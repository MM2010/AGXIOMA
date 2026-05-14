using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage.Embedders;

// ─────────────────────────────────────────────────────────────────────────────
// E-07 — Voyage AI Embeddings (voyage-3-large, 1024 dims)
// POST v1/embeddings — proprietary schema, not OpenAI-compat.
// ─────────────────────────────────────────────────────────────────────────────

public sealed record VoyageEmbedderOptions
{
    public required string ApiKey { get; init; }
    public string BaseUrl          { get; init; } = "https://api.voyageai.com/";
    public string Model            { get; init; } = "voyage-3-large";
    public string InputType        { get; init; } = "document";
}

public sealed class VoyageEmbedder : IEmbedder, IDisposable
{
    private readonly HttpClient _http;
    private readonly VoyageEmbedderOptions _opts;

    public EmbedderType EmbedderType => EmbedderType.VoyageAI;
    public int Dimensions            => 1024;

    public VoyageEmbedder(HttpClient http, VoyageEmbedderOptions opts)
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

        var resp = await _http.PostAsJsonAsync("v1/embeddings", new
        {
            input      = texts,
            model      = _opts.Model,
            input_type = _opts.InputType,
        }, ct);
        resp.EnsureSuccessStatusCode();

        var raw = await resp.Content.ReadFromJsonAsync<VoyageEmbedResponse>(ct);
        if (raw?.Data is null) return new float[texts.Count][];

        return raw.Data.OrderBy(d => d.Index).Select(d => d.Embedding).ToArray();
    }

    public void Dispose() => _http.Dispose();

    private sealed class VoyageEmbedResponse
    {
        [JsonPropertyName("data")]
        public List<VoyageEmbedData>? Data { get; set; }
    }

    private sealed class VoyageEmbedData
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("embedding")]
        public float[] Embedding { get; set; } = [];
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// E-08 — Jina AI Embeddings (jina-embeddings-v3, 1024 dims)
// POST v1/embeddings — OpenAI-compatible schema.
// ─────────────────────────────────────────────────────────────────────────────

public sealed record JinaEmbedderOptions
{
    public required string ApiKey { get; init; }
    public string BaseUrl          { get; init; } = "https://api.jina.ai/";
    public string Model            { get; init; } = "jina-embeddings-v3";
    public string Task             { get; init; } = "retrieval.passage";
}

public sealed class JinaEmbedder : IEmbedder, IDisposable
{
    private readonly HttpClient _http;
    private readonly JinaEmbedderOptions _opts;

    public EmbedderType EmbedderType => EmbedderType.JinaAI;
    public int Dimensions            => 1024;

    public JinaEmbedder(HttpClient http, JinaEmbedderOptions opts)
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

        var resp = await _http.PostAsJsonAsync("v1/embeddings", new
        {
            input = texts,
            model = _opts.Model,
            task  = _opts.Task,
        }, ct);
        resp.EnsureSuccessStatusCode();

        var raw = await resp.Content.ReadFromJsonAsync<JinaEmbedResponse>(ct);
        if (raw?.Data is null) return new float[texts.Count][];

        return raw.Data.OrderBy(d => d.Index).Select(d => d.Embedding).ToArray();
    }

    public void Dispose() => _http.Dispose();

    private sealed class JinaEmbedResponse
    {
        [JsonPropertyName("data")]
        public List<JinaEmbedData>? Data { get; set; }
    }

    private sealed class JinaEmbedData
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("embedding")]
        public float[] Embedding { get; set; } = [];
    }
}
