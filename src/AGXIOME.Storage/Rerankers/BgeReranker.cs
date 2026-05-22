using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage.Rerankers;

// ─────────────────────────────────────────────────────────────────────────────
// RK-08 — BgeApiReranker  (BGE-Reranker via HTTP API)
//
// Compatible endpoints:
//   - InfinityEmbeddings:          https://github.com/michaelfeil/infinity
//   - text-embeddings-inference:   https://github.com/huggingface/text-embeddings-inference
//   - SentenceTransformers API:    custom self-hosted service
//
// Default endpoint: http://localhost:7997  (InfinityEmbeddings default)
// Default model:    BAAI/bge-reranker-v2-m3
//
// POST /rerank
// Body: { "query": "...", "texts": ["...", "..."], "model": "...", "top_n": 5 }
// Response: { "results": [{ "index": 0, "score": 0.95, "document": {"text":"..."} }] }
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Configuration for a BGE-compatible HTTP reranker API.</summary>
public record BgeRerankerOptions
{
    /// <summary>Base URL of the InfinityEmbeddings / TEI rerank endpoint.</summary>
    public string BaseUrl { get; init; } = "http://localhost:7997";

    /// <summary>BGE reranker model name as expected by the server.</summary>
    public string Model   { get; init; } = "BAAI/bge-reranker-v2-m3";

    /// <summary>Optional Bearer token for authenticated deployments.</summary>
    public string? ApiKey { get; init; }
}

/// <summary>
/// HTTP-based reranker backed by a BGE model served through an InfinityEmbeddings-
/// or text-embeddings-inference-compatible <c>/rerank</c> endpoint.
/// Requires a self-hosted or cloud-deployed BGE reranker service; no extra NuGet packages.
/// </summary>
public sealed class BgeApiReranker : IReranker, IDisposable
{
    private readonly HttpClient _http;
    private readonly BgeRerankerOptions _opts;

    public RerankerType RerankerType => RerankerType.BgeApi;

    /// <summary>Constructor for DI / manual wiring with an externally-owned <see cref="HttpClient"/>.</summary>
    public BgeApiReranker(HttpClient http, BgeRerankerOptions opts)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(opts);
        _http = http;
        _http.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
        if (!string.IsNullOrWhiteSpace(opts.ApiKey))
            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opts.ApiKey);
        _opts = opts;
    }

    /// <summary>Convenience constructor that creates its own <see cref="HttpClient"/>.</summary>
    public BgeApiReranker(BgeRerankerOptions opts) : this(new HttpClient(), opts) { }

    /// <summary>Convenience constructor using default options (localhost:7997).</summary>
    public BgeApiReranker() : this(new BgeRerankerOptions()) { }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RerankResult>> RerankAsync(
        string query,
        IReadOnlyList<RerankCandidate> candidates,
        int topK = 0,
        CancellationToken ct = default)
    {
        if (candidates.Count == 0) return [];

        var topN = topK <= 0 ? candidates.Count : topK;

        var body = new
        {
            query,
            texts  = candidates.Select(c => c.Text).ToArray(),
            model  = _opts.Model,
            top_n  = topN,
        };

        var resp = await _http.PostAsJsonAsync("rerank", body, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        var raw = await resp.Content.ReadFromJsonAsync<BgeRerankResponse>(ct).ConfigureAwait(false);
        if (raw?.Results is null) return [];

        return raw.Results
            .Select((r, idx) => new RerankResult(
                Id:           candidates[r.Index].Id,
                Text:         candidates[r.Index].Text,
                Score:        r.Score,
                OriginalRank: r.Index,
                NewRank:      idx,
                Metadata:     candidates[r.Index].Metadata))
            .ToList();
    }

    /// <inheritdoc />
    public void Dispose() => _http.Dispose();

    // ── JSON shape ────────────────────────────────────────────────────────────
    // InfinityEmbeddings format:
    // { "results": [{ "index": 0, "score": 0.95, "document": { "text": "..." } }] }

    private sealed class BgeRerankResponse
    {
        [JsonPropertyName("results")]
        public List<BgeRerankItem>? Results { get; set; }
    }

    private sealed class BgeRerankItem
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("score")]
        public float Score { get; set; }
    }
}
