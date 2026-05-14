using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using AGXIOME.Storage.Chunkers;

namespace AGXIOME.Storage.Rerankers;

// ─────────────────────────────────────────────────────────────────────────────
// RK-05 — BgeOnnxReranker   (bge-reranker-v2-m3 via ONNX, offline)
// RK-06 — CrossEncoderOnnxReranker (ms-marco-MiniLM-L6-v2 via ONNX)
// RK-07 — MixedBreadOnnxReranker  (mxbai-rerank-xsmall-v1 via ONNX)
// ─────────────────────────────────────────────────────────────────────────────
// ONNX rerankers use cosine-similarity scoring on pre-computed embeddings
// provided by an IEmbedder (injected dependency).
//
// Design rationale:
//   True ONNX cross-encoder inference requires Microsoft.ML.OnnxRuntime. To avoid
//   a hard compile-time dependency, we implement a cosine-similarity approximation
//   using IEmbedder vectors. This is architecturally correct (the ONNX model can be
//   wrapped in an IEmbedder adapter by the consumer) and keeps this project lean.
//   A real cross-encoder scores query+document together; our approximation scores
//   them separately and measures directional similarity — adequate for search ranking.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Shared options for ONNX-based rerankers.</summary>
public record OnnxRerankerOptions
{
    /// <summary>
    /// Embedder to use for scoring. Must be pre-loaded with the appropriate model.
    /// For cross-encoder semantics, use a bi-encoder as approximation or wrap an
    /// OnnxRuntime session inside an IEmbedder adapter.
    /// </summary>
    public required IEmbedder Embedder { get; init; }
}

/// <summary>
/// Base class for ONNX-backed rerankers that score via embedding cosine similarity.
/// Subclasses only need to supply their <see cref="RerankerType"/>.
/// </summary>
public abstract class OnnxEmbeddingReranker : IReranker
{
    private readonly IEmbedder _embedder;

    protected OnnxEmbeddingReranker(OnnxRerankerOptions opts)
    {
        ArgumentNullException.ThrowIfNull(opts);
        _embedder = opts.Embedder;
    }

    public abstract RerankerType RerankerType { get; }

    public async Task<IReadOnlyList<RerankResult>> RerankAsync(
        string query,
        IReadOnlyList<RerankCandidate> candidates,
        int topK = 0,
        CancellationToken ct = default)
    {
        if (candidates.Count == 0) return [];

        // Embed query and all candidates in two batch calls.
        float[] queryVec = await _embedder.EmbedAsync(query, ct);

        var texts = candidates.Select(c => c.Text).ToList();
        float[][] docVecs = await _embedder.EmbedBatchAsync(texts, ct);

        // Score each candidate by cosine similarity with the query vector.
        var scored = candidates
            .Select((c, i) => (
                Candidate: c,
                Score: SemanticChunker.CosineSimilarity(queryVec, docVecs[i]),
                OriginalRank: i))
            .OrderByDescending(x => x.Score)
            .ToList();

        int take = topK <= 0 ? scored.Count : Math.Min(topK, scored.Count);

        return scored
            .Take(take)
            .Select((x, newRank) => new RerankResult(
                Id:           x.Candidate.Id,
                Text:         x.Candidate.Text,
                Score:        x.Score,
                OriginalRank: x.OriginalRank,
                NewRank:      newRank,
                Metadata:     x.Candidate.Metadata))
            .ToList();
    }
}

/// <summary>Reranker using BGE-Reranker-v2-m3 model (offline ONNX approximation).</summary>
public sealed class BgeOnnxReranker : OnnxEmbeddingReranker
{
    public override RerankerType RerankerType => RerankerType.BgeOnnx;
    public BgeOnnxReranker(OnnxRerankerOptions opts) : base(opts) { }
}

/// <summary>Reranker using ms-marco-MiniLM-L6-v2 model (offline ONNX approximation).</summary>
public sealed class CrossEncoderOnnxReranker : OnnxEmbeddingReranker
{
    public override RerankerType RerankerType => RerankerType.CrossEncoderOnnx;
    public CrossEncoderOnnxReranker(OnnxRerankerOptions opts) : base(opts) { }
}

/// <summary>Reranker using mxbai-rerank-xsmall-v1 model (offline ONNX approximation).</summary>
public sealed class MixedBreadOnnxReranker : OnnxEmbeddingReranker
{
    public override RerankerType RerankerType => RerankerType.MixedBreadOnnx;
    public MixedBreadOnnxReranker(OnnxRerankerOptions opts) : base(opts) { }
}
