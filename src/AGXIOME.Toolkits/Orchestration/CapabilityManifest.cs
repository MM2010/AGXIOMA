// CO-A2 — CapabilityManifest
// Per-toolkit descriptor used by CapabilityRegistry, ToolRouter, and SemanticToolSelector.
// Immutable value object — built once by the registry and cached.

namespace AGXIOME.Toolkits.Orchestration;

/// <summary>
/// Immutable descriptor for a single registered toolkit capability.
/// Produced by <see cref="CapabilityRegistry"/> and consumed by routers, selectors, and UI.
/// </summary>
/// <param name="Id">Unique toolkit identifier (e.g. "sqlite_query").</param>
/// <param name="Name">Human-readable display name (e.g. "SQLite").</param>
/// <param name="Category">Capability category used for routing and filtering.</param>
/// <param name="IsConfigured">Whether the toolkit can execute without additional setup.</param>
/// <param name="SupportedActions">
/// Optional list of actions the toolkit declares (opt-in via <see cref="ISupportedActionsProvider"/>).
/// Empty when the toolkit does not advertise its actions.
/// </param>
public sealed record CapabilityManifest(
    string              Id,
    string              Name,
    ToolkitCategory     Category,
    bool                IsConfigured,
    IReadOnlyList<string> SupportedActions);

/// <summary>
/// Optional interface toolkits can implement to advertise their supported action names.
/// Used by <see cref="CapabilityRegistry"/> to build richer <see cref="CapabilityManifest"/> entries
/// and by <see cref="SemanticToolSelector"/> to improve embedding quality.
/// </summary>
public interface ISupportedActionsProvider
{
    IReadOnlyList<string> SupportedActions { get; }
}
