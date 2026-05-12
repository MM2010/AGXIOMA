using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Core.Storage;

// ─────────────────────────────────────────────────────────────────────────────
// SS-08 — Team Default Tools
//
// Manages the set of tools that are automatically assigned to every agent in a
// team unless explicitly overridden at the agent level.
//
// Design:
//   • TeamToolPolicy — record that captures per-team tool defaults + override rules.
//   • DefaultToolEntry — one registered default tool (TypeName + Config + scope).
//   • ToolScope enum — specifies whether a tool is team-wide or topology-specific.
//   • ITeamToolPolicyStore — typed contract: set/get/clear/list default tools.
//   • TeamToolPolicyStore — in-memory backed by ICommandStore.
//
// Enum-first policy: ToolScope uses an enum — never a string like "global".
//
// Merging rule (resolved at agent build time, NOT stored here):
//   final tools = teamDefaults (filtered by scope) ∪ agentOverrides
//   where agentOverrides.ExcludeDefaults = true means team tools are stripped.
// ─────────────────────────────────────────────────────────────────────────────

// ── Enums ─────────────────────────────────────────────────────────────────────

/// <summary>Scope at which a default tool applies within a team.</summary>
public enum ToolScope
{
    /// <summary>Tool is injected into every agent in the team regardless of topology.</summary>
    AllAgents,

    /// <summary>Tool is injected only into the supervisor/coordinator agent.</summary>
    SupervisorOnly,

    /// <summary>Tool is injected only into leaf/worker agents.</summary>
    WorkersOnly,
}

// ── Records ───────────────────────────────────────────────────────────────────

/// <summary>A single default tool entry bound to a team.</summary>
public record DefaultToolEntry
{
    public string TypeName { get; init; } = string.Empty;
    public ToolScope Scope { get; init; } = ToolScope.AllAgents;
    public Dictionary<string, object>? Config { get; init; }
}

/// <summary>Full tool policy for a team (default tools + exclusion rules).</summary>
public record TeamToolPolicy
{
    public string TeamId { get; init; } = string.Empty;

    /// <summary>Ordered list of default tools injected into matching agents.</summary>
    public IReadOnlyList<DefaultToolEntry> DefaultTools { get; init; }
        = Array.Empty<DefaultToolEntry>();

    /// <summary>
    /// When true, individual agents in this team may NOT add extra tools beyond the defaults.
    /// Useful for locked-down governed teams.
    /// </summary>
    public bool LockAgentTools { get; init; } = false;

    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
    public string SchemaVersion { get; init; } = "1";
}

// ── Interface ─────────────────────────────────────────────────────────────────

/// <summary>Typed storage contract for per-team default tool policies.</summary>
public interface ITeamToolPolicyStore
{
    /// <summary>Persist (upsert) a tool policy for a team.</summary>
    Task SetPolicyAsync(TeamToolPolicy policy, CancellationToken ct = default);

    /// <summary>Retrieve the tool policy for a team. Returns null if not set.</summary>
    Task<TeamToolPolicy?> GetPolicyAsync(string teamId, CancellationToken ct = default);

    /// <summary>Remove the tool policy for a team. No-op if not set.</summary>
    Task ClearPolicyAsync(string teamId, CancellationToken ct = default);

    /// <summary>List all stored policies.</summary>
    Task<IReadOnlyList<TeamToolPolicy>> ListPoliciesAsync(CancellationToken ct = default);

    /// <summary>
    /// Resolve the effective tool list for a given scope within a team.
    /// Returns empty if no policy is set.
    /// </summary>
    Task<IReadOnlyList<DefaultToolEntry>> GetEffectiveToolsAsync(
        string teamId, ToolScope scope, CancellationToken ct = default);
}

// ── Implementation ────────────────────────────────────────────────────────────

/// <summary>
/// Team tool policy store backed by <see cref="ICommandStore"/> + in-memory index.
/// Suitable for single-process deployments.
/// </summary>
public sealed class TeamToolPolicyStore : ITeamToolPolicyStore
{
    private readonly ICommandStore _commands;
    private readonly Dictionary<string, TeamToolPolicy> _index = new();

    public TeamToolPolicyStore(ICommandStore commandStore)
    {
        ArgumentNullException.ThrowIfNull(commandStore);
        _commands = commandStore;
    }

    public async Task SetPolicyAsync(TeamToolPolicy policy, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentException.ThrowIfNullOrWhiteSpace(policy.TeamId);

        if (_index.ContainsKey(policy.TeamId))
            await _commands.UpdateAsync(policy, ct);
        else
            await _commands.SaveAsync(policy, ct);

        _index[policy.TeamId] = policy;
    }

    public Task<TeamToolPolicy?> GetPolicyAsync(string teamId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(teamId);
        _index.TryGetValue(teamId, out var policy);
        return Task.FromResult(policy);
    }

    public async Task ClearPolicyAsync(string teamId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(teamId);
        await _commands.DeleteAsync<TeamToolPolicy>(teamId, ct);
        _index.Remove(teamId);
    }

    public Task<IReadOnlyList<TeamToolPolicy>> ListPoliciesAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TeamToolPolicy>>(_index.Values.ToList());

    public Task<IReadOnlyList<DefaultToolEntry>> GetEffectiveToolsAsync(
        string teamId, ToolScope scope, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(teamId);

        if (!_index.TryGetValue(teamId, out var policy))
            return Task.FromResult<IReadOnlyList<DefaultToolEntry>>(Array.Empty<DefaultToolEntry>());

        // AllAgents entries apply to every scope; scoped entries apply only to their scope.
        var tools = policy.DefaultTools
            .Where(t => t.Scope == ToolScope.AllAgents || t.Scope == scope)
            .ToList();

        return Task.FromResult<IReadOnlyList<DefaultToolEntry>>(tools);
    }
}
