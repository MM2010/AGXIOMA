using System.Text.Json;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using AGXIOME.Storage.Stores;
using Microsoft.Data.Sqlite;
using Dapper;
using Npgsql;
using StackExchange.Redis;

namespace AGXIOME.Storage.Session;

// ─────────────────────────────────────────────────────────────────────────────
// Provider-agnostic ISessionStore<T> adapters.
//
// Consumers inject ISessionStore<ConversationSession> (or any T).
// The concrete backend is chosen at DI registration time — completely
// invisible to the consumer.
//
// Adapters available:
//   InMemorySessionStore<T>  — no infrastructure, useful for dev/test
//   SqliteSessionStore<T>    — embedded file, ideal for local/dev
//   RedisSessionStoreAdapter<T> — distributed, ideal for production
//   PostgresSessionStore<T>  — relational, ideal for multi-tenant prod
//   JsonFileSessionStore<T>  — file-backed, zero dependencies
// ─────────────────────────────────────────────────────────────────────────────

// ── In-Memory ─────────────────────────────────────────────────────────────────

/// <summary>Volatile in-memory session store. Zero infrastructure requirements.
/// Suitable for dev, test, and single-instance scenarios.</summary>
public sealed class InMemorySessionStore<T> : ISessionStore<T> where T : class
{
    private readonly Dictionary<string, T> _store = new();
    private readonly object _lock = new();

    public Task SetAsync(string sessionId, T value, CancellationToken ct = default)
    {
        lock (_lock) _store[sessionId] = value;
        return Task.CompletedTask;
    }

    public Task<T?> GetAsync(string sessionId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _store.TryGetValue(sessionId, out var val);
            return Task.FromResult<T?>(val);
        }
    }

    public Task RemoveAsync(string sessionId, CancellationToken ct = default)
    {
        lock (_lock) _store.Remove(sessionId);
        return Task.CompletedTask;
    }

    public Task<bool> HealthCheckAsync(CancellationToken ct = default)
        => Task.FromResult(true);
}

// ── SQLite ────────────────────────────────────────────────────────────────────

public sealed record SqliteSessionStoreOptions
{
    public required string ConnectionString { get; init; }
    public string TableName { get; init; } = "agxiome_sessions";
}

/// <summary>SQLite-backed session store. File-embedded, zero-server, ideal for dev/edge.</summary>
public sealed class SqliteSessionStore<T> : ISessionStore<T>, IDisposable where T : class
{
    private readonly SqliteSessionStoreOptions _opts;
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = false };

    public SqliteSessionStore(SqliteSessionStoreOptions opts)
    {
        _opts = opts ?? throw new ArgumentNullException(nameof(opts));
        EnsureSchema();
    }

    private void EnsureSchema()
    {
        using var conn = new SqliteConnection(_opts.ConnectionString);
        conn.Execute($"""
            CREATE TABLE IF NOT EXISTS "{_opts.TableName}" (
                "Id"        TEXT NOT NULL PRIMARY KEY,
                "Data"      TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL
            )
            """);
    }

    public async Task SetAsync(string sessionId, T value, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_opts.ConnectionString);
        var data = JsonSerializer.Serialize(value, _json);
        await conn.ExecuteAsync(
            $"""
            INSERT INTO "{_opts.TableName}" ("Id","Data","UpdatedAt")
            VALUES (@Id, @Data, @UpdatedAt)
            ON CONFLICT("Id") DO UPDATE SET "Data"=excluded."Data", "UpdatedAt"=excluded."UpdatedAt"
            """,
            new { Id = sessionId, Data = data, UpdatedAt = DateTime.UtcNow.ToString("O") },
            commandTimeout: 30);
    }

    public async Task<T?> GetAsync(string sessionId, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_opts.ConnectionString);
        var data = await conn.ExecuteScalarAsync<string>(
            $"SELECT \"Data\" FROM \"{_opts.TableName}\" WHERE \"Id\" = @Id",
            new { Id = sessionId }, commandTimeout: 30);
        return data is null ? null : JsonSerializer.Deserialize<T>(data, _json);
    }

    public async Task RemoveAsync(string sessionId, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_opts.ConnectionString);
        await conn.ExecuteAsync(
            $"DELETE FROM \"{_opts.TableName}\" WHERE \"Id\" = @Id",
            new { Id = sessionId }, commandTimeout: 30);
    }

    public async Task<bool> HealthCheckAsync(CancellationToken ct = default)
    {
        try
        {
            await using var conn = new SqliteConnection(_opts.ConnectionString);
            await conn.OpenAsync(ct);
            return true;
        }
        catch { return false; }
    }

    public void Dispose() { }
}

// ── Redis ─────────────────────────────────────────────────────────────────────

public sealed record RedisSessionAdapterOptions
{
    public required string ConnectionString { get; init; }
    public string KeyPrefix { get; init; } = "agxioma:session";
    public TimeSpan? Expiry { get; init; }
}

/// <summary>Redis-backed session store. Distributed, low-latency, ideal for production scale-out.</summary>
public sealed class RedisSessionStoreAdapter<T> : ISessionStore<T>, IDisposable where T : class
{
    private readonly RedisSessionAdapterOptions _opts;
    private readonly ConnectionMultiplexer _mux;
    private readonly IDatabase _db;
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = false };

    public RedisSessionStoreAdapter(RedisSessionAdapterOptions opts)
    {
        _opts = opts ?? throw new ArgumentNullException(nameof(opts));
        _mux  = ConnectionMultiplexer.Connect(opts.ConnectionString);
        _db   = _mux.GetDatabase();
    }

    private RedisKey Key(string sessionId) => $"{_opts.KeyPrefix}:{typeof(T).Name}:{sessionId}";

    public async Task SetAsync(string sessionId, T value, CancellationToken ct = default)
    {
        var data = JsonSerializer.Serialize(value, _json);
        await _db.StringSetAsync(Key(sessionId), data,
            _opts.Expiry.HasValue ? new Expiration(_opts.Expiry.Value) : default(Expiration));
    }

    public async Task<T?> GetAsync(string sessionId, CancellationToken ct = default)
    {
        var data = await _db.StringGetAsync(Key(sessionId));
        return data.IsNullOrEmpty ? null : JsonSerializer.Deserialize<T>((string)data!, _json);
    }

    public async Task RemoveAsync(string sessionId, CancellationToken ct = default)
        => await _db.KeyDeleteAsync(Key(sessionId));

    public async Task<bool> HealthCheckAsync(CancellationToken ct = default)
    {
        try { return await _db.PingAsync() >= TimeSpan.Zero; }
        catch { return false; }
    }

    public void Dispose() => _mux.Dispose();
}

// ── PostgreSQL ────────────────────────────────────────────────────────────────

public sealed record PostgresSessionStoreOptions
{
    public required string ConnectionString { get; init; }
    public string TableName { get; init; } = "agxiome_sessions";
    public string SchemaName { get; init; } = "public";
}

/// <summary>PostgreSQL-backed session store. Durable, multi-tenant, full ACID.</summary>
public sealed class PostgresSessionStore<T> : ISessionStore<T> where T : class
{
    private readonly PostgresSessionStoreOptions _opts;
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = false };

    public PostgresSessionStore(PostgresSessionStoreOptions opts)
    {
        _opts = opts ?? throw new ArgumentNullException(nameof(opts));
        EnsureSchemaAsync().GetAwaiter().GetResult();
    }

    private async Task EnsureSchemaAsync()
    {
        await using var conn = new NpgsqlConnection(_opts.ConnectionString);
        await conn.ExecuteAsync($"""
            CREATE TABLE IF NOT EXISTS "{_opts.SchemaName}"."{_opts.TableName}" (
                "Id"        TEXT NOT NULL PRIMARY KEY,
                "Data"      TEXT NOT NULL,
                "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT now()
            )
            """);
    }

    public async Task SetAsync(string sessionId, T value, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_opts.ConnectionString);
        var data = JsonSerializer.Serialize(value, _json);
        await conn.ExecuteAsync(
            $"""
            INSERT INTO "{_opts.SchemaName}"."{_opts.TableName}" ("Id","Data","UpdatedAt")
            VALUES (@Id, @Data::jsonb, now())
            ON CONFLICT("Id") DO UPDATE SET "Data"=EXCLUDED."Data", "UpdatedAt"=now()
            """,
            new { Id = sessionId, Data = data }, commandTimeout: 30);
    }

    public async Task<T?> GetAsync(string sessionId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_opts.ConnectionString);
        var data = await conn.ExecuteScalarAsync<string>(
            $"SELECT \"Data\" FROM \"{_opts.SchemaName}\".\"{_opts.TableName}\" WHERE \"Id\" = @Id",
            new { Id = sessionId }, commandTimeout: 30);
        return data is null ? null : JsonSerializer.Deserialize<T>(data, _json);
    }

    public async Task RemoveAsync(string sessionId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_opts.ConnectionString);
        await conn.ExecuteAsync(
            $"DELETE FROM \"{_opts.SchemaName}\".\"{_opts.TableName}\" WHERE \"Id\" = @Id",
            new { Id = sessionId }, commandTimeout: 30);
    }

    public async Task<bool> HealthCheckAsync(CancellationToken ct = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_opts.ConnectionString);
            await conn.OpenAsync(ct);
            await conn.ExecuteScalarAsync<int>("SELECT 1");
            return true;
        }
        catch { return false; }
    }
}

// ── JSON File ─────────────────────────────────────────────────────────────────

public sealed record JsonFileSessionStoreOptions
{
    public required string BaseDirectory { get; init; }
}

/// <summary>JSON-file-backed session store. Zero infrastructure, suitable for offline/dev scenarios.</summary>
public sealed class JsonFileSessionStore<T> : ISessionStore<T> where T : class
{
    private readonly JsonFileSessionStoreOptions _opts;
    private readonly ReaderWriterLockSlim _lock = new();
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = false };

    public JsonFileSessionStore(JsonFileSessionStoreOptions opts)
    {
        _opts = opts ?? throw new ArgumentNullException(nameof(opts));
        Directory.CreateDirectory(opts.BaseDirectory);
    }

    private string FilePath(string sessionId)
        => Path.Combine(_opts.BaseDirectory,
               $"{typeof(T).Name.ToLowerInvariant()}_{sessionId.Replace('/', '_')}.json");

    public Task SetAsync(string sessionId, T value, CancellationToken ct = default)
    {
        _lock.EnterWriteLock();
        try
        {
            var path = FilePath(sessionId);
            var tmp  = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(value, _json));
            File.Move(tmp, path, overwrite: true);
        }
        finally { _lock.ExitWriteLock(); }
        return Task.CompletedTask;
    }

    public Task<T?> GetAsync(string sessionId, CancellationToken ct = default)
    {
        _lock.EnterReadLock();
        try
        {
            var path = FilePath(sessionId);
            if (!File.Exists(path)) return Task.FromResult<T?>(null);
            var json = File.ReadAllText(path);
            return Task.FromResult(JsonSerializer.Deserialize<T>(json, _json));
        }
        finally { _lock.ExitReadLock(); }
    }

    public Task RemoveAsync(string sessionId, CancellationToken ct = default)
    {
        _lock.EnterWriteLock();
        try { if (File.Exists(FilePath(sessionId))) File.Delete(FilePath(sessionId)); }
        finally { _lock.ExitWriteLock(); }
        return Task.CompletedTask;
    }

    public Task<bool> HealthCheckAsync(CancellationToken ct = default)
        => Task.FromResult(Directory.Exists(_opts.BaseDirectory));
}
