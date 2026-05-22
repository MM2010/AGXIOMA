// CO-A5 — BudgetAwareExecutor
// Wraps IToolkit.ExecuteAsync with per-toolkit budget enforcement and metrics recording.
// Integrates IToolMetricsCollector (TI-06, already in AGXIOME.Core) for latency + error tracking.

using System.Collections.Concurrent;
using System.Diagnostics;
using AGXIOME.Core.Tools;

namespace AGXIOME.Toolkits.Orchestration;

// ── Budget ────────────────────────────────────────────────────────────────────

/// <summary>
/// Execution budget constraints for a toolkit invocation.
/// All fields are optional — only constraints that are explicitly set are enforced.
/// </summary>
public sealed record ToolkitBudget
{
    /// <summary>Maximum calls per 60-second rolling window. Default: unlimited.</summary>
    public int MaxCallsPerMinute { get; init; } = int.MaxValue;

    /// <summary>Maximum wall-clock time for a single <see cref="IToolkit.ExecuteAsync"/> call.</summary>
    public TimeSpan? MaxDuration { get; init; }

    /// <summary>Maximum total calls across the lifetime of this executor instance. Default: unlimited.</summary>
    public int MaxTotalCalls { get; init; } = int.MaxValue;

    public static readonly ToolkitBudget Unlimited = new();

    public static ToolkitBudget PerMinute(int maxCalls) => new() { MaxCallsPerMinute = maxCalls };
    public static ToolkitBudget WithTimeout(TimeSpan timeout) => new() { MaxDuration = timeout };
}

// ── Executor ──────────────────────────────────────────────────────────────────

/// <summary>
/// Executes toolkit actions with budget enforcement (rate limit, timeout, total call cap)
/// and records every call to <see cref="IToolMetricsCollector"/> for latency/error tracking.
///
/// <para>
/// This is the primary execution gateway in the orchestration pipeline — downstream callers
/// should prefer this over calling <see cref="IToolkit.ExecuteAsync"/> directly.
/// </para>
/// </summary>
public sealed class BudgetAwareExecutor
{
    private readonly IToolMetricsCollector _metrics;

    // Per-toolkit sliding-window state for rate limiting
    private readonly ConcurrentDictionary<string, RateLimitState> _rateLimits = new();

    // Global call counter across all toolkits (for total-calls budget)
    private long _totalCallCount;

    public BudgetAwareExecutor(IToolMetricsCollector metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        _metrics = metrics;
    }

    // ── Execution ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Execute a toolkit action with budget enforcement and metrics recording.
    /// </summary>
    /// <param name="toolkit">Toolkit to invoke.</param>
    /// <param name="action">Action name.</param>
    /// <param name="parameters">Action parameters.</param>
    /// <param name="budget">Budget constraints. <c>null</c> → <see cref="ToolkitBudget.Unlimited"/>.</param>
    /// <param name="ct">Cancellation token (combined with timeout when <see cref="ToolkitBudget.MaxDuration"/> is set).</param>
    public async Task<ToolkitResult> ExecuteAsync(
        IToolkit                  toolkit,
        string                    action,
        Dictionary<string, object?> parameters,
        ToolkitBudget?            budget = null,
        CancellationToken         ct     = default)
    {
        ArgumentNullException.ThrowIfNull(toolkit);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);

        budget ??= ToolkitBudget.Unlimited;

        // Total-calls cap (across all toolkits)
        var callNumber = Interlocked.Increment(ref _totalCallCount);
        if (callNumber > budget.MaxTotalCalls)
        {
            Interlocked.Decrement(ref _totalCallCount);
            return ToolkitResult.Fail(
                $"Total call budget exhausted: max {budget.MaxTotalCalls} calls allowed.");
        }

        // Per-toolkit rate limit (calls/min)
        if (!CheckRateLimit(toolkit.Id, budget.MaxCallsPerMinute))
        {
            return ToolkitResult.Fail(
                $"Rate limit exceeded for '{toolkit.Id}': max {budget.MaxCallsPerMinute} calls/min.");
        }

        // Timeout CTS (linked to caller token)
        CancellationTokenSource? cts = null;
        if (budget.MaxDuration.HasValue)
        {
            cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(budget.MaxDuration.Value);
        }

        var sw = Stopwatch.StartNew();
        try
        {
            var result = await toolkit.ExecuteAsync(action, parameters, cts?.Token ?? ct)
                                       .ConfigureAwait(false);
            sw.Stop();
            _metrics.Record(toolkit.Id, result.Status == ToolkitResultStatus.Ok, sw.Elapsed.TotalMilliseconds);
            return result;
        }
        catch (OperationCanceledException) when (cts?.IsCancellationRequested == true && !ct.IsCancellationRequested)
        {
            sw.Stop();
            _metrics.Record(toolkit.Id, false, sw.Elapsed.TotalMilliseconds);
            return ToolkitResult.Fail(
                $"Toolkit '{toolkit.Id}' exceeded max duration ({budget.MaxDuration}).");
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            _metrics.Record(toolkit.Id, false, sw.Elapsed.TotalMilliseconds);
            throw;
        }
        finally
        {
            cts?.Dispose();
        }
    }

    // ── Metrics Passthrough ───────────────────────────────────────────────────

    /// <summary>Return the collected metrics snapshot for a specific toolkit.</summary>
    public ToolMetricSnapshot? GetMetrics(string toolkitId)
        => _metrics.GetSnapshot(toolkitId);

    /// <summary>Return metrics for all toolkits that have been executed.</summary>
    public IReadOnlyList<ToolMetricSnapshot> GetAllMetrics()
        => _metrics.GetAllSnapshots();

    /// <summary>Total number of executions dispatched through this executor instance.</summary>
    public long TotalCallCount => Interlocked.Read(ref _totalCallCount);

    // ── Rate Limit ────────────────────────────────────────────────────────────

    private bool CheckRateLimit(string toolkitId, int maxPerMinute)
    {
        if (maxPerMinute == int.MaxValue) return true;

        var state = _rateLimits.GetOrAdd(toolkitId, _ => new RateLimitState());
        var now = DateTimeOffset.UtcNow;

        lock (state)
        {
            // Reset window if more than 60 seconds have passed
            if ((now - state.WindowStart).TotalSeconds >= 60)
            {
                state.WindowStart = now;
                state.Count = 0;
            }

            if (state.Count >= maxPerMinute) return false;
            state.Count++;
            return true;
        }
    }

    private sealed class RateLimitState
    {
        public DateTimeOffset WindowStart = DateTimeOffset.UtcNow;
        public int Count;
    }
}
