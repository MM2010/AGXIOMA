using System.Collections.Concurrent;

namespace AGXIOME.Core.Tools;

// ── Enums ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Defines when a "stop after tool" rule triggers relative to the tool's outcome.
/// </summary>
public enum StopAfterToolMode
{
    /// <summary>Never stop — rule is effectively disabled.</summary>
    Never = 0,
    /// <summary>Stop immediately after the tool is called regardless of outcome.</summary>
    AfterAnyCall = 1,
    /// <summary>Stop only when the tool completes successfully (Success == true).</summary>
    AfterSuccess = 2,
    /// <summary>Stop only when the tool call fails (Success == false).</summary>
    AfterError = 3
}

// ── Records ───────────────────────────────────────────────────────────────────

/// <summary>
/// Associates a tool name with a stop condition.
/// <para>
/// Example: stop the run as soon as <c>submit_order</c> succeeds, so no further
/// tools are called after an irreversible side-effect tool fires.
/// </para>
/// </summary>
public record StopAfterToolRule(
    /// <summary>Exact tool name this rule applies to (case-sensitive).</summary>
    string ToolName,
    /// <summary>Condition that triggers a stop.</summary>
    StopAfterToolMode Mode,
    /// <summary>Optional message surfaced to the agent/orchestrator explaining the stop.</summary>
    string? ExitMessage = null
);

/// <summary>Result returned by <see cref="IStopAfterToolEvaluator.Evaluate"/>.</summary>
public record StopEvaluation(
    bool ShouldStop,
    string ToolName,
    StopAfterToolMode TriggerMode,
    string? ExitMessage
)
{
    public static StopEvaluation Continue(string toolName) =>
        new(false, toolName, StopAfterToolMode.Never, null);
}

// ── Interface ─────────────────────────────────────────────────────────────────

public interface IStopAfterToolEvaluator
{
    /// <summary>Add or replace a stop rule for the given tool name.</summary>
    void AddRule(StopAfterToolRule rule);

    /// <summary>Remove the rule for the given tool name. No-op if absent.</summary>
    void RemoveRule(string toolName);

    /// <summary>
    /// Evaluate whether the run should stop given a tool outcome.
    /// <paramref name="success"/> indicates whether the tool call succeeded.
    /// </summary>
    StopEvaluation Evaluate(string toolName, bool success);

    /// <summary>Return all registered rules (snapshot).</summary>
    IReadOnlyList<StopAfterToolRule> GetRules();

    /// <summary>Remove all registered rules.</summary>
    void Clear();
}

// ── Implementation ────────────────────────────────────────────────────────────

/// <summary>
/// Thread-safe evaluator for "stop-after-tool" rules.
/// Rules are stored in a <see cref="ConcurrentDictionary{TKey,TValue}"/> keyed by tool name.
/// Designed to be instantiated once per run (or shared across runs if rules are global).
/// </summary>
public sealed class StopAfterToolEvaluator : IStopAfterToolEvaluator
{
    /// <summary>Global singleton evaluator used by Auto mode.</summary>
    public static readonly StopAfterToolEvaluator Global = new();
    private readonly ConcurrentDictionary<string, StopAfterToolRule> _rules = new();

    public void AddRule(StopAfterToolRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentException.ThrowIfNullOrWhiteSpace(rule.ToolName);
        if (!Enum.IsDefined(rule.Mode))
            throw new ArgumentOutOfRangeException(nameof(rule), rule.Mode, "Stop-after-tool mode is not valid.");
        _rules[rule.ToolName] = rule;
    }

    public void RemoveRule(string toolName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        _rules.TryRemove(toolName, out _);
    }

    public StopEvaluation Evaluate(string toolName, bool success)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        if (!_rules.TryGetValue(toolName, out var rule))
            return StopEvaluation.Continue(toolName);

        bool stop = rule.Mode switch
        {
            StopAfterToolMode.AfterAnyCall => true,
            StopAfterToolMode.AfterSuccess => success,
            StopAfterToolMode.AfterError   => !success,
            _                              => false  // Never
        };

        return new StopEvaluation(stop, toolName, rule.Mode, stop ? rule.ExitMessage : null);
    }

    public IReadOnlyList<StopAfterToolRule> GetRules()
        => _rules.Values.ToList();

    public void Clear() => _rules.Clear();
}
