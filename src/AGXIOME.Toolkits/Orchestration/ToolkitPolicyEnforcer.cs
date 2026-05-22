// CO-A6 — ToolkitPolicyEnforcer
// Enforces RBAC-style access control over toolkit execution.
// Defines a lightweight IToolkitPolicyProvider contract (no Storage dependency)
// so the policy can be backed by TeamToolPolicyStore or any other source at composition time.

namespace AGXIOME.Toolkits.Orchestration;

// ── Policy Model ──────────────────────────────────────────────────────────────

/// <summary>
/// Defines which toolkits an agent or team is permitted to use.
/// </summary>
public sealed record ToolkitPolicy
{
    /// <summary>Identifier of the agent or team this policy applies to.</summary>
    public string SubjectId { get; init; } = string.Empty;

    /// <summary>
    /// Explicit allowlist of toolkit IDs. When non-empty, only listed toolkits are permitted.
    /// Takes priority over <see cref="AllowedCategories"/>.
    /// </summary>
    public IReadOnlyList<string> AllowedToolkitIds { get; init; } = [];

    /// <summary>
    /// When non-empty, only toolkits in these categories are permitted.
    /// Ignored when <see cref="AllowedToolkitIds"/> is non-empty.
    /// </summary>
    public IReadOnlyList<ToolkitCategory> AllowedCategories { get; init; } = [];

    /// <summary>Explicit blocklist of toolkit IDs. Applied after allowlist/category checks.</summary>
    public IReadOnlyList<string> BlockedToolkitIds { get; init; } = [];

    /// <summary>
    /// Maximum calls per minute across ALL toolkits for this subject.
    /// <c>0</c> = unlimited.
    /// </summary>
    public int MaxCallsPerMinute { get; init; } = 0;

    /// <summary>
    /// When <c>true</c>, all toolkits are blocked by default (deny-all, then allow by list).
    /// When <c>false</c>, all toolkits are allowed by default (allow-all, then block by list).
    /// </summary>
    public bool DefaultDeny { get; init; } = false;

    public static readonly ToolkitPolicy AllowAll = new();
}

// ── Provider Contract ─────────────────────────────────────────────────────────

/// <summary>
/// Provides <see cref="ToolkitPolicy"/> for a given subject (agent/team ID).
/// Implement this interface to back the enforcer with any policy storage
/// (e.g. <c>TeamToolPolicyStore</c>, in-memory map, database).
/// </summary>
public interface IToolkitPolicyProvider
{
    /// <summary>Return the policy for a subject. Returns <see cref="ToolkitPolicy.AllowAll"/> when no policy is set.</summary>
    Task<ToolkitPolicy> GetPolicyAsync(string subjectId, CancellationToken ct = default);
}

/// <summary>Default implementation: no policy configured → allow everything.</summary>
public sealed class AllowAllPolicyProvider : IToolkitPolicyProvider
{
    public static readonly AllowAllPolicyProvider Instance = new();
    public Task<ToolkitPolicy> GetPolicyAsync(string subjectId, CancellationToken ct = default)
        => Task.FromResult(ToolkitPolicy.AllowAll);
}

/// <summary>In-memory policy map — useful for tests and single-process scenarios.</summary>
public sealed class InMemoryPolicyProvider : IToolkitPolicyProvider
{
    private readonly Dictionary<string, ToolkitPolicy> _policies = new(StringComparer.OrdinalIgnoreCase);

    public InMemoryPolicyProvider Set(string subjectId, ToolkitPolicy policy)
    {
        _policies[subjectId] = policy;
        return this;
    }

    public Task<ToolkitPolicy> GetPolicyAsync(string subjectId, CancellationToken ct = default)
        => Task.FromResult(
            _policies.TryGetValue(subjectId, out var p) ? p : ToolkitPolicy.AllowAll);
}

// ── Enforcer ──────────────────────────────────────────────────────────────────

/// <summary>
/// Gates toolkit execution through <see cref="ToolkitPolicy"/> rules.
///
/// <para>
/// Resolution order:
/// <list type="number">
///   <item><description>Blocklist check — immediately denies blocked IDs.</description></item>
///   <item><description>Allowlist check — if non-empty, toolkit ID must be listed.</description></item>
///   <item><description>Category check — if allowlist empty but categories set, category must match.</description></item>
///   <item><description>Default — allow (or deny when <see cref="ToolkitPolicy.DefaultDeny"/> is set).</description></item>
/// </list>
/// </para>
/// </summary>
public sealed class ToolkitPolicyEnforcer
{
    private readonly IToolkitPolicyProvider _policyProvider;

    public ToolkitPolicyEnforcer(IToolkitPolicyProvider policyProvider)
    {
        ArgumentNullException.ThrowIfNull(policyProvider);
        _policyProvider = policyProvider;
    }

    // ── Access Check ──────────────────────────────────────────────────────────

    /// <summary>
    /// Returns <c>true</c> if <paramref name="subjectId"/> is allowed to use
    /// the toolkit identified by <paramref name="toolkitId"/> / <paramref name="category"/>.
    /// </summary>
    public async Task<bool> IsAllowedAsync(
        string          subjectId,
        string          toolkitId,
        ToolkitCategory category,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolkitId);

        var policy = await _policyProvider.GetPolicyAsync(subjectId, ct).ConfigureAwait(false);

        // 1. Blocklist
        if (policy.BlockedToolkitIds.Contains(toolkitId, StringComparer.OrdinalIgnoreCase))
            return false;

        // 2. Allowlist (ID-based)
        if (policy.AllowedToolkitIds.Count > 0)
            return policy.AllowedToolkitIds.Contains(toolkitId, StringComparer.OrdinalIgnoreCase);

        // 3. Category allowlist
        if (policy.AllowedCategories.Count > 0)
            return policy.AllowedCategories.Contains(category);

        // 4. Default
        return !policy.DefaultDeny;
    }

    // ── Enforce + Execute ─────────────────────────────────────────────────────

    /// <summary>
    /// Check policy and, if allowed, execute the toolkit action.
    /// Returns <see cref="ToolkitResultStatus.Unauthorized"/> when the policy blocks access.
    /// </summary>
    public async Task<ToolkitResult> EnforceAndExecuteAsync(
        IToolkit                  toolkit,
        string                    action,
        Dictionary<string, object?> parameters,
        string                    subjectId,
        CancellationToken         ct = default)
    {
        ArgumentNullException.ThrowIfNull(toolkit);
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);

        if (!await IsAllowedAsync(subjectId, toolkit.Id, toolkit.Category, ct).ConfigureAwait(false))
        {
            return new ToolkitResult(
                ToolkitResultStatus.Unauthorized,
                null,
                $"Toolkit '{toolkit.Id}' (category: {toolkit.Category}) is not permitted for subject '{subjectId}'.");
        }

        return await toolkit.ExecuteAsync(action, parameters, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Filter a list of manifests down to those accessible to <paramref name="subjectId"/>.
    /// </summary>
    public async Task<IReadOnlyList<CapabilityManifest>> FilterAsync(
        IEnumerable<CapabilityManifest> manifests,
        string                         subjectId,
        CancellationToken              ct = default)
    {
        ArgumentNullException.ThrowIfNull(manifests);
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);

        var policy = await _policyProvider.GetPolicyAsync(subjectId, ct).ConfigureAwait(false);
        var result = new List<CapabilityManifest>();

        foreach (var m in manifests)
        {
            if (policy.BlockedToolkitIds.Contains(m.Id, StringComparer.OrdinalIgnoreCase))
                continue;

            if (policy.AllowedToolkitIds.Count > 0)
            {
                if (policy.AllowedToolkitIds.Contains(m.Id, StringComparer.OrdinalIgnoreCase))
                    result.Add(m);
                continue;
            }

            if (policy.AllowedCategories.Count > 0)
            {
                if (policy.AllowedCategories.Contains(m.Category))
                    result.Add(m);
                continue;
            }

            if (!policy.DefaultDeny) result.Add(m);
        }

        return result;
    }
}
