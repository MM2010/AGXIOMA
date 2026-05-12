using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage.Embedders;

// ─────────────────────────────────────────────────────────────────────────────
// E-06 — Google Gemini Embed (text-embedding-004, 768 dims)
// POST /v1beta/models/{model}:embedContent  (single)
// POST /v1beta/models/{model}:batchEmbedContents  (batch)
// ─────────────────────────────────────────────────────────────────────────────

public sealed record GeminiEmbedderOptions
{
    public required string ApiKey { get; init; }
    public string BaseUrl          { get; init; } = "https://generativelanguage.googleapis.com/";
    public string Model            { get; init; } = "text-embedding-004";
    /// <summary>Gemini task type. "RETRIEVAL_DOCUMENT" is optimal for passage embeddings.</summary>
    public string TaskType         { get; init; } = "RETRIEVAL_DOCUMENT";
}

public sealed class GeminiEmbedder : IEmbedder, IDisposable
{
    private readonly HttpClient _http;
    private readonly GeminiEmbedderOptions _opts;

    public EmbedderType EmbedderType => EmbedderType.GoogleGemini;
    public int Dimensions            => 768;

    public GeminiEmbedder(HttpClient http, GeminiEmbedderOptions opts)
    {
        _opts = opts ?? throw new ArgumentNullException(nameof(opts));
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _http.BaseAddress ??= new Uri(opts.BaseUrl);
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(text)) return new float[Dimensions];

        var url  = $"v1beta/models/{_opts.Model}:embedContent?key={_opts.ApiKey}";
        var body = new { content = new { parts = new[] { new { text } } }, taskType = _opts.TaskType };
        var resp = await _http.PostAsJsonAsync(url, body, ct);
        resp.EnsureSuccessStatusCode();

        var raw = await resp.Content.ReadFromJsonAsync<GeminiEmbedResponse>(ct);
        return raw?.Embedding?.Values ?? new float[Dimensions];
    }

    public async Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        if (texts.Count == 0) return [];

        var url = $"v1beta/models/{_opts.Model}:batchEmbedContents?key={_opts.ApiKey}";
        var requests = texts.Select(t => new
        {
            model   = $"models/{_opts.Model}",
            content = new { parts = new[] { new { text = t } } },
            taskType = _opts.TaskType
        }).ToArray();

        var resp = await _http.PostAsJsonAsync(url, new { requests }, ct);
        resp.EnsureSuccessStatusCode();

        var raw = await resp.Content.ReadFromJsonAsync<GeminiBatchResponse>(ct);
        return raw?.Embeddings?.Select(e => e.Values ?? new float[Dimensions]).ToArray()
               ?? new float[texts.Count][];
    }

    public void Dispose() => _http.Dispose();

    private sealed class GeminiEmbedResponse
    {
        [JsonPropertyName("embedding")]
        public GeminiEmbedding? Embedding { get; set; }
    }

    private sealed class GeminiBatchResponse
    {
        [JsonPropertyName("embeddings")]
        public List<GeminiEmbedding>? Embeddings { get; set; }
    }

    private sealed class GeminiEmbedding
    {
        [JsonPropertyName("values")]
        public float[]? Values { get; set; }
    }
}
