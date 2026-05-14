using AGXIOME.Shared.Models;
using System.Collections.Concurrent;

namespace AGXIOME.Core.LLM;

// ─────────────────────────────────────────────────────────────────────────────
// ResponseCache — CE-06
// TTL-based LLM response cache. Strategy is enum-driven (CacheStrategy).
// InMemory: ConcurrentDictionary with periodic expiry sweep.
// Redis: pluggable via ICacheBackend (injected, not instantiated here).
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Configuration for the response cache.</summary>
public record ResponseCacheOptions
{
    public CacheStrategy Strategy { get; init; } = CacheStrategy.InMemory;
    public TimeSpan DefaultTtl { get; init; } = TimeSpan.FromMinutes(15);
    /// <summary>Maximum in-memory entries. LRU eviction kicks in when exceeded.</summary>
    public int MaxEntries { get; init; } = 1000;
}

/// <summary>A single cached response entry with expiry metadata.</summary>
public record CacheEntry(string Response, DateTimeOffset ExpiresAt)
{
    public bool IsExpired => DateTimeOffset.UtcNow > ExpiresAt;
}

/// <summary>Optional backend for distributed caching (e.g. Redis). Default impl is no-op (in-memory only).</summary>
public interface ICacheBackend
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task SetAsync(string key, string value, TimeSpan ttl, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
}

/// <summary>LLM response cache. Cache key = SHA-256(provider + model + prompt).
/// Thread-safe. Supports None, InMemory, Redis, and Hybrid strategies via <see cref="CacheStrategy"/> enum.</summary>
public sealed class ResponseCache
{
    private readonly ResponseCacheOptions _opts;
    private readonly ICacheBackend? _backend;
    private readonly ConcurrentDictionary<string, CacheEntry> _memory = new();

    public ResponseCache(ResponseCacheOptions? options = null, ICacheBackend? backend = null)
    {
        _opts    = options ?? new ResponseCacheOptions();
        _backend = backend;
    }

    /// <summary>Try to retrieve a cached response. Returns null on cache miss or expired entry.</summary>
    public async Task<string?> GetAsync(
        LLMProviderType provider,
        string model,
        string prompt,
        CancellationToken ct = default)
    {
        if (_opts.Strategy == CacheStrategy.None) return null;

        var key = ComputeKey(provider, model, prompt);

        // L1: in-memory
        if (_memory.TryGetValue(key, out var entry))
        {
            if (!entry.IsExpired) return entry.Response;
            _memory.TryRemove(key, out _);
        }

        // L2: remote backend (Redis or equivalent)
        if (_opts.Strategy is CacheStrategy.Redis or CacheStrategy.Hybrid && _backend is not null)
            return await _backend.GetAsync(key, ct);

        return null;
    }

    /// <summary>Store a response in the cache with default TTL.</summary>
    public async Task SetAsync(
        LLMProviderType provider,
        string model,
        string prompt,
        string response,
        TimeSpan? ttl = null,
        CancellationToken ct = default)
    {
        if (_opts.Strategy == CacheStrategy.None) return;

        var key      = ComputeKey(provider, model, prompt);
        var duration = ttl ?? _opts.DefaultTtl;
        var expiry   = DateTimeOffset.UtcNow.Add(duration);

        if (_opts.Strategy is CacheStrategy.InMemory or CacheStrategy.Hybrid)
        {
            // Simple LRU eviction: remove oldest entry if at capacity.
            if (_memory.Count >= _opts.MaxEntries)
                EvictOldest();
            _memory[key] = new CacheEntry(response, expiry);
        }

        if (_opts.Strategy is CacheStrategy.Redis or CacheStrategy.Hybrid && _backend is not null)
            await _backend.SetAsync(key, response, duration, ct);
    }

    /// <summary>Explicitly invalidate a cached entry.</summary>
    public async Task InvalidateAsync(
        LLMProviderType provider,
        string model,
        string prompt,
        CancellationToken ct = default)
    {
        var key = ComputeKey(provider, model, prompt);
        _memory.TryRemove(key, out _);
        if (_backend is not null)
            await _backend.RemoveAsync(key, ct);
    }

    /// <summary>Remove all expired in-memory entries. Call periodically from a background timer.</summary>
    public void PurgeExpired()
    {
        foreach (var (k, v) in _memory)
            if (v.IsExpired) _memory.TryRemove(k, out _);
    }

    public int MemoryCount => _memory.Count;
    public CacheStrategy Strategy => _opts.Strategy;

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Deterministic SHA-256 key. Provider is enum-based — no magic strings.</summary>
    private static string ComputeKey(LLMProviderType provider, string model, string prompt)
    {
        var raw = $"{(int)provider}|{model.ToLowerInvariant()}|{prompt}";
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private void EvictOldest()
    {
        // Find the oldest entry and remove it (O(n) scan acceptable for MaxEntries<=1000).
        var oldest = _memory
            .OrderBy(kv => kv.Value.ExpiresAt)
            .Select(kv => kv.Key)
            .FirstOrDefault();
        if (oldest is not null) _memory.TryRemove(oldest, out _);
    }
}
