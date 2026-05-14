using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage.Rerankers;

// ─────────────────────────────────────────────────────────────────────────────
// RK-04 — VoyageReranker  (voyage-rerank-2)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Configuration for the Voyage AI Rerank API.</summary>
public record VoyageRerankerOptions
{
    public required string ApiKey { get; init; }
    public string BaseUrl         { get; init; } = "https://api.voyageai.com";
    public string Model           { get; init; } = "rerank-2";
}

/// <summary>Reranker backed by the Voyage AI rerank-2 endpoint.</summary>
public sealed class VoyageReranker : IReranker, IDisposable
{
    private readonly HttpClient _http;
    private readonly VoyageRerankerOptions _opts;

    public RerankerType RerankerType => RerankerType.VoyageRerank2;

    public VoyageReranker(VoyageRerankerOptions opts) : this(new HttpClient(), opts) { }

    public VoyageReranker(HttpClient http, VoyageRerankerOptions opts)
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
            top_k     = topK <= 0 ? candidates.Count : topK,
        };

        var resp = await _http.PostAsJsonAsync("v1/rerank", body, ct);
        resp.EnsureSuccessStatusCode();

        var raw = await resp.Content.ReadFromJsonAsync<VoyageRerankResponse>(ct);
        if (raw?.Data is null) return [];

        return raw.Data.Select((r, newRank) => new RerankResult(
            Id:           candidates[r.Index].Id,
            Text:         candidates[r.Index].Text,
            Score:        r.RelevanceScore,
            OriginalRank: r.Index,
            NewRank:      newRank,
            Metadata:     candidates[r.Index].Metadata
        )).ToList();
    }

    public void Dispose() => _http.Dispose();

    private sealed class VoyageRerankResponse
    {
        [JsonPropertyName("data")]
        public List<VoyageResult>? Data { get; set; }
    }

    private sealed class VoyageResult
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("relevance_score")]
        public float RelevanceScore { get; set; }
    }
}
