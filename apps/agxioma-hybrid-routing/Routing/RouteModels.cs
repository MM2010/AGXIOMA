namespace AgxiomaHybridRouting.Routing;

// ─── SemanticRouteProfile ─────────────────────────────────────────────────────

/// <summary>
/// Pre-computed semantic profile for one agent entry.
/// Embeddings are calculated once at bootstrap and cached here.
/// </summary>
public sealed class SemanticRouteProfile
{
    public string TreeName   { get; init; } = "";
    public string AgentName  { get; init; } = "";
    public string Description { get; init; } = "";
    public IReadOnlyList<string> Keywords { get; init; } = [];
    public string ProfileText { get; init; } = "";

    /// <summary>
    /// Embedding vector (null if Ollama unavailable — falls back to keyword-only).
    /// </summary>
    public float[]? Embedding { get; init; }

    /// <summary>Optional priority boost in [0, 1]. Default 0.</summary>
    public double Priority { get; init; } = 0.0;
}

// ─── RouteCandidate ───────────────────────────────────────────────────────────

/// <summary>
/// Full diagnostic snapshot for one routing candidate.
/// Mutable: reranker adjusts RerankAdjustment and FinalScore in place.
/// </summary>
public class RouteCandidate
{
    public string TreeName  { get; set; } = "";
    public string AgentName { get; set; } = "";

    /// <summary>Cosine-similarity score (0–1) against request embedding.</summary>
    public double SemanticScore { get; set; }

    /// <summary>Normalised keyword-overlap score (0–1).</summary>
    public double KeywordScore { get; set; }

    /// <summary>Priority bias configured per agent.</summary>
    public double PriorityBias { get; set; }

    /// <summary>Weighted score before reranking.</summary>
    public double InitialScore { get; set; }

    /// <summary>Cumulative delta applied by the deterministic reranker (can be negative).</summary>
    public double RerankAdjustment { get; set; }

    /// <summary>Final score after reranking (InitialScore + RerankAdjustment, clamped 0–1).</summary>
    public double FinalScore { get; set; }

    public List<string> MatchedKeywords { get; set; } = [];
    public string Explanation { get; set; } = "";
}

// ─── RouteDecision ────────────────────────────────────────────────────────────

/// <summary>
/// Complete output of the hybrid router for one request.
/// </summary>
public sealed class RouteDecision
{
    public string SelectedTree  { get; init; } = "";
    public string SelectedAgent { get; init; } = "";
    public double SelectedScore { get; init; }

    public ConfidenceLevel Confidence { get; init; }
    public bool IsAmbiguous { get; init; }

    /// <summary>Top-N candidates with full score breakdown.</summary>
    public IReadOnlyList<RouteCandidate> Candidates { get; init; } = [];

    /// <summary>True when Ollama was unavailable and routing fell back to keyword-only.</summary>
    public bool EmbeddingFallback { get; init; }

    /// <summary>True when embeddings were successfully used.</summary>
    public bool EmbeddingUsed { get; init; }
}
