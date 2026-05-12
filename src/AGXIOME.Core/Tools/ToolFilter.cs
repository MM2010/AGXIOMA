using System.Collections.Concurrent;

namespace AGXIOME.Core.Tools;

// ── Enums ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Determines how the tool filter interprets its tool name list.
/// </summary>
public enum ToolFilterMode
{
    /// <summary>
    /// Only the listed tools are permitted; all others are blocked.
    /// An empty list means no tools are allowed.
    /// </summary>
    Allowlist = 0,
    /// <summary>
    /// All tools are permitted except those listed.
    /// An empty list means all tools are allowed.
    /// </summary>
    Blocklist = 1
}

// ── Records ───────────────────────────────────────────────────────────────────

/// <summary>
/// A filter rule scoped to a session or a specific run (run-level rules override session-level).
/// </summary>
public record ToolFilterRule(
    /// <summary>Session or run identifier this rule belongs to.</summary>
    string ScopeId,
    ToolFilterMode Mode,
    /// <summary>Tool names subject to the filter (case-sensitive).</summary>
    IReadOnlySet<string> ToolNames
);

// ── Interface ─────────────────────────────────────────────────────────────────

public interface IToolFilter
{
    /// <summary>
    /// Set (or replace) the filter rule for a scope.
    /// Pass an empty set of tool names together with <see cref="ToolFilterMode.Blocklist"/>
    /// to allow all tools.
    /// </summary>
    void SetFilter(ToolFilterRule rule);

    /// <summary>
    /// Returns true when the given tool is allowed under the rule for <paramref name="scopeId"/>.
    /// If no rule is registered for the scope, all tools are allowed by default.
    /// </summary>
    bool IsAllowed(string scopeId, string toolName);

    /// <summary>Return the current rule for a scope, or null if not set.</summary>
    ToolFilterRule? GetFilter(string scopeId);

    /// <summary>Remove the filter rule for a scope (reverts to "allow all").</summary>
    void ClearFilter(string scopeId);

    /// <summary>Remove all filter rules.</summary>
    void ClearAll();

    /// <summary>Snapshot of all registered rules.</summary>
    IReadOnlyList<ToolFilterRule> GetAllFilters();
}

// ── Implementation ────────────────────────────────────────────────────────────

/// <summary>
/// Thread-safe tool filter supporting per-scope allow/block-list rules.
/// <para>
/// Typical usage:
/// <list type="bullet">
///   <item>Set a session-level rule (scopeId = sessionId) during session bootstrap.</item>
///   <item>Optionally override with a run-level rule (scopeId = runId) for a specific agent invocation.</item>
///   <item>Call <see cref="IsAllowed"/> in the tool dispatch path before executing a tool.</item>
/// </list>
/// </para>
/// </summary>
public sealed class ToolFilter : IToolFilter
{
    private readonly ConcurrentDictionary<string, ToolFilterRule> _rules = new();

    public void SetFilter(ToolFilterRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentException.ThrowIfNullOrWhiteSpace(rule.ScopeId);
        _rules[rule.ScopeId] = rule;
    }

    public bool IsAllowed(string scopeId, string toolName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        if (!_rules.TryGetValue(scopeId, out var rule))
            return true; // No rule → allow everything

        return rule.Mode switch
        {
            ToolFilterMode.Allowlist => rule.ToolNames.Contains(toolName),
            ToolFilterMode.Blocklist => !rule.ToolNames.Contains(toolName),
            _ => true
        };
    }

    public ToolFilterRule? GetFilter(string scopeId)
    {
        _rules.TryGetValue(scopeId, out var rule);
        return rule;
    }

    public void ClearFilter(string scopeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeId);
        _rules.TryRemove(scopeId, out _);
    }

    public void ClearAll() => _rules.Clear();

    public IReadOnlyList<ToolFilterRule> GetAllFilters() => _rules.Values.ToList();
}
