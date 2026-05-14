using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage.Embedders;

// ─────────────────────────────────────────────────────────────────────────────
// E-03 — Anthropic Embeddings
// Anthropic does not yet expose a public embeddings REST endpoint; when they do
// it will follow the pattern below. This adapter is ready to activate once the
// endpoint ships (target: /v1/embeddings, Bearer auth, same schema as OpenAI).
// ─────────────────────────────────────────────────────────────────────────────

public sealed record AnthropicEmbedderOptions
{
    public required string ApiKey { get; init; }
    public string BaseUrl         { get; init; } = "https://api.anthropic.com/";
    public string Model           { get; init; } = "claude-embed-v1";  // placeholder
    public int Dimensions         { get; init; } = 1536;
}

public sealed class AnthropicEmbedder : IEmbedder, IDisposable
{
    private readonly HttpClient _http;
    private readonly AnthropicEmbedderOptions _opts;

    public EmbedderType EmbedderType => EmbedderType.Anthropic;
    public int Dimensions            => _opts.Dimensions;

    public AnthropicEmbedder(HttpClient http, AnthropicEmbedderOptions opts)
    {
        _opts = opts ?? throw new ArgumentNullException(nameof(opts));
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _http.BaseAddress ??= new Uri(opts.BaseUrl);
        _http.DefaultRequestHeaders.TryAddWithoutValidation("x-api-key", opts.ApiKey);
        _http.DefaultRequestHeaders.TryAddWithoutValidation("anthropic-version", "2023-06-01");
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(text)) return new float[_opts.Dimensions];

        var resp = await _http.PostAsJsonAsync(
            "v1/embeddings",
            new { input = text, model = _opts.Model }, ct);
        resp.EnsureSuccessStatusCode();

        var raw = await resp.Content.ReadFromJsonAsync<AnthropicEmbedResponse>(ct);
        return raw?.Data?[0].Embedding ?? new float[_opts.Dimensions];
    }

    public async Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        if (texts.Count == 0) return [];
        var tasks = texts.Select(t => EmbedAsync(t, ct));
        return await Task.WhenAll(tasks);
    }

    public void Dispose() => _http.Dispose();

    private sealed class AnthropicEmbedResponse
    {
        [JsonPropertyName("data")]
        public List<AnthropicEmbedData>? Data { get; set; }
    }

    private sealed class AnthropicEmbedData
    {
        [JsonPropertyName("embedding")]
        public float[] Embedding { get; set; } = [];
    }
}
