using System.Collections.Concurrent;

namespace AGXIOME.Core.Tools;

// ── Enums ─────────────────────────────────────────────────────────────────────

/// <summary>Resolution used when computing approximate percentile from a rolling sample.</summary>
public enum PercentileApproximation
{
    /// <summary>Use a fixed-size sorted ring buffer — low memory, accurate enough for p95.</summary>
    RingBuffer = 0,
    /// <summary>Track only min/max/sum — minimal overhead, no percentile support.</summary>
    SummaryOnly = 1
}

// ── Records ───────────────────────────────────────────────────────────────────

/// <summary>
/// Immutable point-in-time metrics snapshot for a single tool.
/// </summary>
public record ToolMetricSnapshot(
    string ToolName,
    long TotalCalls,
    long SuccessCount,
    long ErrorCount,
    double TotalLatencyMs,
    double AvgLatencyMs,
    /// <summary>
    /// Approximate p95 latency in ms. -1 when insufficient samples exist (&lt; 20 calls).
    /// </summary>
    double P95LatencyMs,
    double ErrorRate,
    DateTimeOffset? LastCalledAt,
    DateTimeOffset? LastErrorAt
);

// ── Internal state ────────────────────────────────────────────────────────────

/// <summary>Mutable per-tool counters. All fields accessed via Interlocked or lock.</summary>
internal sealed class ToolMetricState
{
    // Ring buffer size for p95 calculation. Keeps last N latency samples.
    private const int RingSize = 200;

    public long TotalCalls;
    public long SuccessCount;
    public long ErrorCount;
    public double TotalLatencyMs;  // protected by _latencyLock
    public long LastCalledAtTicks; // UTC ticks, 0 = never called
    public long LastErrorAtTicks;  // UTC ticks, 0 = never errored

    private readonly double[] _ring = new double[RingSize];
    private int _ringHead;
    private int _ringFilled;
    private readonly object _latencyLock = new();

    public void RecordLatency(double ms)
    {
        lock (_latencyLock)
        {
            TotalLatencyMs += ms;
            _ring[_ringHead % RingSize] = ms;
            _ringHead++;
            if (_ringFilled < RingSize) _ringFilled++;
        }
    }

    public double ComputeP95()
    {
        double[] samples;
        lock (_latencyLock)
        {
            if (_ringFilled < 20) return -1.0;
            samples = _ring[.._ringFilled];
        }
        Array.Sort(samples);
        int idx = (int)Math.Ceiling(samples.Length * 0.95) - 1;
        return samples[Math.Clamp(idx, 0, samples.Length - 1)];
    }
}

// ── Interface ─────────────────────────────────────────────────────────────────

public interface IToolMetricsCollector
{
    /// <summary>Record a completed tool call (success or failure) with its measured latency.</summary>
    void Record(string toolName, bool success, double latencyMs);

    /// <summary>Return a metrics snapshot for a single tool, or null if no data exists.</summary>
    ToolMetricSnapshot? GetSnapshot(string toolName);

    /// <summary>Return snapshots for every tool that has been recorded.</summary>
    IReadOnlyList<ToolMetricSnapshot> GetAllSnapshots();

    /// <summary>Reset all metrics for a specific tool.</summary>
    void Reset(string toolName);

    /// <summary>Reset metrics for all tools.</summary>
    void ResetAll();
}

// ── Implementation ────────────────────────────────────────────────────────────

/// <summary>
/// Lock-free (mostly) per-tool metrics collector using <see cref="ConcurrentDictionary{TKey,TValue}"/>
/// with fine-grained <see cref="Interlocked"/> counters.
/// <para>
/// Latency percentile (p95) uses a ring buffer of the last 200 samples — accurate enough for
/// operational alerting without maintaining a full sorted list.
/// </para>
/// Thread-safe. Suitable for high-frequency tool-dispatch paths on a single node.
/// For multi-node aggregation, push snapshots to a central metrics sink (e.g. Prometheus).
/// </summary>
public sealed class ToolMetricsCollector : IToolMetricsCollector
{
    private readonly ConcurrentDictionary<string, ToolMetricState> _states = new();

    public void Record(string toolName, bool success, double latencyMs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        var state = _states.GetOrAdd(toolName, _ => new ToolMetricState());

        Interlocked.Increment(ref state.TotalCalls);
        if (success)
            Interlocked.Increment(ref state.SuccessCount);
        else
        {
            Interlocked.Increment(ref state.ErrorCount);
            Interlocked.Exchange(ref state.LastErrorAtTicks, DateTimeOffset.UtcNow.UtcTicks);
        }

        state.RecordLatency(latencyMs);
        Interlocked.Exchange(ref state.LastCalledAtTicks, DateTimeOffset.UtcNow.UtcTicks);
    }

    public ToolMetricSnapshot? GetSnapshot(string toolName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        if (!_states.TryGetValue(toolName, out var s)) return null;
        return BuildSnapshot(toolName, s);
    }

    public IReadOnlyList<ToolMetricSnapshot> GetAllSnapshots()
        => _states.Select(kv => BuildSnapshot(kv.Key, kv.Value)).ToList();

    public void Reset(string toolName) => _states.TryRemove(toolName, out _);

    public void ResetAll() => _states.Clear();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ToolMetricSnapshot BuildSnapshot(string toolName, ToolMetricState s)
    {
        long total   = Interlocked.Read(ref s.TotalCalls);
        long success = Interlocked.Read(ref s.SuccessCount);
        long errors  = Interlocked.Read(ref s.ErrorCount);
        double totalMs = s.TotalLatencyMs;  // slight race acceptable — this is a metrics path
        double avgMs   = total > 0 ? totalMs / total : 0.0;
        double errorRate = total > 0 ? (double)errors / total : 0.0;
        double p95Ms   = s.ComputeP95();

        long lastCalledTicks = Interlocked.Read(ref s.LastCalledAtTicks);
        long lastErrorTicks  = Interlocked.Read(ref s.LastErrorAtTicks);

        return new ToolMetricSnapshot(
            toolName,
            total,
            success,
            errors,
            totalMs,
            avgMs,
            p95Ms,
            errorRate,
            lastCalledTicks > 0 ? new DateTimeOffset(lastCalledTicks, TimeSpan.Zero) : null,
            lastErrorTicks > 0  ? new DateTimeOffset(lastErrorTicks,  TimeSpan.Zero) : null
        );
    }
}
