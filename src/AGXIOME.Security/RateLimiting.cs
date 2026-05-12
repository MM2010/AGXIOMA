// SA-05 — Rate Limiting
// Sliding-window token-bucket algorithm.
// Supports three limit dimensions: per-endpoint, per-user, per-IP.
// Designed for integration with ASP.NET Core middleware (inject IRateLimiter).

using System.Collections.Concurrent;

namespace AGXIOME.Security.RateLimit;

// ─── Enums ───────────────────────────────────────────────────────────────────

public enum RateLimitDimension { Endpoint = 0, User = 1, Ip = 2 }

// ─── Value Objects ────────────────────────────────────────────────────────────

/// <param name="MaxRequests">Maximum requests allowed in the window.</param>
/// <param name="Window">Sliding window duration.</param>
/// <param name="BurstAllowance">Extra requests above MaxRequests allowed in a short burst (default 0).</param>
public sealed record RateLimitPolicy(
    int      MaxRequests,
    TimeSpan Window,
    int      BurstAllowance = 0);

public sealed record RateLimitCheckResult(
    bool   IsAllowed,
    int    Remaining,
    TimeSpan RetryAfter);

// ─── Interface ────────────────────────────────────────────────────────────────

public interface IRateLimiter
{
    /// <summary>
    /// Checks and records a request attempt.
    /// key: composite key — "{dimension}:{identifier}:{endpoint}"
    /// </summary>
    RateLimitCheckResult Check(string key, RateLimitPolicy policy);
    void Reset(string key);
}

// ─── Sliding-Window Implementation ───────────────────────────────────────────

/// <summary>
/// Sliding-window rate limiter backed by an in-memory concurrent dictionary.
/// Timestamps are stored per key; expired entries are evicted lazily.
/// Thread-safe via lock-per-bucket strategy.
/// </summary>
public sealed class SlidingWindowRateLimiter : IRateLimiter
{
    // Each bucket is a sorted list of UTC ticks for request timestamps within the window.
    private readonly ConcurrentDictionary<string, RateLimitBucket> _buckets = new();

    public RateLimitCheckResult Check(string key, RateLimitPolicy policy)
    {
        var bucket = _buckets.GetOrAdd(key, _ => new RateLimitBucket());
        lock (bucket)
        {
            var now    = DateTimeOffset.UtcNow;
            var cutoff = now - policy.Window;
            bucket.Prune(cutoff);

            var effective = policy.MaxRequests + policy.BurstAllowance;
            if (bucket.Count < effective)
            {
                bucket.Add(now);
                var remaining = Math.Max(0, effective - bucket.Count);
                return new RateLimitCheckResult(true, remaining, TimeSpan.Zero);
            }

            // Rejected: compute retry-after as time until oldest request exits the window
            var oldest    = bucket.Oldest();
            var retryAfter = (oldest + policy.Window) - now;
            return new RateLimitCheckResult(false, 0, retryAfter < TimeSpan.Zero ? TimeSpan.Zero : retryAfter);
        }
    }

    public void Reset(string key) => _buckets.TryRemove(key, out _);
}

internal sealed class RateLimitBucket
{
    private readonly List<DateTimeOffset> _timestamps = [];

    public int Count => _timestamps.Count;

    public void Add(DateTimeOffset ts) => _timestamps.Add(ts);

    /// <summary>Removes timestamps older than cutoff (sliding window eviction).</summary>
    public void Prune(DateTimeOffset cutoff)
    {
        int i = 0;
        while (i < _timestamps.Count && _timestamps[i] < cutoff) i++;
        if (i > 0) _timestamps.RemoveRange(0, i);
    }

    public DateTimeOffset Oldest() =>
        _timestamps.Count > 0 ? _timestamps[0] : DateTimeOffset.UtcNow;
}

// ─── Policy Registry ─────────────────────────────────────────────────────────

/// <summary>
/// Associates route patterns and dimensions with their RateLimitPolicy.
/// Example: registry.Register("/api/agent/run", RateLimitDimension.User, new(20, TimeSpan.FromMinutes(1)))
/// </summary>
public sealed class RateLimitPolicyRegistry
{
    private readonly Dictionary<(string Route, RateLimitDimension Dimension), RateLimitPolicy> _policies = new();

    public RateLimitPolicyRegistry Register(string route, RateLimitDimension dimension, RateLimitPolicy policy)
    {
        _policies[(route, dimension)] = policy;
        return this;
    }

    public RateLimitPolicy? Find(string route, RateLimitDimension dimension) =>
        _policies.TryGetValue((route, dimension), out var p) ? p : null;

    public IReadOnlyDictionary<(string Route, RateLimitDimension Dimension), RateLimitPolicy> All => _policies;
}
