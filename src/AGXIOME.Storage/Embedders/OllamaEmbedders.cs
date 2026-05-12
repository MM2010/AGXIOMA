using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage.Embedders;

// ─────────────────────────────────────────────────────────────────────────────
// E-14 — Ollama Embed (nomic-embed-text, 768 dims)
// E-15 — Ollama Embed v2 (mxbai-embed-large, 1024 dims)
//
// Ollama exposes a single POST /api/embed endpoint (v0.3+) that accepts a
// model name and a list of input strings.
// ─────────────────────────────────────────────────────────────────────────────

public sealed record OllamaEmbedderOptions
{
    public string BaseUrl { get; init; } = "http://localhost:11434/";
    public string Model   { get; init; } = "nomic-embed-text";
}

/// <summary>Shared HTTP client for Ollama /api/embed endpoint.</summary>
internal sealed class OllamaEmbedClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _model;

    internal OllamaEmbedClient(HttpClient http, OllamaEmbedderOptions opts)
    {
        _http  = http;
        _model = opts.Model;
        _http.BaseAddress ??= new Uri(opts.BaseUrl);
    }

    internal async Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct)
    {
        if (texts.Count == 0) return [];

        var resp = await _http.PostAsJsonAsync("api/embed",
            new { model = _model, input = texts }, ct);
        resp.EnsureSuccessStatusCode();

        var raw = await resp.Content.ReadFromJsonAsync<OllamaEmbedResponse>(ct);
        return raw?.Embeddings ?? new float[texts.Count][];
    }

    public void Dispose() => _http.Dispose();

    private sealed class OllamaEmbedResponse
    {
        [JsonPropertyName("embeddings")]
        public float[][]? Embeddings { get; set; }
    }
}

public sealed class OllamaNomicEmbedder : IEmbedder, IDisposable
{
    private readonly OllamaEmbedClient _client;

    public EmbedderType EmbedderType => EmbedderType.OllamaNomic;
    public int Dimensions            => 768;

    public OllamaNomicEmbedder(HttpClient http, OllamaEmbedderOptions? opts = null)
    {
        var o = opts ?? new OllamaEmbedderOptions { Model = "nomic-embed-text" };
        _client = new OllamaEmbedClient(http, o);
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(text)) return new float[Dimensions];
        var batch = await _client.EmbedBatchAsync([text], ct);
        return batch[0];
    }

    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
        => _client.EmbedBatchAsync(texts, ct);

    public void Dispose() => _client.Dispose();
}

public sealed class OllamaMxbaiEmbedder : IEmbedder, IDisposable
{
    private readonly OllamaEmbedClient _client;

    public EmbedderType EmbedderType => EmbedderType.OllamaMxbai;
    public int Dimensions            => 1024;

    public OllamaMxbaiEmbedder(HttpClient http, OllamaEmbedderOptions? opts = null)
    {
        var o = opts ?? new OllamaEmbedderOptions { Model = "mxbai-embed-large" };
        _client = new OllamaEmbedClient(http, o);
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(text)) return new float[Dimensions];
        var batch = await _client.EmbedBatchAsync([text], ct);
        return batch[0];
    }

    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
        => _client.EmbedBatchAsync(texts, ct);

    public void Dispose() => _client.Dispose();
}
