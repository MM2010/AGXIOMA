using AgxiomaHybridRouting.AgentServices;

namespace AgxiomaHybridRouting.Routing;

/// <summary>
/// Post-processes the initial ranked candidate list with deterministic rules
/// that model domain-specific routing signals.
/// All adjustments are additive deltas on top of the initial score.
/// </summary>
public interface IRouteReranker
{
    IReadOnlyList<RouteCandidate> Rerank(
        string request,
        IReadOnlyList<RouteCandidate> candidates,
        IReadOnlyList<ConversationMessage>? history = null);
}
