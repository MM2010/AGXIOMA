using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using Dapper;
using MySqlConnector;

namespace AGXIOME.Storage.Stores;

// ─────────────────────────────────────────────────────────────────────────────
// DB-06 — SingleStore
// MySQL-wire compatible; supports high-speed bulk insert via INSERT...VALUES batch.
// Uses MySqlConnector (fully async, cross-platform).
// ─────────────────────────────────────────────────────────────────────────────

public sealed record SingleStoreOptions
{
    public required string ConnectionString { get; init; }
    /// <summary>Max rows per bulk-insert batch (default 500).</summary>
    public int BulkBatchSize { get; init; } = 500;
}

public sealed class SingleStoreCommandStore : ICommandStore
{
    private readonly SingleStoreOptions _opts;

    public StorageProviderType ProviderType => StorageProviderType.SingleStore;

    public SingleStoreCommandStore(SingleStoreOptions opts)
        => _opts = opts ?? throw new ArgumentNullException(nameof(opts));

    public async Task SaveAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        await using var conn = new MySqlConnection(_opts.ConnectionString);
        // INSERT ... ON DUPLICATE KEY UPDATE — SingleStore/MySQL idempotent upsert.
        await conn.ExecuteAsync(BuildUpsertSql<T>(), entity, commandTimeout: 30);
    }

    public async Task UpdateAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        await using var conn = new MySqlConnection(_opts.ConnectionString);
        await conn.ExecuteAsync(BuildUpdateSql<T>(), entity, commandTimeout: 30);
    }

    public async Task DeleteAsync<T>(string id, CancellationToken ct = default) where T : class
    {
        await using var conn = new MySqlConnection(_opts.ConnectionString);
        var table = TableName<T>();
        await conn.ExecuteAsync($"DELETE FROM `{table}` WHERE `Id` = @Id", new { Id = id }, commandTimeout: 30);
    }

    /// <summary>
    /// Bulk-insert a batch of entities using a single multi-value INSERT statement.
    /// Preferred over repeated SaveAsync for high-throughput ingestion.
    /// </summary>
    public async Task BulkSaveAsync<T>(IReadOnlyList<T> entities, CancellationToken ct = default) where T : class
    {
        if (entities.Count == 0) return;
        await using var conn = new MySqlConnection(_opts.ConnectionString);
        await conn.OpenAsync(ct);
        // Chunked into BulkBatchSize to avoid excessive packet size.
        foreach (var batch in entities.Chunk(_opts.BulkBatchSize))
        {
            await conn.ExecuteAsync(BuildUpsertSql<T>(), batch, commandTimeout: 60);
        }
    }

    private static string BuildUpsertSql<T>()
    {
        var props  = Props<T>();
        var table  = TableName<T>();
        var cols   = string.Join(", ", props.Select(p => $"`{p.Name}`"));
        var vals   = string.Join(", ", props.Select(p => "@" + p.Name));
        var update = string.Join(", ", props.Where(p => p.Name != "Id")
            .Select(p => $"`{p.Name}` = VALUES(`{p.Name}`)"));
        return $"INSERT INTO `{table}` ({cols}) VALUES ({vals}) ON DUPLICATE KEY UPDATE {update}";
    }

    private static string BuildUpdateSql<T>()
    {
        var set = string.Join(", ", Props<T>().Where(p => p.Name != "Id")
            .Select(p => $"`{p.Name}` = @{p.Name}"));
        return $"UPDATE `{TableName<T>()}` SET {set} WHERE `Id` = @Id";
    }

    private static System.Reflection.PropertyInfo[] Props<T>() =>
        typeof(T).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

    private static string TableName<T>() => typeof(T).Name.ToLowerInvariant() + "s";

    public async Task<bool> HealthCheckAsync(CancellationToken ct = default)
    {
        try
        {
            await using var conn = new MySqlConnection(_opts.ConnectionString);
            await conn.OpenAsync(ct);
            await conn.ExecuteScalarAsync<int>("SELECT 1");
            return true;
        }
        catch { return false; }
    }
}
