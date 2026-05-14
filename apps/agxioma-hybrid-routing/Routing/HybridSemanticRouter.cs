using AGXIOME.Shared.Interfaces;
using AgxiomaHybridRouting.AgentServices;
using Microsoft.Extensions.Logging;

namespace AgxiomaHybridRouting.Routing;

// ─── HybridAgentEntry ─────────────────────────────────────────────────────────

/// <summary>
/// A single registerable agent with routing metadata.
/// </summary>
public sealed record HybridAgentEntry(
    string TreeName,
    string AgentName,
    string Description,
    string[] Keywords,
    IAgxiomeAgent Agent,
    double Priority = 0.0);

// ─── HybridSemanticRouter ─────────────────────────────────────────────────────

/// <summary>
/// Core routing engine.
/// Computes hybrid score = keyword*(kw) + semantic*(sem) + priority*(pri)
/// then optionally applies deterministic reranking rules.
/// Falls back to keyword-only when Ollama embeddings are unavailable.
/// </summary>
public sealed class HybridSemanticRouter
{
    private readonly HybridForestSwarmConfig _config;
    private readonly IRouteEmbedder _embedder;
    private readonly IRouteReranker _reranker;
    private readonly ILogger<HybridSemanticRouter> _logger;

    private IReadOnlyList<SemanticRouteProfile> _profiles = [];

    public IReadOnlyList<SemanticRouteProfile> Profiles => _profiles;

    public HybridSemanticRouter(
        HybridForestSwarmConfig config,
        IRouteEmbedder embedder,
        IRouteReranker reranker,
        ILogger<HybridSemanticRouter> logger)
    {
        _config   = config;
        _embedder = embedder;
        _reranker = reranker;
        _logger   = logger;
    }

    /// <summary>
    /// Build a <see cref="SemanticRouteProfile"/> for each agent and pre-compute embeddings.
    /// Call once at startup.
    /// </summary>
    public async Task InitializeAsync(
        IReadOnlyList<HybridAgentEntry> entries,
        CancellationToken ct = default)
    {
        var profiles = new List<SemanticRouteProfile>(entries.Count);

        foreach (var entry in entries)
        {
            var profileText = BuildProfileText(entry);
            float[]? embedding = null;

            if (_config.RoutingMode != RoutingMode.Keyword)
            {
                embedding = await _embedder.EmbedAsync(profileText, ct);
                if (embedding is null)
                    _logger.LogWarning(
                        "Embedding unavailable for agent '{Agent}' (tree: {Tree}). Keyword scoring only.",
                        entry.AgentName, entry.TreeName);
            }

            profiles.Add(new SemanticRouteProfile
            {
                TreeName    = entry.TreeName,
                AgentName   = entry.AgentName,
                Description = entry.Description,
                Keywords    = entry.Keywords.ToList().AsReadOnly(),
                ProfileText = profileText,
                Embedding   = embedding,
                Priority    = entry.Priority
            });
        }

        _profiles = profiles.AsReadOnly();
        _logger.LogInformation(
            "HybridSemanticRouter initialised with {Count} profiles. Embeddings available: {Avail}",
            _profiles.Count, _embedder.IsAvailable);
    }

    /// <summary>
    /// Route <paramref name="request"/> to the best agent.
    /// </summary>
    public async Task<RouteDecision> RouteAsync(
        string request,
        IReadOnlyList<ConversationMessage>? history = null,
        CancellationToken ct = default)
    {
        if (_profiles.Count == 0)
            throw new InvalidOperationException("Router not initialised — call InitializeAsync first.");

        var normalised = NormaliseRequest(request);

        // Obtain request embedding (may be null → keyword-only fallback)
        float[]? requestEmbedding = null;
        bool embeddingFallback    = false;
        bool embeddingUsed        = false;

        if (_config.RoutingMode != RoutingMode.Keyword && _embedder.IsAvailable)
        {
            requestEmbedding = await _embedder.EmbedAsync(normalised, ct);
            if (requestEmbedding is null)
                embeddingFallback = true;
            else
                embeddingUsed = true;
        }

        // ── Score all profiles ────────────────────────────────────────────────
        var candidates = new List<RouteCandidate>(_profiles.Count);

        foreach (var profile in _profiles)
        {
            var (keywordScore, matchedKw) = ComputeKeywordScore(normalised, profile.Keywords);
            double semanticScore = 0.0;

            if (requestEmbedding is not null && profile.Embedding is not null)
                semanticScore = CosineSimilarity(requestEmbedding, profile.Embedding);

            double initialScore = _config.RoutingMode switch
            {
                RoutingMode.Keyword  => keywordScore,
                RoutingMode.Semantic => semanticScore,
                _                    => semanticScore * _config.SemanticWeight
                                      + keywordScore  * _config.KeywordWeight
                                      + profile.Priority * _config.PriorityWeight
            };

            candidates.Add(new RouteCandidate
            {
                TreeName        = profile.TreeName,
                AgentName       = profile.AgentName,
                SemanticScore   = semanticScore,
                KeywordScore    = keywordScore,
                PriorityBias    = profile.Priority,
                InitialScore    = initialScore,
                FinalScore      = initialScore,
                MatchedKeywords = matchedKw,
                Explanation     = ""
            });
        }

        // ── Sort by initial score, keep top-N ────────────────────────────────
        candidates.Sort((a, b) => b.InitialScore.CompareTo(a.InitialScore));
        var topCandidates = candidates.Take(_config.TopCandidates).ToList();

        // ── Deterministic rerank ──────────────────────────────────────────────
        IReadOnlyList<RouteCandidate> ranked;
        if (_config.EnableDeterministicRerank)
            ranked = _reranker.Rerank(normalised, topCandidates, history);
        else
        {
            foreach (var c in topCandidates)
                c.FinalScore = c.InitialScore;
            ranked = topCandidates.AsReadOnly();
        }

        var winner     = ranked[0];
        double gap     = ranked.Count > 1 ? winner.FinalScore - ranked[1].FinalScore : 1.0;
        bool ambiguous = gap < _config.AmbiguityGap;

        var confidence = winner.FinalScore >= 0.75 && gap >= 0.12 ? ConfidenceLevel.High
                       : winner.FinalScore >= 0.55                 ? ConfidenceLevel.Medium
                                                                   : ConfidenceLevel.Low;

        return new RouteDecision
        {
            SelectedTree    = winner.TreeName,
            SelectedAgent   = winner.AgentName,
            SelectedScore   = winner.FinalScore,
            Confidence      = confidence,
            IsAmbiguous     = ambiguous,
            EmbeddingUsed   = embeddingUsed,
            EmbeddingFallback = embeddingFallback,
            Candidates      = _config.ReturnRoutingDiagnostics ? ranked : Array.Empty<RouteCandidate>()
        };
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private static string BuildProfileText(HybridAgentEntry entry)
        => $"{entry.TreeName} {entry.AgentName} {entry.Description} {string.Join(' ', entry.Keywords)}";

    private static string NormaliseRequest(string request)
        => request.Trim().ToLowerInvariant();

    /// <summary>
    /// Keyword overlap score = matchedCount / Min(5, totalKeywords), clamped [0, 1].
    /// </summary>
    private static (double Score, List<string> Matched) ComputeKeywordScore(
        string normalisedRequest,
        IReadOnlyList<string> keywords)
    {
        var matched = new List<string>();

        foreach (var kw in keywords)
        {
            if (normalisedRequest.Contains(kw.ToLowerInvariant()))
                matched.Add(kw);
        }

        if (keywords.Count == 0) return (0.0, matched);

        double score = (double)matched.Count / Math.Min(5, keywords.Count);
        return (Math.Min(score, 1.0), matched);
    }

    /// <summary>
    /// Cosine similarity between two float vectors, result clamped to [0, 1].
    /// </summary>
    public static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0) return 0.0;

        double dot  = 0.0;
        double normA = 0.0;
        double normB = 0.0;

        for (int i = 0; i < a.Length; i++)
        {
            dot  += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        double denom = Math.Sqrt(normA) * Math.Sqrt(normB);
        if (denom < 1e-10) return 0.0;

        return Math.Clamp(dot / denom, 0.0, 1.0);
    }
}
