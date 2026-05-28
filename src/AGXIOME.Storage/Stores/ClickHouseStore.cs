using System.Text.Json;
using ClickHouse.Client.ADO;
using ClickHouse.Client.Utility;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage.Stores;

// ─────────────────────────────────────────────────────────────────────────────
// DB-19 — ClickHouse Store
// Uses ClickHouse.Client. Async INSERT with ReplacingMergeTree engine target.
// Entities are inserted as JSON rows via the HTTP interface.
// ─────────────────────────────────────────────────────────────────────────────

public sealed record ClickHouseStoreOptions
{
    public required string ConnectionString { get; init; }
    /// <summary>Target database name.</summary>
    public required string Database { get; init; }
}

public sealed class ClickHouseCommandStore : ICommandStore
{
    private readonly ClickHouseStoreOptions _opts;

    public StorageProviderType ProviderType => StorageProviderType.ClickHouse;

    public ClickHouseCommandStore(ClickHouseStoreOptions opts)
        => _opts = opts ?? throw new ArgumentNullException(nameof(opts));

    public async Task SaveAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        await using var conn = new ClickHouseConnection(_opts.ConnectionString);
        var table = TableName<T>();
        // ClickHouse INSERT … VALUES via INSERT INTO table FORMAT JSONEachRow
        var json = JsonSerializer.Serialize(entity);
        var sql  = $"INSERT INTO {_opts.Database}.{table} FORMAT JSONEachRow {json}";
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ClickHouse is append-only by design; UPDATE is a mutation — use ALTER TABLE … UPDATE.
    public async Task UpdateAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        await using var conn = new ClickHouseConnection(_opts.ConnectionString);
        var table = TableName<T>();
        var props = typeof(T).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(p => p.Name != "Id");
        var set = string.Join(", ", props.Select(p =>
        {
            var v = p.GetValue(entity);
            var s = v is string str ? $"'{str.Replace("'", "\\'")}'"
                  : v?.ToString() ?? "NULL";
            return $"{p.Name} = {s}";
        }));
        var id = GetId(entity);
        var sql = $"ALTER TABLE {_opts.Database}.{table} UPDATE {set} WHERE Id = '{id}' SETTINGS mutations_sync = 1";
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync<T>(string id, CancellationToken ct = default) where T : class
    {
        await using var conn = new ClickHouseConnection(_opts.ConnectionString);
        var table = TableName<T>();
        var sql   = $"ALTER TABLE {_opts.Database}.{table} DELETE WHERE Id = '{id}' SETTINGS mutations_sync = 1";
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static string TableName<T>() => typeof(T).Name.ToLowerInvariant() + "s";

    private static string GetId<T>(T entity)
    {
        var prop = typeof(T).GetProperty("Id");
        return prop?.GetValue(entity)?.ToString() ?? string.Empty;
    }
}
