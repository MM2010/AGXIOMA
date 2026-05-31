using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage.Embedders;

// ─────────────────────────────────────────────────────────────────────────────
// Shared HTTP helper for OpenAI-compatible embedding endpoints.
// Handles /v1/embeddings with Bearer auth.
// ─────────────────────────────────────────────────────────────────────────────
internal sealed class OpenAICompatEmbedderClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _model;

    internal OpenAICompatEmbedderClient(HttpClient http, string model)
    {
        _http  = http  ?? throw new ArgumentNullException(nameof(http));
        _model = model ?? throw new ArgumentNullException(nameof(model));
    }

    internal async Task<float[]> EmbedAsync(string text, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(text)) return [];

        var resp = await _http.PostAsJsonAsync(
            "v1/embeddings",
            new { input = text, model = _model, encoding_format = "float" }, ct);
        resp.EnsureSuccessStatusCode();

        var raw = await resp.Content.ReadFromJsonAsync<OpenAIEmbedResponse>(ct);
        return raw?.Data?[0].Embedding ?? [];
    }

    internal async Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct)
    {
        if (texts.Count == 0) return [];

        var resp = await _http.PostAsJsonAsync(
            "v1/embeddings",
            new { input = texts, model = _model, encoding_format = "float" }, ct);
        resp.EnsureSuccessStatusCode();

        var raw = await resp.Content.ReadFromJsonAsync<OpenAIEmbedResponse>(ct);
        if (raw?.Data is null) return new float[texts.Count][];

        // API returns results in order by default; re-sort by index for safety.
        var sorted = raw.Data.OrderBy(d => d.Index).Select(d => d.Embedding).ToArray();
        return sorted;
    }

    public void Dispose() => _http.Dispose();

    private sealed class OpenAIEmbedResponse
    {
        [JsonPropertyName("data")]
        public List<OpenAIEmbedData>? Data { get; set; }
    }

    private sealed class OpenAIEmbedData
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("embedding")]
        public float[] Embedding { get; set; } = [];
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// E-01 — OpenAI text-embedding-3-small (1536 dims)
// E-02 — OpenAI text-embedding-3-large (3072 dims)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Data transfer record for the AGXIOME framework.</summary>
public sealed record OpenAIEmbedderOptions
{
    public required string ApiKey  { get; init; }
    public string BaseUrl          { get; init; } = "https://api.openai.com/";
    public int MaxRetries          { get; init; } = 3;
}

public sealed class OpenAISmallEmbedder : IEmbedder, IDisposable
{
    private readonly OpenAICompatEmbedderClient _client;

    public EmbedderType EmbedderType => EmbedderType.OpenAISmall;
    public int Dimensions            => 1536;

    public OpenAISmallEmbedder(HttpClient http, OpenAIEmbedderOptions opts)
    {
        ArgumentNullException.ThrowIfNull(opts);
        http.BaseAddress ??= new Uri(opts.BaseUrl);
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opts.ApiKey);
        _client = new OpenAICompatEmbedderClient(http, "text-embedding-3-small");
    }

    public Task<float[]>   EmbedAsync(string text, CancellationToken ct = default)
        => _client.EmbedAsync(text, ct);
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
        => _client.EmbedBatchAsync(texts, ct);
    public void Dispose() => _client.Dispose();
}

public sealed class OpenAILargeEmbedder : IEmbedder, IDisposable
{
    private readonly OpenAICompatEmbedderClient _client;

    public EmbedderType EmbedderType => EmbedderType.OpenAILarge;
    public int Dimensions            => 3072;

    public OpenAILargeEmbedder(HttpClient http, OpenAIEmbedderOptions opts)
    {
        ArgumentNullException.ThrowIfNull(opts);
        http.BaseAddress ??= new Uri(opts.BaseUrl);
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opts.ApiKey);
        _client = new OpenAICompatEmbedderClient(http, "text-embedding-3-large");
    }

    public Task<float[]>   EmbedAsync(string text, CancellationToken ct = default)
        => _client.EmbedAsync(text, ct);
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
        => _client.EmbedBatchAsync(texts, ct);
    public void Dispose() => _client.Dispose();
}
