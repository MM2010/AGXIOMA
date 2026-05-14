using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Core.Storage;

// ─────────────────────────────────────────────────────────────────────────────
// SS-09 — Callable Members
//
// Provides runtime resolution and registry of callable team members (agents,
// tools, skills) by name/ID within a session or team scope.
//
// Design:
//   • CallableMemberType — enum identifying what kind of callable is registered.
//   • CallableMemberDescriptor — metadata-only record (no handler closures).
//   • ICallableMemberRegistry — typed contract:
//       RegisterAsync — add a callable descriptor to a scope
//       ResolveAsync  — lookup by name within a scope (returns null if not found)
//       ListAsync     — all callables in a scope, optionally filtered by type
//       UnregisterAsync — remove a callable from a scope
//   • CallableMemberRegistry — in-memory implementation (per-process lifetime).
//     For distributed use project registrations to Redis or a DB table.
//
// Enum-first policy: CallableMemberType enum — never "agent" / "tool" strings.
// ─────────────────────────────────────────────────────────────────────────────

// ── Enums ─────────────────────────────────────────────────────────────────────

/// <summary>Identifies what kind of callable is being registered.</summary>
public enum CallableMemberType
{
    Agent,
    Tool,
    Skill,
    TeamProxy,  // a sub-team exposed as a single callable
}

// ── Records ───────────────────────────────────────────────────────────────────

/// <summary>Metadata descriptor for a registered callable member.</summary>
public record CallableMemberDescriptor
{
    /// <summary>Unique name within the registration scope (used for lookup).</summary>
    public string Name          { get; init; } = string.Empty;

    /// <summary>The kind of callable — enum-first.</summary>
    public CallableMemberType Type { get; init; }

    /// <summary>
    /// Canonical ID of the underlying entity (agentId, toolTypeName, skillId, teamId).
    /// Used by the runtime to hydrate the actual instance.
    /// </summary>
    public string EntityId      { get; init; } = string.Empty;

    public string? Description  { get; init; }

    /// <summary>Optional role/label within the team (e.g. "Researcher", "Executor").</summary>
    public string? Role         { get; init; }

    public Dictionary<string, string>? Metadata { get; init; }

    public DateTimeOffset RegisteredAt { get; init; } = DateTimeOffset.UtcNow;
}

// ── Interface ─────────────────────────────────────────────────────────────────

/// <summary>Registry for runtime-resolvable callable members within a scope.</summary>
public interface ICallableMemberRegistry
{
    /// <summary>Register a callable descriptor in a scope. Replaces any existing entry with the same name.</summary>
    Task RegisterAsync(string scopeId, CallableMemberDescriptor descriptor, CancellationToken ct = default);

    /// <summary>
    /// Resolve a callable by name within a scope.
    /// Returns null if not found.
    /// </summary>
    Task<CallableMemberDescriptor?> ResolveAsync(
        string scopeId, string name, CancellationToken ct = default);

    /// <summary>List all callable members in a scope, optionally filtered by type.</summary>
    Task<IReadOnlyList<CallableMemberDescriptor>> ListAsync(
        string scopeId, CallableMemberType? type = null, CancellationToken ct = default);

    /// <summary>Remove a callable from a scope. No-op if not registered.</summary>
    Task UnregisterAsync(string scopeId, string name, CancellationToken ct = default);

    /// <summary>Remove all callables for a scope (e.g. on session teardown).</summary>
    Task ClearScopeAsync(string scopeId, CancellationToken ct = default);
}

// ── Implementation ────────────────────────────────────────────────────────────

/// <summary>
/// In-memory callable member registry. Suitable for single-process/single-node use.
/// For multi-process deployments project to a Redis hash per scope.
/// </summary>
public sealed class CallableMemberRegistry : ICallableMemberRegistry
{
    // scopeId → (memberName → descriptor)
    private readonly Dictionary<string, Dictionary<string, CallableMemberDescriptor>> _scopes = new();

    public Task RegisterAsync(string scopeId, CallableMemberDescriptor descriptor, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeId);
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.Name);

        if (!_scopes.TryGetValue(scopeId, out var scope))
        {
            scope = new Dictionary<string, CallableMemberDescriptor>();
            _scopes[scopeId] = scope;
        }
        scope[descriptor.Name] = descriptor;

        return Task.CompletedTask;
    }

    public Task<CallableMemberDescriptor?> ResolveAsync(
        string scopeId, string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        CallableMemberDescriptor? result = null;
        if (_scopes.TryGetValue(scopeId, out var scope))
            scope.TryGetValue(name, out result);

        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<CallableMemberDescriptor>> ListAsync(
        string scopeId, CallableMemberType? type = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeId);

        if (!_scopes.TryGetValue(scopeId, out var scope))
            return Task.FromResult<IReadOnlyList<CallableMemberDescriptor>>(Array.Empty<CallableMemberDescriptor>());

        IEnumerable<CallableMemberDescriptor> items = scope.Values;
        if (type.HasValue)
            items = items.Where(d => d.Type == type.Value);

        return Task.FromResult<IReadOnlyList<CallableMemberDescriptor>>(items.ToList());
    }

    public Task UnregisterAsync(string scopeId, string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (_scopes.TryGetValue(scopeId, out var scope))
            scope.Remove(name);

        return Task.CompletedTask;
    }

    public Task ClearScopeAsync(string scopeId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeId);
        _scopes.Remove(scopeId);
        return Task.CompletedTask;
    }
}
