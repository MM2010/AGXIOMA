using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage.Embedders;

// ─────────────────────────────────────────────────────────────────────────────
// E-16 — llama.cpp Server Embeddings (768+ dims, GGUF models)
// POST /embedding  (llama.cpp server REST API)
// The server must be started with --embedding flag enabled.
// ─────────────────────────────────────────────────────────────────────────────

public sealed record LlamaCppEmbedderOptions
{
    public string BaseUrl    { get; init; } = "http://localhost:8080/";
    /// <summary>Dimensions depend on the loaded GGUF model; default 768 is typical.</summary>
    public int Dimensions    { get; init; } = 768;
}

public sealed class LlamaCppEmbedder : IEmbedder, IDisposable
{
    private readonly HttpClient _http;
    private readonly LlamaCppEmbedderOptions _opts;

    public EmbedderType EmbedderType => EmbedderType.LlamaCpp;
    public int Dimensions            => _opts.Dimensions;

    public LlamaCppEmbedder(HttpClient http, LlamaCppEmbedderOptions? opts = null)
    {
        _opts = opts ?? new LlamaCppEmbedderOptions();
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _http.BaseAddress ??= new Uri(_opts.BaseUrl);
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(text)) return new float[Dimensions];

        var resp = await _http.PostAsJsonAsync("embedding", new { content = text }, ct);
        resp.EnsureSuccessStatusCode();

        var raw = await resp.Content.ReadFromJsonAsync<LlamaCppEmbedResponse>(ct);
        return raw?.Embedding ?? new float[Dimensions];
    }

    public async Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        if (texts.Count == 0) return [];

        // llama.cpp server accepts one text per call.
        // Fan-out concurrently with a bounded semaphore to avoid overloading the server.
        var sem     = new SemaphoreSlim(4, 4);
        var tasks   = texts.Select(async t =>
        {
            await sem.WaitAsync(ct);
            try   { return await EmbedAsync(t, ct); }
            finally { sem.Release(); }
        });
        return await Task.WhenAll(tasks);
    }

    public void Dispose() => _http.Dispose();

    private sealed class LlamaCppEmbedResponse
    {
        [JsonPropertyName("embedding")]
        public float[]? Embedding { get; set; }
    }
}
