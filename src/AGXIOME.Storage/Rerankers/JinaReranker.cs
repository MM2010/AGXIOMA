using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage.Rerankers;

// ─────────────────────────────────────────────────────────────────────────────
// RK-03 — JinaReranker  (jina-reranker-v2-base-multilingual)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Configuration for the Jina AI Reranker HTTP API.</summary>
public record JinaRerankerOptions
{
    public required string ApiKey { get; init; }
    public string BaseUrl         { get; init; } = "https://api.jina.ai";
    public string Model           { get; init; } = "jina-reranker-v2-base-multilingual";
}

/// <summary>Reranker backed by the Jina AI rerank endpoint.</summary>
public sealed class JinaReranker : IReranker, IDisposable
{
    private readonly HttpClient _http;
    private readonly JinaRerankerOptions _opts;

    public RerankerType RerankerType => RerankerType.JinaV2;

    public JinaReranker(JinaRerankerOptions opts) : this(new HttpClient(), opts) { }

    public JinaReranker(HttpClient http, JinaRerankerOptions opts)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(opts);
        _http = http;
        _http.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opts.ApiKey);
        _opts = opts;
    }

    public async Task<IReadOnlyList<RerankResult>> RerankAsync(
        string query,
        IReadOnlyList<RerankCandidate> candidates,
        int topK = 0,
        CancellationToken ct = default)
    {
        var body = new
        {
            model     = _opts.Model,
            query,
            documents = candidates.Select(c => c.Text).ToArray(),
            top_n     = topK <= 0 ? candidates.Count : topK,
        };

        var resp = await _http.PostAsJsonAsync("v1/rerank", body, ct);
        resp.EnsureSuccessStatusCode();

        var raw = await resp.Content.ReadFromJsonAsync<JinaRerankResponse>(ct);
        if (raw?.Results is null) return [];

        // Trim to topK client-side as a defensive measure in case the API ignores top_n.
        var source = (topK > 0 && raw.Results.Count > topK)
            ? raw.Results.Take(topK)
            : raw.Results.AsEnumerable();

        return source.Select((r, newRank) => new RerankResult(
            Id:           candidates[r.Index].Id,
            Text:         candidates[r.Index].Text,
            Score:        r.RelevanceScore,
            OriginalRank: r.Index,
            NewRank:      newRank,
            Metadata:     candidates[r.Index].Metadata
        )).ToList();
    }

    public void Dispose() => _http.Dispose();

    private sealed class JinaRerankResponse
    {
        [JsonPropertyName("results")]
        public List<JinaResult>? Results { get; set; }
    }

    private sealed class JinaResult
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("relevance_score")]
        public float RelevanceScore { get; set; }
    }
}
