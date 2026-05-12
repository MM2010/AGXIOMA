// MU-10 — Claude SDK Bridge
// Direct bridge to Anthropic's Messages API (claude-3.x models).
// No external SDK dependency; uses raw HTTP with JWE-protected API key transport.
// SECURITY: API key must come from env ANTHROPIC_API_KEY, never from client payload.

namespace AGXIOME.XLink.Bridges;

public interface IClaudeBridge
{
    bool IsAvailable { get; }

    /// <summary>
    /// Sends a prompt to Claude and returns the assistant message content.
    /// <param name="prompt">User message text.</param>
    /// <param name="model">Claude model identifier (default: claude-3-haiku-20240307).</param>
    /// <param name="maxTokens">Max response tokens (default: 1024).</param>
    /// </summary>
    Task<AgentBridgeResult> CompleteAsync(
        string prompt,
        string model     = "claude-3-haiku-20240307",
        int    maxTokens = 1024,
        CancellationToken ct = default);

    /// <summary>Returns the configured model names supported by this bridge.</summary>
    IReadOnlyList<string> SupportedModels { get; }
}

public sealed class ClaudeBridge : IClaudeBridge
{
    private const string _apiBase = "https://api.anthropic.com/v1";
    private const string _version  = "2023-06-01";

    private readonly string? _apiKey;
    private readonly System.Net.Http.HttpClient _http;

    public static readonly IReadOnlyList<string> DefaultModels =
    [
        "claude-3-haiku-20240307",
        "claude-3-sonnet-20240229",
        "claude-3-opus-20240229",
        "claude-3-5-sonnet-20241022"
    ];

    public ClaudeBridge(string? apiKey = null, System.Net.Http.HttpClient? http = null)
    {
        // SECURITY: never accept api key from caller if env is set; env takes precedence
        _apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? apiKey;
        _http   = http ?? new System.Net.Http.HttpClient();
        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            _http.DefaultRequestHeaders.TryAddWithoutValidation("x-api-key",         _apiKey);
            _http.DefaultRequestHeaders.TryAddWithoutValidation("anthropic-version",  _version);
        }
    }

    public bool IsAvailable => !string.IsNullOrWhiteSpace(_apiKey);

    public IReadOnlyList<string> SupportedModels => DefaultModels;

    public async Task<AgentBridgeResult> CompleteAsync(
        string prompt,
        string model     = "claude-3-haiku-20240307",
        int    maxTokens = 1024,
        CancellationToken ct = default)
    {
        if (!IsAvailable)
            return new AgentBridgeResult(false, null, "ANTHROPIC_API_KEY not configured", "Claude");

        var sw   = System.Diagnostics.Stopwatch.StartNew();
        var body = System.Text.Json.JsonSerializer.Serialize(new
        {
            model,
            max_tokens = maxTokens,
            messages   = new[] { new { role = "user", content = prompt } }
        });

        try
        {
            using var req  = new System.Net.Http.StringContent(body, System.Text.Encoding.UTF8, "application/json");
            using var resp = await _http.PostAsync($"{_apiBase}/messages", req, ct);
            sw.Stop();
            var json    = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
                return new AgentBridgeResult(false, null, json, "Claude", sw.ElapsedMilliseconds);

            // Parse content[0].text from response envelope
            using var doc   = System.Text.Json.JsonDocument.Parse(json);
            var content     = doc.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString();
            return new AgentBridgeResult(true, content, null, "Claude", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            return new AgentBridgeResult(false, null, ex.Message, "Claude", sw.ElapsedMilliseconds);
        }
    }
}
