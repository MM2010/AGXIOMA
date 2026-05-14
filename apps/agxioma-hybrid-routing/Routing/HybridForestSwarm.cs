using AGXIOME.Shared.Models;
using AgxiomaHybridRouting.AgentServices;
using Microsoft.Extensions.Logging;

namespace AgxiomaHybridRouting.Routing;

/// <summary>
/// Drop-in replacement for ForestSwarm that adds hybrid semantic routing.
/// Wraps a list of <see cref="HybridAgentEntry"/> objects and a
/// <see cref="HybridSemanticRouter"/> to score and dispatch requests.
/// </summary>
public sealed class HybridForestSwarm
{
    private readonly HybridForestSwarmConfig _config;
    private readonly IReadOnlyList<HybridAgentEntry> _entries;
    private readonly HybridSemanticRouter _router;
    private readonly ILogger<HybridForestSwarm> _logger;

    public string Name        { get; }
    public string Description { get; }

    public HybridForestSwarm(
        HybridForestSwarmConfig config,
        IReadOnlyList<HybridAgentEntry> entries,
        HybridSemanticRouter router,
        ILogger<HybridForestSwarm> logger,
        string name = "hybrid-forest-swarm",
        string description = "Multi-tree swarm with hybrid semantic routing")
    {
        _config      = config;
        _entries     = entries;
        _router      = router;
        _logger      = logger;
        Name         = name;
        Description  = description;
    }

    /// <summary>
    /// Pre-compute embeddings for all agent profiles.
    /// Call once during application startup.
    /// </summary>
    public Task InitializeAsync(CancellationToken ct = default)
        => _router.InitializeAsync(_entries, ct);

    /// <summary>
    /// Route <paramref name="message"/> using hybrid scoring, then invoke the selected agent.
    /// </summary>
    public async Task<HybridSwarmResult> RunAsync(
        string message,
        IReadOnlyList<ConversationMessage>? history = null,
        CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var decision = await _router.RouteAsync(message, history, ct);

        _logger.LogInformation(
            "Routed '{Preview}...' → tree={Tree}, agent={Agent}, score={Score:F3}, confidence={Conf}",
            message.Length > 40 ? message[..40] : message,
            decision.SelectedTree, decision.SelectedAgent,
            decision.SelectedScore, decision.Confidence);

        // Find the selected agent entry
        var entry = _entries.FirstOrDefault(e =>
            e.TreeName.Equals(decision.SelectedTree, StringComparison.OrdinalIgnoreCase) &&
            e.AgentName.Equals(decision.SelectedAgent, StringComparison.OrdinalIgnoreCase))
            ?? _entries[0];   // safety fallback

        var agentResponse = await entry.Agent.RunAsync(new AgentRequest(message), ct);

        sw.Stop();
        return new HybridSwarmResult(
            OriginalMessage : message,
            SelectedTree    : decision.SelectedTree,
            SelectedAgent   : decision.SelectedAgent,
            SelectedScore   : decision.SelectedScore,
            Output          : agentResponse.Content,
            Duration        : sw.Elapsed,
            RoutingDecision : decision);
    }

    /// <summary>
    /// Route-only (no LLM call). Useful for fast diagnostic endpoints.
    /// </summary>
    public Task<RouteDecision> DiagnoseAsync(
        string message,
        IReadOnlyList<ConversationMessage>? history = null,
        CancellationToken ct = default)
        => _router.RouteAsync(message, history, ct);

    /// <summary>
    /// Returns the pre-computed semantic profiles (useful for /routing-profiles endpoint).
    /// </summary>
    public IReadOnlyList<SemanticRouteProfile> GetProfiles()
        => _router.Profiles;
}

// ─── Result ───────────────────────────────────────────────────────────────────

public sealed record HybridSwarmResult(
    string OriginalMessage,
    string SelectedTree,
    string SelectedAgent,
    double SelectedScore,
    string Output,
    TimeSpan Duration,
    RouteDecision RoutingDecision);
