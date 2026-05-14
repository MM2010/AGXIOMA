using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage.Embedders;

// ─────────────────────────────────────────────────────────────────────────────
// E-04 — Cohere Embeddings (embed-english-v3.0, 1024 dims)
// POST v2/embed — uses input_type="search_document" for document embedding.
// ─────────────────────────────────────────────────────────────────────────────

public sealed record CohereEmbedderOptions
{
    public required string ApiKey { get; init; }
    public string BaseUrl          { get; init; } = "https://api.cohere.com/";
    public string Model            { get; init; } = "embed-english-v3.0";
    public string InputType        { get; init; } = "search_document";
}

public sealed class CohereEmbedder : IEmbedder, IDisposable
{
    private readonly HttpClient _http;
    private readonly CohereEmbedderOptions _opts;

    public EmbedderType EmbedderType => EmbedderType.Cohere;
    public int Dimensions            => 1024;

    public CohereEmbedder(HttpClient http, CohereEmbedderOptions opts)
    {
        _opts = opts ?? throw new ArgumentNullException(nameof(opts));
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _http.BaseAddress ??= new Uri(opts.BaseUrl);
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opts.ApiKey);
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(text)) return new float[Dimensions];

        var batch = await EmbedBatchAsync([text], ct);
        return batch[0];
    }

    public async Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        if (texts.Count == 0) return [];

        var resp = await _http.PostAsJsonAsync("v2/embed", new
        {
            texts      = texts,
            model      = _opts.Model,
            input_type = _opts.InputType,
            // embedding_types not specified → API returns float by default
        }, ct);
        resp.EnsureSuccessStatusCode();

        var raw = await resp.Content.ReadFromJsonAsync<CohereEmbedResponse>(ct);
        return raw?.Embeddings?.Float ?? new float[texts.Count][];
    }

    public void Dispose() => _http.Dispose();

    private sealed class CohereEmbedResponse
    {
        [JsonPropertyName("embeddings")]
        public CohereEmbeddings? Embeddings { get; set; }
    }

    private sealed class CohereEmbeddings
    {
        [JsonPropertyName("float")]
        public float[][]? Float { get; set; }
    }
}
