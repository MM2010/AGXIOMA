using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using AGXIOME.Shared.Config;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace AGXIOME.Core.Agent;

// ─────────────────────────────────────────────────────────────────────────────
// RemoteAgent — CE-03
// Delegates agent execution to a remote AGXIOME Runtime host via HTTP REST.
// This is a transport adapter — it does not re-implement local agent logic.
// The remote host is authoritative for all tool calls and state mutations.
//
// Endpoints used:
//   POST /api/runs          → sync run, returns { runId, status, content }
//   POST /api/runs/async    → background run, returns { runId }
//   GET  /api/runs/{id}/stream?input=... → SSE stream
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Configuration for connecting to a remote AGXIOME Runtime instance.</summary>
public record RemoteAgentOptions
{
    /// <summary>Base URL of the remote AGXIOME Runtime host (e.g. https://host:5250).</summary>
    public required string BaseUrl { get; init; }

    /// <summary>AgentId to send with every run request. Defaults to "remote".</summary>
    public string AgentId { get; init; } = "remote";

    /// <summary>Timeout per remote run call. Increase for multi-tool or long-reasoning agents.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>Bearer token for authenticating with the remote runtime. Never log this value.</summary>
    public string? BearerToken { get; init; }
}

/// <summary>Contract for remote agent execution. Smaller surface than IAgxiomeAgent
/// — remote operations are limited to run, stream, and reset.</summary>
public interface IRemoteAgent
{
    string RemoteBaseUrl { get; }

    /// <summary>Execute a prompt on the remote host. Returns the authoritative response.</summary>
    Task<AgentResponse> RunAsync(AgentRequest request, CancellationToken ct = default);

    /// <summary>Stream SSE tokens from the remote host. Returns raw chunk payloads.</summary>
    IAsyncEnumerable<AgentChunk> StreamAsync(AgentRequest request, CancellationToken ct = default);
}

// ── Response DTOs ─────────────────────────────────────────────────────────────
file record RemoteRunResponse(string? RunId, string? Status, string? Content, string? Message);
file record RemoteAsyncResponse(string? RunId, string? Status);
file record RemoteStreamChunk(string? Chunk, bool IsFinal);

/// <summary>HTTP-backed remote agent. Use <see cref="RemoteAgentFactory"/> for DI-integrated creation.</summary>
public sealed class RemoteAgent : IRemoteAgent
{
    private readonly HttpClient _http;
    private readonly RemoteAgentOptions _opts;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public string RemoteBaseUrl => _opts.BaseUrl;

    public RemoteAgent(HttpClient http, RemoteAgentOptions opts)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(opts);

        _opts = opts;
        _http = http;
        _http.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
        _http.Timeout = opts.Timeout;

        // Inject auth header when provided. Token value must never be logged.
        if (!string.IsNullOrWhiteSpace(opts.BearerToken))
            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opts.BearerToken);
    }

    /// <summary>POST /api/runs — synchronous run on the remote host.</summary>
    public async Task<AgentResponse> RunAsync(AgentRequest request, CancellationToken ct = default)
    {
        var body = new { AgentId = _opts.AgentId, Input = request.Prompt };
        var resp = await _http.PostAsJsonAsync("api/runs", body, ct);
        resp.EnsureSuccessStatusCode();

        var result = await resp.Content.ReadFromJsonAsync<RemoteRunResponse>(_json, ct);
        var content = result?.Content ?? string.Empty;
        var status  = result?.Status == "completed" ? AgentStatus.Completed : AgentStatus.Error;
        return new AgentResponse(content, status);
    }

    /// <summary>
    /// POST /api/runs/async → get runId, then GET /api/runs/{runId}/stream for SSE.
    /// Each yielded chunk contains the parsed content and IsFinal flag.
    /// </summary>
    public async IAsyncEnumerable<AgentChunk> StreamAsync(
        AgentRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // 1. Enqueue async run to obtain runId
        var body = new { AgentId = _opts.AgentId, Input = request.Prompt };
        using var startResp = await _http.PostAsJsonAsync("api/runs/async", body, ct);
        startResp.EnsureSuccessStatusCode();

        var queued = await startResp.Content.ReadFromJsonAsync<RemoteAsyncResponse>(_json, ct);
        var runId  = queued?.RunId ?? throw new InvalidOperationException("Remote host did not return a runId for async run.");

        // 2. Open SSE stream
        var url = $"api/runs/{runId}/stream?input={Uri.EscapeDataString(request.Prompt)}";
        using var streamReq = new HttpRequestMessage(HttpMethod.Get, url);
        using var streamResp = await _http.SendAsync(streamReq, HttpCompletionOption.ResponseHeadersRead, ct);
        streamResp.EnsureSuccessStatusCode();

        using var stream = await streamResp.Content.ReadAsStreamAsync(ct);
        using var reader = new System.IO.StreamReader(stream);

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null) break;
            if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;

            var payload = line["data:".Length..].Trim();
            if (string.IsNullOrEmpty(payload)) continue;

            RemoteStreamChunk? chunk = null;
            try { chunk = JsonSerializer.Deserialize<RemoteStreamChunk>(payload, _json); }
            catch (JsonException) { /* malformed chunk — skip */ }

            var content  = chunk?.Chunk ?? payload;
            var isFinal  = chunk?.IsFinal ?? false;
            yield return new AgentChunk(content, IsFinal: isFinal);

            if (isFinal) break;
        }
    }
}

/// <summary>
/// Wraps <see cref="RemoteAgent"/> and implements <see cref="IAgxiomeAgent"/> for drop-in use
/// anywhere a local agent is expected. Write/mutate operations (RegisterTool, AddHook, etc.)
/// are intentionally no-ops — the remote host owns its own tool and hook configuration.
/// </summary>
public sealed class RemoteAgentAdapter : IAgxiomeAgent
{
    private readonly RemoteAgent _remote;

    public string Id { get; }
    public AgentStatus Status { get; private set; } = AgentStatus.Idle;
    public IAgentMemory Memory { get; } = new AgentMemoryManager();
    public ILearningMachine? Learning => null;
    public string? SessionId => null;
    public ISessionState SessionState { get; } = new SessionState();

    public RemoteAgentAdapter(RemoteAgent remote, string? id = null)
    {
        _remote = remote;
        Id = id ?? $"remote:{new Uri(remote.RemoteBaseUrl).Host}";
    }

    public async Task<AgentResponse> RunAsync(AgentRequest request, CancellationToken ct = default)
    {
        Status = AgentStatus.Thinking;
        try
        {
            var result = await _remote.RunAsync(request, ct);
            Status = result.Status;
            return result;
        }
        catch { Status = AgentStatus.Error; throw; }
    }

    public async Task<T> RunAsync<T>(AgentRequest request, CancellationToken ct = default)
    {
        var resp = await RunAsync(request, ct);
        return JsonSerializer.Deserialize<T>(resp.Content, new JsonSerializerOptions(JsonSerializerDefaults.Web))
               ?? throw new InvalidOperationException("Remote response could not be deserialized to " + typeof(T).Name);
    }

    public async IAsyncEnumerable<AgentChunk> RunStreamAsync(
        AgentRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        Status = AgentStatus.Streaming;
        await foreach (var chunk in _remote.StreamAsync(request, ct))
        {
            if (chunk.IsFinal) Status = AgentStatus.Completed;
            yield return chunk;
        }
    }

    public async IAsyncEnumerable<RunOutputEvent> RunEventStreamAsync(
        AgentRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var chunk in RunStreamAsync(request, ct))
            yield return new RunOutputEvent(
                chunk.IsFinal ? RunEvent.RunCompleted : RunEvent.RunContent,
                Content: chunk.Content);
    }

    public Task<AgentResponse> ContinueRunAsync(string runId, IReadOnlyList<HITLRequirement> requirements, CancellationToken ct = default)
        => throw new NotSupportedException("ContinueRun is not supported over the remote transport.");

    // ── No-op write operations ────────────────────────────────────────────────
    public void RegisterFunction<T>() where T : AgxiomeFunction    { }
    public void RegisterTool(IAgxiomeTool tool)                     { }
    public void UseMiddleware(IAgentMiddleware middleware)           { }
    public void AddHook(IAgentHook hook)                            { }
    public void AddToolHook(IToolHook toolHook)                     { }
    public void AddGuardrail(IGuardrail guardrail)                  { }
    public void SetOutputSchema<T>() where T : class                { }
    public void SetInputSchema<T>() where T : class                 { }
    public void RequireConfirmationFor<T>() where T : AgxiomeFunction { }
    public void EnableLearning(LearningConfig? config = null)       { }
    public void Dispose()                                            { }
}

/// <summary>Creates <see cref="RemoteAgent"/> and <see cref="RemoteAgentAdapter"/> instances.</summary>
public static class RemoteAgentFactory
{
    /// <summary>Create a raw <see cref="RemoteAgent"/> (limited interface).</summary>
    public static RemoteAgent Create(RemoteAgentOptions opts)
    {
        var http = new HttpClient();
        return new RemoteAgent(http, opts);
    }

    /// <summary>Create a full <see cref="IAgxiomeAgent"/> adapter over a remote Runtime host.</summary>
    public static IAgxiomeAgent CreateAdapter(RemoteAgentOptions opts, string? agentId = null)
    {
        var remote = Create(opts);
        return new RemoteAgentAdapter(remote, agentId);
    }
}
