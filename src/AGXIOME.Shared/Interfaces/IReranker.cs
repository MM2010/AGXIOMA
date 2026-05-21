using AGXIOME.Shared.Models;

namespace AGXIOME.Shared.Interfaces;

/// <summary>A single candidate document/passage to rerank.</summary>
public record RerankCandidate(
    string Id,
    string Text,
    float InitialScore = 0f,
    Dictionary<string, object>? Metadata = null
);

/// <summary>Reranked result with its new score and original rank.</summary>
public record RerankResult(
    string Id,
    string Text,
    float Score,
    int OriginalRank,
    int NewRank,
    Dictionary<string, object>? Metadata = null
);

/// <summary>Reranker contract. Top-K is enforced by the implementation; 0 = return all.</summary>
public interface IReranker
{
    RerankerType RerankerType { get; }

    Task<IReadOnlyList<RerankResult>> RerankAsync(
        string query,
        IReadOnlyList<RerankCandidate> candidates,
        int topK = 0,
        CancellationToken ct = default);
}
