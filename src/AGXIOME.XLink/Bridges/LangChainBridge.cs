// MU-08 — LangChain Bridge
// Wraps LangChain-compatible agent invocations via HTTP JSON-RPC.
// AGXIOME acts as orchestrator; LangChain agent is a remote capability endpoint.
// No direct LangChain SDK dependency; uses HTTP to avoid runtime coupling.

namespace AGXIOME.XLink.Bridges;

// ── Shared result type ────────────────────────────────────────────────────────

/// <summary>Unified result from any framework bridge call.</summary>
public sealed record AgentBridgeResult(
    bool   Success,
    string? Content,
    string? Error           = null,
    string? FrameworkName   = null,
    long    LatencyMs       = 0);

// ── LangChain Bridge ──────────────────────────────────────────────────────────

public interface ILangChainBridge
{
    /// <summary>Is the LangChain endpoint configured and reachable?</summary>
    bool IsAvailable { get; }

    /// <summary>Run a LangChain chain by name with the given inputs.</summary>
    Task<AgentBridgeResult> RunChainAsync(
        string chainName,
        Dictionary<string, string> inputs,
        CancellationToken ct = default);

    /// <summary>List chains registered at the LangChain endpoint.</summary>
    Task<IReadOnlyList<string>> ListChainsAsync(CancellationToken ct = default);
}

public sealed class LangChainBridge : ILangChainBridge
{
    private readonly string? _endpointUrl;
    private readonly System.Net.Http.HttpClient _http;

    // endpoint url expected from env: LANGCHAIN_ENDPOINT (e.g. http://localhost:8001)
    public LangChainBridge(string? endpointUrl = null, System.Net.Http.HttpClient? http = null)
    {
        _endpointUrl = endpointUrl ?? Environment.GetEnvironmentVariable("LANGCHAIN_ENDPOINT");
        _http        = http ?? new System.Net.Http.HttpClient();
    }

    public bool IsAvailable => !string.IsNullOrWhiteSpace(_endpointUrl);

    public async Task<AgentBridgeResult> RunChainAsync(
        string chainName,
        Dictionary<string, string> inputs,
        CancellationToken ct = default)
    {
        if (!IsAvailable) return Unavailable("LangChain endpoint not configured");

        var sw   = System.Diagnostics.Stopwatch.StartNew();
        var body = System.Text.Json.JsonSerializer.Serialize(new { chain = chainName, inputs });
        try
        {
            using var req  = new System.Net.Http.StringContent(body, System.Text.Encoding.UTF8, "application/json");
            using var resp = await _http.PostAsync($"{_endpointUrl}/run", req, ct);
            sw.Stop();
            var content    = await resp.Content.ReadAsStringAsync(ct);
            return resp.IsSuccessStatusCode
                ? new AgentBridgeResult(true,  content, null, "LangChain", sw.ElapsedMilliseconds)
                : new AgentBridgeResult(false, null, content,   "LangChain", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            return new AgentBridgeResult(false, null, ex.Message, "LangChain", sw.ElapsedMilliseconds);
        }
    }

    public async Task<IReadOnlyList<string>> ListChainsAsync(CancellationToken ct = default)
    {
        if (!IsAvailable) return [];
        try
        {
            var json   = await _http.GetStringAsync($"{_endpointUrl}/chains", ct);
            var chains = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);
            return chains ?? [];
        }
        catch { return []; }
    }

    private static AgentBridgeResult Unavailable(string reason)
        => new(false, null, reason, "LangChain");
}
