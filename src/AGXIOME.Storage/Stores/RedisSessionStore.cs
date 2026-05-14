using System.Text.Json;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using StackExchange.Redis;

namespace AGXIOME.Storage.Stores;

// ─────────────────────────────────────────────────────────────────────────────
// DB-15 — Redis Session Store
// Uses StackExchange.Redis. Stores serialized JSON with configurable TTL.
// Key format: "{KeyPrefix}:{EntityType}:{Id}"
// ─────────────────────────────────────────────────────────────────────────────

public sealed record RedisSessionStoreOptions
{
    /// <summary>Redis connection string. Default dev: "localhost:6379,abortConnect=false"</summary>
    public required string ConnectionString { get; init; }
    /// <summary>Key namespace prefix (e.g., "agxioma:session").</summary>
    public string KeyPrefix { get; init; } = "agxioma:session";
    /// <summary>TTL for stored entries. Null = no expiry.</summary>
    public TimeSpan? Expiry { get; init; }
}

public sealed class RedisSessionStore : ICommandStore, IDisposable
{
    private readonly RedisSessionStoreOptions _opts;
    private readonly IDatabase _db;
    private readonly ConnectionMultiplexer _mux;

    public StorageProviderType ProviderType => StorageProviderType.RedisSession;

    private static readonly JsonSerializerOptions _json = new() { WriteIndented = false };

    public RedisSessionStore(RedisSessionStoreOptions opts)
    {
        _opts = opts ?? throw new ArgumentNullException(nameof(opts));
        _mux  = ConnectionMultiplexer.Connect(opts.ConnectionString);
        _db   = _mux.GetDatabase();
    }

    public async Task SaveAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        var key   = BuildKey<T>(GetId(entity));
        var value = JsonSerializer.Serialize(entity, _json);
        await _db.StringSetAsync(key, value, _opts.Expiry);
    }

    public async Task UpdateAsync<T>(T entity, CancellationToken ct = default) where T : class
        => await SaveAsync(entity, ct);  // SET is idempotent in Redis.

    public async Task DeleteAsync<T>(string id, CancellationToken ct = default) where T : class
    {
        var key = BuildKey<T>(id);
        await _db.KeyDeleteAsync(key);
    }

    private RedisKey BuildKey<T>(string id) => $"{_opts.KeyPrefix}:{typeof(T).Name}:{id}";

    private static string GetId<T>(T entity)
    {
        var prop = typeof(T).GetProperty("Id");
        return prop?.GetValue(entity)?.ToString() ?? Guid.NewGuid().ToString();
    }

    public void Dispose() => _mux.Dispose();
}
