using AGXIOME.Shared.Interfaces;
using StackExchange.Redis;
using System.Text.Json;

namespace AGXIOME.Storage.Stores;

/// <summary>Redis cache: read-through, write-through, TTL, invalidation.</summary>
public sealed class RedisCache
{
    private readonly ConnectionMultiplexer _redis;
    private readonly IDatabase _db;

    public RedisCache(string connectionString)
    {
        var cfg = ConfigurationOptions.Parse(connectionString);
        cfg.AbortOnConnectFail = false;
        cfg.ConnectTimeout = 5000;
        cfg.SyncTimeout = 3000;
        _redis = ConnectionMultiplexer.Connect(cfg);
        _db = _redis.GetDatabase();
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        var val = await _db.StringGetAsync(key);
        return val.IsNullOrEmpty ? default : JsonSerializer.Deserialize<T>((string)val!);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null)
    {
        var json = JsonSerializer.Serialize(value);
        await _db.StringSetAsync(key, json, ttl ?? TimeSpan.FromMinutes(5));
    }

    public async Task InvalidateAsync(string pattern)
    {
        var server = _redis.GetServer(_redis.GetEndPoints().First());
        var keys = server.Keys(pattern: pattern).ToArray();
        if (keys.Length > 0) await _db.KeyDeleteAsync(keys);
    }

    public async Task<long> IncrementAsync(string key, TimeSpan? expiry = null)
    {
        var val = await _db.StringIncrementAsync(key, 1);
        if (expiry.HasValue) await _db.KeyExpireAsync(key, expiry.Value);
        return val;
    }
}
