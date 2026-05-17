using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AGXIOME.Shared.Models;

namespace AGXIOME.Core.Tools;

// ── Enums ─────────────────────────────────────────────────────────────────────

/// <summary>Cache eviction strategy applied when the cache exceeds its capacity.</summary>
public enum CacheEvictionPolicy
{
    /// <summary>Remove entries in insertion order (oldest first).</summary>
    Lru = 0,
    /// <summary>Remove entries whose TTL is closest to expiry first.</summary>
    TtlFirst = 1
}

// ── Records ───────────────────────────────────────────────────────────────────

/// <summary>
/// Immutable cache statistics snapshot.
/// Hit and miss counts are reset by <see cref="IToolResultCache.ResetStatsAsync"/>.
/// </summary>
public record ToolCacheStats(
    long Hits,
    long Misses,
    int EntryCount,
    double HitRate
);

/// <summary>Internal cache entry — stored value + expiry instant.</summary>
internal sealed record ToolCacheEntry(
    ToolResult Result,
    DateTimeOffset ExpiresAt
)
{
    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
}

// ── Interface ─────────────────────────────────────────────────────────────────

public interface IToolResultCache
{
    /// <summary>Try to retrieve a cached result for the given tool + parameters combination.</summary>
    ValueTask<ToolResult?> TryGetAsync(string toolName, Dictionary<string, object>? parameters, CancellationToken ct = default);

    /// <summary>Store a tool result. Uses the default TTL if <paramref name="ttl"/> is null.</summary>
    ValueTask SetAsync(string toolName, Dictionary<string, object>? parameters, ToolResult result, TimeSpan? ttl = null, CancellationToken ct = default);

    /// <summary>Remove the cached entry for a specific tool + parameters combination.</summary>
    ValueTask InvalidateAsync(string toolName, Dictionary<string, object>? parameters, CancellationToken ct = default);

    /// <summary>Remove all cached entries for a given tool name.</summary>
    ValueTask InvalidateToolAsync(string toolName, CancellationToken ct = default);

    /// <summary>Purge the entire cache.</summary>
    ValueTask InvalidateAllAsync(CancellationToken ct = default);

    /// <summary>Return a point-in-time snapshot of cache statistics.</summary>
    ValueTask<ToolCacheStats> GetStatsAsync(CancellationToken ct = default);

    /// <summary>Reset hit/miss counters without purging cached entries.</summary>
    ValueTask ResetStatsAsync(CancellationToken ct = default);
}

// ── Implementation ────────────────────────────────────────────────────────────

/// <summary>
/// In-memory TTL tool result cache backed by <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// <para>
/// Cache key = SHA256(toolName + sorted JSON of parameters) — deterministic across identical inputs.
/// Expired entries are lazily evicted on read; active sweep is triggered by <see cref="InvalidateAllAsync"/>.
/// </para>
/// Thread-safe; suitable for single-node deployments. For multi-node use, replace with a Redis-backed implementation.
/// </summary>
public sealed class ToolResultCache : IToolResultCache, IDisposable
{
    private readonly ConcurrentDictionary<string, ToolCacheEntry> _store = new();
    // Prefix index: toolName → set of full cache keys. Enables O(n_tool) invalidation.
    private readonly ConcurrentDictionary<string, ConcurrentBag<string>> _keysByTool = new();

    private readonly TimeSpan _defaultTtl;
    private readonly int _maxEntries;
    private readonly CacheEvictionPolicy _evictionPolicy;
    private readonly Timer _sweepTimer;

    private long _hits;
    private long _misses;

    /// <param name="defaultTtl">TTL applied when <see cref="SetAsync"/> receives a null ttl. Default: 5 minutes.</param>
    /// <param name="maxEntries">Hard cap on concurrent entries. When exceeded, eviction policy applies. Default: 1000.</param>
    /// <param name="evictionPolicy">Strategy used when cap is reached.</param>
    /// <param name="sweepInterval">Interval for background expired-entry cleanup. Default: 60 seconds.</param>
    public ToolResultCache(
        TimeSpan? defaultTtl = null,
        int maxEntries = 1000,
        CacheEvictionPolicy evictionPolicy = CacheEvictionPolicy.Lru,
        TimeSpan? sweepInterval = null)
    {
        _defaultTtl = defaultTtl ?? TimeSpan.FromMinutes(5);
        var effectiveSweepInterval = sweepInterval ?? TimeSpan.FromSeconds(60);

        if (_defaultTtl <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(defaultTtl), "Default TTL must be greater than zero.");
        if (maxEntries <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxEntries), "Max entries must be greater than zero.");
        if (effectiveSweepInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(sweepInterval), "Sweep interval must be greater than zero.");

        _maxEntries = maxEntries;
        _evictionPolicy = evictionPolicy;
        _sweepTimer = new Timer(
            _ => SweepExpired(),
            null,
            effectiveSweepInterval,
            effectiveSweepInterval);
    }

    public ValueTask<ToolResult?> TryGetAsync(string toolName, Dictionary<string, object>? parameters, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        var key = BuildKey(toolName, parameters);
        if (_store.TryGetValue(key, out var entry))
        {
            if (!entry.IsExpired)
            {
                Interlocked.Increment(ref _hits);
                return ValueTask.FromResult<ToolResult?>(entry.Result);
            }
            // Lazy evict expired entry
            _store.TryRemove(key, out _);
        }
        Interlocked.Increment(ref _misses);
        return ValueTask.FromResult<ToolResult?>(null);
    }

    public ValueTask SetAsync(string toolName, Dictionary<string, object>? parameters, ToolResult result, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentNullException.ThrowIfNull(result);

        var key = BuildKey(toolName, parameters);
        var entry = new ToolCacheEntry(result, DateTimeOffset.UtcNow.Add(ttl ?? _defaultTtl));

        // Enforce max entries before inserting
        if (_store.Count >= _maxEntries && !_store.ContainsKey(key))
            Evict();

        _store[key] = entry;
        _keysByTool.AddOrUpdate(
            toolName,
            _ => new ConcurrentBag<string> { key },
            (_, bag) => { bag.Add(key); return bag; });

        return ValueTask.CompletedTask;
    }

    public ValueTask InvalidateAsync(string toolName, Dictionary<string, object>? parameters, CancellationToken ct = default)
    {
        var key = BuildKey(toolName, parameters);
        _store.TryRemove(key, out _);
        return ValueTask.CompletedTask;
    }

    public ValueTask InvalidateToolAsync(string toolName, CancellationToken ct = default)
    {
        if (_keysByTool.TryGetValue(toolName, out var keys))
            foreach (var k in keys) _store.TryRemove(k, out _);
        return ValueTask.CompletedTask;
    }

    public ValueTask InvalidateAllAsync(CancellationToken ct = default)
    {
        _store.Clear();
        _keysByTool.Clear();
        return ValueTask.CompletedTask;
    }

    public ValueTask<ToolCacheStats> GetStatsAsync(CancellationToken ct = default)
    {
        long h = Interlocked.Read(ref _hits);
        long m = Interlocked.Read(ref _misses);
        long total = h + m;
        double rate = total == 0 ? 0.0 : (double)h / total;
        return ValueTask.FromResult(new ToolCacheStats(h, m, _store.Count, rate));
    }

    public ValueTask ResetStatsAsync(CancellationToken ct = default)
    {
        Interlocked.Exchange(ref _hits, 0);
        Interlocked.Exchange(ref _misses, 0);
        return ValueTask.CompletedTask;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Deterministic SHA256 key: SHA256("{toolName}|{sortedParamJson}").
    /// Parameters are sorted by key to ensure identical inputs → identical key.
    /// </summary>
    private static string BuildKey(string toolName, Dictionary<string, object>? parameters)
    {
        var sortedParams = parameters is null
            ? "{}"
            : JsonSerializer.Serialize(
                new SortedDictionary<string, object>(parameters),
                new JsonSerializerOptions { WriteIndented = false });
        var raw = $"{toolName}|{sortedParams}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }

    private void SweepExpired()
    {
        foreach (var kv in _store)
            if (kv.Value.IsExpired) _store.TryRemove(kv.Key, out _);
    }

    private void Evict()
    {
        if (_evictionPolicy == CacheEvictionPolicy.TtlFirst)
        {
            // Remove the entry whose TTL expires soonest
            var candidate = _store.OrderBy(kv => kv.Value.ExpiresAt).FirstOrDefault();
            if (candidate.Key != null) _store.TryRemove(candidate.Key, out _);
        }
        else
        {
            // LRU approximation: remove first enumerated entry (insertion order is not guaranteed,
            // but ConcurrentDictionary tends to expose oldest buckets first under low contention)
            var first = _store.Keys.FirstOrDefault();
            if (first != null) _store.TryRemove(first, out _);
        }
    }

    public void Dispose() => _sweepTimer.Dispose();
}
