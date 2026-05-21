using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using Microsoft.Data.Sqlite;
using Dapper;

namespace AGXIOME.Storage.Stores;

// ─────────────────────────────────────────────────────────────────────────────
// DB-01 — SQLite Store
// Uses Microsoft.Data.Sqlite + Dapper. Schema is auto-created from entity type
// on first use (development / embedded mode).
// ─────────────────────────────────────────────────────────────────────────────

public sealed record SqliteStoreOptions
{
    public required string ConnectionString { get; init; }
    /// <summary>When true, CREATE TABLE IF NOT EXISTS runs on first Upsert.</summary>
    public bool AutoCreateSchema { get; init; } = true;
}

public sealed class SqliteCommandStore : ICommandStore, IDisposable
{
    private readonly SqliteStoreOptions _opts;

    public StorageProviderType ProviderType => StorageProviderType.SQLite;

    public SqliteCommandStore(SqliteStoreOptions opts)
    {
        _opts = opts ?? throw new ArgumentNullException(nameof(opts));
        ArgumentException.ThrowIfNullOrWhiteSpace(_opts.ConnectionString);
    }

    public async Task SaveAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        await using var conn = new SqliteConnection(_opts.ConnectionString);
        if (_opts.AutoCreateSchema) await EnsureTableAsync<T>(conn, ct);
        await conn.ExecuteAsync(BuildInsertSql<T>(), entity, commandTimeout: 30);
    }

    public async Task UpdateAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        await using var conn = new SqliteConnection(_opts.ConnectionString);
        await conn.ExecuteAsync(BuildUpdateSql<T>(), entity, commandTimeout: 30);
    }

    public async Task DeleteAsync<T>(string id, CancellationToken ct = default) where T : class
    {
        await using var conn = new SqliteConnection(_opts.ConnectionString);
        var table = TableName<T>();
        await conn.ExecuteAsync($"DELETE FROM \"{table}\" WHERE Id = @Id", new { Id = id }, commandTimeout: 30);
    }

    // ── Schema helpers ────────────────────────────────────────────────────────

    private static async Task EnsureTableAsync<T>(SqliteConnection conn, CancellationToken ct)
    {
        var table = TableName<T>();
        var props = typeof(T).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        var cols  = string.Join(", ", props.Select(p =>
        {
            string type = p.PropertyType == typeof(int) || p.PropertyType == typeof(long) ? "INTEGER"
                        : p.PropertyType == typeof(double) || p.PropertyType == typeof(float) ? "REAL"
                        : "TEXT";
            return $"\"{p.Name}\" {type}";
        }));
        var ddl = $"CREATE TABLE IF NOT EXISTS \"{table}\" ({cols})";
        await conn.ExecuteAsync(ddl);
    }

    private static string BuildInsertSql<T>()
    {
        var props = typeof(T).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        var cols  = string.Join(", ", props.Select(p => $"\"{p.Name}\""));
        var vals  = string.Join(", ", props.Select(p => "@" + p.Name));
        return $"INSERT OR REPLACE INTO \"{TableName<T>()}\" ({cols}) VALUES ({vals})";
    }

    private static string BuildUpdateSql<T>()
    {
        var props = typeof(T).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(p => p.Name != "Id");
        var set = string.Join(", ", props.Select(p => $"\"{p.Name}\" = @{p.Name}"));
        return $"UPDATE \"{TableName<T>()}\" SET {set} WHERE \"Id\" = @Id";
    }

    private static string TableName<T>() => typeof(T).Name + "s";

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

    public void Dispose() { /* Connection per-call; nothing to dispose at store level. */ }
}
