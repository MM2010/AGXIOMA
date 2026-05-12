using System.Collections.Concurrent;

namespace AGXIOME.Core.Execution;

// ─────────────────────────────────────────────────────────────────────────────
// CancellationManager — CE-02
// In-memory cancellation token registry with optional Redis-backed distributed
// signalling. All operations are thread-safe.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Contract for managing cancellation tokens across run sessions.</summary>
public interface ICancellationManager
{
    /// <summary>Register a new token for <paramref name="runId"/>. Replaces any existing registration.</summary>
    CancellationToken Register(string runId);

    /// <summary>Signal cancellation for <paramref name="runId"/>. Returns false if the run ID is unknown.</summary>
    bool Cancel(string runId);

    /// <summary>Cancel all currently registered runs.</summary>
    void CancelAll();

    /// <summary>Remove the registration for a completed or forgotten run.</summary>
    bool Remove(string runId);

    /// <summary>True if the run is known and its token is not yet cancelled.</summary>
    bool IsActive(string runId);

    /// <summary>List all registered run IDs (snapshot).</summary>
    IReadOnlyList<string> ActiveRunIds { get; }
}

/// <summary>In-memory cancellation manager. For distributed scenarios, extend by propagating
/// <see cref="Cancel"/> calls via Redis Pub/Sub after the local cancellation.</summary>
public sealed class CancellationManager : ICancellationManager, IDisposable
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _registry = new();

    public CancellationToken Register(string runId)
    {
        // Dispose previous CTS if replaced.
        if (_registry.TryRemove(runId, out var old))
            old.Dispose();

        var cts = new CancellationTokenSource();
        _registry[runId] = cts;
        return cts.Token;
    }

    public bool Cancel(string runId)
    {
        if (!_registry.TryGetValue(runId, out var cts)) return false;
        if (cts.IsCancellationRequested) return false;
        cts.Cancel();
        return true;
    }

    public void CancelAll()
    {
        foreach (var cts in _registry.Values)
        {
            if (!cts.IsCancellationRequested)
                cts.Cancel();
        }
    }

    public bool Remove(string runId)
    {
        if (!_registry.TryRemove(runId, out var cts)) return false;
        cts.Dispose();
        return true;
    }

    public bool IsActive(string runId) =>
        _registry.TryGetValue(runId, out var cts) && !cts.IsCancellationRequested;

    public IReadOnlyList<string> ActiveRunIds =>
        _registry.Where(kv => !kv.Value.IsCancellationRequested)
                 .Select(kv => kv.Key)
                 .ToList();

    public void Dispose()
    {
        foreach (var cts in _registry.Values) cts.Dispose();
        _registry.Clear();
    }
}
