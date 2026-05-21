namespace AGXIOME.Shared.Models;

// ─────────────────────────────────────────────────────────────────────────────
// Reranker types — 8 rerankers
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Typed reranker discriminator covering cross-encoder and API-based reranking models.</summary>
public enum RerankerType
{
    CohereEnglish = 0,
    CohereMultilingual = 1,
    JinaV2 = 2,
    VoyageRerank2 = 3,
    BgeOnnx = 4,
    CrossEncoderOnnx = 5,
    MixedBreadOnnx = 6,
    LLMReranker = 7,
}
