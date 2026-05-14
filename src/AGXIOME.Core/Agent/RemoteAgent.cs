using AGXIOME.Shared.Models;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;

namespace AGXIOME.Core.Agent;

// ─────────────────────────────────────────────────────────────────────────────
// RemoteAgent — CE-03
// Delegates agent execution to a remote AGXIOME Runtime host via HTTP REST.
// This is a transport adapter — it does not re-implement local agent logic.
// The remote host is authoritative for all tool calls and state mutations.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Configuration for connecting to a remote AGXIOME Runtime instance.</summary>
public record RemoteAgentOptions
{
    /// <summary>Base URL of the remote AGXIOME Runtime host (e.g. https://host:5250).</summary>
    public required string BaseUrl { get; init; }

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

    /// <summary>Stream SSE tokens from the remote host.</summary>
    IAsyncEnumerable<string> StreamAsync(AgentRequest request, CancellationToken ct = default);
}

/// <summary>HTTP-backed remote agent. Use <see cref="RemoteAgentFactory"/> for DI-integrated creation.</summary>
public sealed class RemoteAgent : IRemoteAgent
{
    private readonly HttpClient _http;
    private readonly RemoteAgentOptions _opts;

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

    public async Task<AgentResponse> RunAsync(AgentRequest request, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("api/agent/run", request, ct);
        resp.EnsureSuccessStatusCode();

        var result = await resp.Content.ReadFromJsonAsync<AgentResponse>(ct);
        return result
            ?? throw new InvalidOperationException("Remote host returned null AgentResponse");
    }

    public async IAsyncEnumerable<string> StreamAsync(
        AgentRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var reqMsg = new HttpRequestMessage(HttpMethod.Post, "api/agent/stream");
        reqMsg.Content = System.Net.Http.Json.JsonContent.Create(request);

        using var resp = await _http.SendAsync(reqMsg, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new System.IO.StreamReader(stream);

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null) break;
            // SSE format: "data: <token>" — strip the prefix and emit.
            if (line.StartsWith("data:", StringComparison.Ordinal))
                yield return line["data:".Length..].Trim();
        }
    }
}

/// <summary>Creates <see cref="RemoteAgent"/> instances without requiring DI container access.</summary>
public static class RemoteAgentFactory
{
    public static RemoteAgent Create(RemoteAgentOptions opts)
    {
        var http = new HttpClient();
        return new RemoteAgent(http, opts);
    }
}
