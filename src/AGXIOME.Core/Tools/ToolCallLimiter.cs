using System.Collections.Concurrent;

namespace AGXIOME.Core.Tools;

// ── Enums ─────────────────────────────────────────────────────────────────────

/// <summary>Reason a tool call limit check failed.</summary>
public enum ToolCallLimitViolation
{
    None = 0,
    /// <summary>Total call count across all tools for the run exceeded the cap.</summary>
    TotalCallsExceeded = 1,
    /// <summary>Too many consecutive errors without a successful call in between.</summary>
    ConsecutiveErrorsExceeded = 2,
    /// <summary>A single tool was called more times than its per-tool cap allows.</summary>
    PerToolCallsExceeded = 3
}

// ── Records ───────────────────────────────────────────────────────────────────

/// <summary>
/// Hard limits applied per run.
/// A value of 0 means "no limit" for that dimension.
/// </summary>
public record ToolCallLimits(
    /// <summary>Max total tool calls across all tools in a run. 0 = unlimited.</summary>
    int MaxTotalCalls = 100,
    /// <summary>Max consecutive errors before the run is halted. 0 = unlimited.</summary>
    int MaxConsecutiveErrors = 5,
    /// <summary>Max calls for any individual tool within the run. 0 = unlimited.</summary>
    int MaxCallsPerTool = 0
);

/// <summary>Snapshot of counters for a single run.</summary>
public record ToolCallRunStats(
    string RunId,
    int TotalCalls,
    int ConsecutiveErrors,
    int TotalErrors,
    IReadOnlyDictionary<string, int> CallsPerTool
);

/// <summary>Result of a limit check — includes the violation type and a human-readable reason.</summary>
public record ToolCallLimitCheckResult(
    bool Allowed,
    ToolCallLimitViolation Violation,
    string? Reason
)
{
    public static readonly ToolCallLimitCheckResult Ok = new(true, ToolCallLimitViolation.None, null);
}

// ── State (internal) ──────────────────────────────────────────────────────────

internal sealed class RunCallState
{
    public int TotalCalls;
    public int ConsecutiveErrors;
    public int TotalErrors;
    public readonly ConcurrentDictionary<string, int> PerTool = new();
}

// ── Interface ─────────────────────────────────────────────────────────────────

public interface IToolCallLimiter
{
    /// <summary>Check whether a call to <paramref name="toolName"/> is permitted under current limits.</summary>
    ToolCallLimitCheckResult Check(string runId, string toolName);

    /// <summary>Record a successful tool call (resets consecutive-error counter).</summary>
    void RecordSuccess(string runId, string toolName);

    /// <summary>Record a failed tool call (increments consecutive-error counter).</summary>
    void RecordError(string runId, string toolName);

    /// <summary>Return a snapshot of counters for the given run, or null if the run is unknown.</summary>
    ToolCallRunStats? GetStats(string runId);

    /// <summary>Reset all counters for a run (e.g. on run teardown or retry).</summary>
    void Reset(string runId);

    /// <summary>Remove all state for a run (frees memory after run is complete).</summary>
    void Remove(string runId);
}

// ── Implementation ────────────────────────────────────────────────────────────

/// <summary>
/// Thread-safe per-run tool call limiter.
/// Call <see cref="Check"/> before executing a tool; call <see cref="RecordSuccess"/> or
/// <see cref="RecordError"/> after execution to update the counters.
/// </summary>
public sealed class ToolCallLimiter : IToolCallLimiter
{
    private readonly ToolCallLimits _limits;
    private readonly ConcurrentDictionary<string, RunCallState> _state = new();

    public ToolCallLimiter(ToolCallLimits? limits = null)
        => _limits = limits ?? new ToolCallLimits();

    public ToolCallLimitCheckResult Check(string runId, string toolName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        var s = _state.GetOrAdd(runId, _ => new RunCallState());

        if (_limits.MaxTotalCalls > 0 && s.TotalCalls >= _limits.MaxTotalCalls)
            return new(false, ToolCallLimitViolation.TotalCallsExceeded,
                $"Run '{runId}' reached the maximum of {_limits.MaxTotalCalls} total tool calls.");

        if (_limits.MaxConsecutiveErrors > 0 && s.ConsecutiveErrors >= _limits.MaxConsecutiveErrors)
            return new(false, ToolCallLimitViolation.ConsecutiveErrorsExceeded,
                $"Run '{runId}' reached {_limits.MaxConsecutiveErrors} consecutive errors.");

        if (_limits.MaxCallsPerTool > 0)
        {
            s.PerTool.TryGetValue(toolName, out var perToolCount);
            if (perToolCount >= _limits.MaxCallsPerTool)
                return new(false, ToolCallLimitViolation.PerToolCallsExceeded,
                    $"Tool '{toolName}' in run '{runId}' reached its per-tool limit of {_limits.MaxCallsPerTool} calls.");
        }

        return ToolCallLimitCheckResult.Ok;
    }

    public void RecordSuccess(string runId, string toolName)
    {
        var s = _state.GetOrAdd(runId, _ => new RunCallState());
        Interlocked.Increment(ref s.TotalCalls);
        Interlocked.Exchange(ref s.ConsecutiveErrors, 0); // reset streak on success
        s.PerTool.AddOrUpdate(toolName, 1, (_, v) => v + 1);
    }

    public void RecordError(string runId, string toolName)
    {
        var s = _state.GetOrAdd(runId, _ => new RunCallState());
        Interlocked.Increment(ref s.TotalCalls);
        Interlocked.Increment(ref s.ConsecutiveErrors);
        Interlocked.Increment(ref s.TotalErrors);
        s.PerTool.AddOrUpdate(toolName, 1, (_, v) => v + 1);
    }

    public ToolCallRunStats? GetStats(string runId)
    {
        if (!_state.TryGetValue(runId, out var s)) return null;
        return new ToolCallRunStats(
            runId,
            s.TotalCalls,
            s.ConsecutiveErrors,
            s.TotalErrors,
            s.PerTool.ToDictionary(kv => kv.Key, kv => kv.Value));
    }

    public void Reset(string runId)
    {
        if (!_state.TryGetValue(runId, out var s)) return;
        Interlocked.Exchange(ref s.TotalCalls, 0);
        Interlocked.Exchange(ref s.ConsecutiveErrors, 0);
        Interlocked.Exchange(ref s.TotalErrors, 0);
        s.PerTool.Clear();
    }

    public void Remove(string runId) => _state.TryRemove(runId, out _);
}
