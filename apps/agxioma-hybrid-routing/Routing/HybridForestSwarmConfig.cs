namespace AgxiomaHybridRouting.Routing;

// ─── Routing mode ──────────────────────────────────────────────────────────────

public enum RoutingMode
{
    /// <summary>Classic keyword-overlap scoring only (original ForestSwarm behaviour).</summary>
    Keyword,
    /// <summary>Cosine-similarity on embeddings only.</summary>
    Semantic,
    /// <summary>Weighted blend of semantic + keyword + deterministic reranking.</summary>
    Hybrid
}

// ─── Confidence levels ─────────────────────────────────────────────────────────

public enum ConfidenceLevel { High, Medium, Low }

// ─── Main configuration ────────────────────────────────────────────────────────

public class HybridForestSwarmConfig
{
    /// <summary>Routing strategy. Default: Hybrid.</summary>
    public RoutingMode RoutingMode { get; init; } = RoutingMode.Hybrid;

    /// <summary>Weight for cosine-similarity score (0–1). Default 0.65.</summary>
    public double SemanticWeight { get; init; } = 0.65;

    /// <summary>Weight for keyword-overlap score (0–1). Default 0.25.</summary>
    public double KeywordWeight { get; init; } = 0.25;

    /// <summary>Weight for per-agent priority bias (0–1). Default 0.10.</summary>
    public double PriorityWeight { get; init; } = 0.10;

    /// <summary>Number of candidates to pass to the reranker. Default 5.</summary>
    public int TopCandidates { get; init; } = 5;

    /// <summary>Minimum final score to trust the top candidate. Below → fallback to general. Default 0.35.</summary>
    public double MinMatchScore { get; init; } = 0.35;

    /// <summary>Maximum score gap (top1 – top2) below which routing is considered ambiguous. Default 0.08.</summary>
    public double AmbiguityGap { get; init; } = 0.08;

    /// <summary>Enable rule-based score adjustments after initial ranking. Default true.</summary>
    public bool EnableDeterministicRerank { get; init; } = true;

    /// <summary>Include top-5 candidate diagnostics in the response. Default true.</summary>
    public bool ReturnRoutingDiagnostics { get; init; } = true;
}
