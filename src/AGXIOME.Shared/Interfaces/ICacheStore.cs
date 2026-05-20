namespace AGXIOME.Shared.Interfaces;

/// <summary>
/// Minimal key-value cache abstraction for pointer cache and checkpoint store.
///
/// Implementations: RedisCacheStore (AGXIOME.Storage), InMemoryCacheStore, etc.
/// The interface is intentionally minimal — just Get/Set/Delete.
///
/// This keeps AGXIOME.Core free from a hard dependency on StackExchange.Redis.
/// The actual Redis implementation lives in AGXIOME.Storage.
/// </summary>
public interface ICacheStore
{
    /// <summary>
    /// Get a value by key. Returns null if not found.
    /// </summary>
    Task<string?> GetAsync(string key);

    /// <summary>
    /// Set a value with optional TTL.
    /// </summary>
    Task<bool> SetAsync(string key, string value, TimeSpan? ttl = null);

    /// <summary>
    /// Delete a key.
    /// </summary>
    Task<bool> DeleteAsync(string key);
}
