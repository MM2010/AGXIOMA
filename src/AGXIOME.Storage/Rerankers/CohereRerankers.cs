using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage.Rerankers;

// ─────────────────────────────────────────────────────────────────────────────
// RK-01 — CohereRerankEnglish  (rerank-english-v3.0)
// RK-02 — CohereRerankMultilingual (rerank-multilingual-v3.0)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Shared configuration for all Cohere Rerank implementations.
/// API key must be supplied via config/environment — never hardcoded.
/// </summary>
public record CohereRerankerOptions
{
    public required string ApiKey    { get; init; }
    public string BaseUrl            { get; init; } = "https://api.cohere.ai";
    public int MaxRetries            { get; init; } = 2;
}

/// <summary>
/// HTTP client adapter for the Cohere Rerank v2 endpoint.
/// Shared by both English and Multilingual model variants.
/// </summary>
internal sealed class CohereRerankerClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly CohereRerankerOptions _opts;

    public CohereRerankerClient(HttpClient http, CohereRerankerOptions opts)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(opts);
        _http = http;
        _http.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
        // API key is transmitted as Bearer — never log this value.
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opts.ApiKey);
        _opts = opts;
    }

    internal async Task<IReadOnlyList<RerankResult>> RerankAsync(
        string model,
        string query,
        IReadOnlyList<RerankCandidate> candidates,
        int topK,
        CancellationToken ct)
    {
        var body = new
        {
            model,
            query,
            documents = candidates.Select(c => c.Text).ToArray(),
            top_n     = topK <= 0 ? candidates.Count : topK,
        };

        HttpResponseMessage resp = await _http.PostAsJsonAsync("v2/rerank", body, ct);
        resp.EnsureSuccessStatusCode();

        var raw = await resp.Content.ReadFromJsonAsync<CohereRerankResponse>(ct);
        if (raw?.Results is null) return [];

        return raw.Results.Select((r, newRank) => new RerankResult(
            Id:           candidates[r.Index].Id,
            Text:         candidates[r.Index].Text,
            Score:        r.RelevanceScore,
            OriginalRank: r.Index,
            NewRank:      newRank,
            Metadata:     candidates[r.Index].Metadata
        )).ToList();
    }

    public void Dispose() => _http.Dispose();

    // ── JSON contracts ──────────────────────────────────────────────────────

    private sealed class CohereRerankResponse
    {
        [JsonPropertyName("results")]
        public List<CohereResult>? Results { get; set; }
    }

    private sealed class CohereResult
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("relevance_score")]
        public float RelevanceScore { get; set; }
    }
}

/// <summary>Reranker using Cohere <c>rerank-english-v3.0</c>.</summary>
public sealed class CohereRerankEnglish : IReranker, IDisposable
{
    private const string ModelName = "rerank-english-v3.0";
    private readonly CohereRerankerClient _client;

    public RerankerType RerankerType => RerankerType.CohereEnglish;

    public CohereRerankEnglish(CohereRerankerOptions opts)
        : this(new HttpClient(), opts) { }

    public CohereRerankEnglish(HttpClient http, CohereRerankerOptions opts)
    {
        _client = new CohereRerankerClient(http, opts);
    }

    public Task<IReadOnlyList<RerankResult>> RerankAsync(
        string query,
        IReadOnlyList<RerankCandidate> candidates,
        int topK = 0,
        CancellationToken ct = default)
        => _client.RerankAsync(ModelName, query, candidates, topK, ct);

    public void Dispose() => _client.Dispose();
}

/// <summary>Reranker using Cohere <c>rerank-multilingual-v3.0</c>.</summary>
public sealed class CohereRerankMultilingual : IReranker, IDisposable
{
    private const string ModelName = "rerank-multilingual-v3.0";
    private readonly CohereRerankerClient _client;

    public RerankerType RerankerType => RerankerType.CohereMultilingual;

    public CohereRerankMultilingual(CohereRerankerOptions opts)
        : this(new HttpClient(), opts) { }

    public CohereRerankMultilingual(HttpClient http, CohereRerankerOptions opts)
    {
        _client = new CohereRerankerClient(http, opts);
    }

    public Task<IReadOnlyList<RerankResult>> RerankAsync(
        string query,
        IReadOnlyList<RerankCandidate> candidates,
        int topK = 0,
        CancellationToken ct = default)
        => _client.RerankAsync(ModelName, query, candidates, topK, ct);

    public void Dispose() => _client.Dispose();
}
