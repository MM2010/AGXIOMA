using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace AGXIOME.Storage.Stores;

// ─────────────────────────────────────────────────────────────────────────────
// Shared SQL Server command store base.
// DB-02 — SQL Server (enterprise)
// DB-03 — SQL Server LocalDB (dev mode)
// Both use Microsoft.Data.SqlClient + Dapper with identical SQL dialect.
// ─────────────────────────────────────────────────────────────────────────────

public abstract class SqlServerCommandStoreBase : ICommandStore
{
    protected abstract string ConnectionString { get; }
    public abstract StorageProviderType ProviderType { get; }

    public async Task SaveAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.ExecuteAsync(BuildMergeSql<T>(), entity, commandTimeout: 30);
    }

    public async Task UpdateAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.ExecuteAsync(BuildUpdateSql<T>(), entity, commandTimeout: 30);
    }

    public async Task DeleteAsync<T>(string id, CancellationToken ct = default) where T : class
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.ExecuteAsync(
            $"DELETE FROM [{TableName<T>()}] WHERE Id = @Id", new { Id = id }, commandTimeout: 30);
    }

    // MERGE (SQL Server UPSERT) — idempotent on Id collision.
    private static string BuildMergeSql<T>()
    {
        var props = Props<T>();
        var table = TableName<T>();
        var cols  = string.Join(", ", props.Select(p => $"[{p.Name}]"));
        var vals  = string.Join(", ", props.Select(p => "@" + p.Name));
        var set   = string.Join(", ", props.Where(p => p.Name != "Id")
            .Select(p => $"[{p.Name}] = @{p.Name}"));
        return $"""
            MERGE [{table}] AS target
            USING (SELECT {vals}) AS source ({cols})
            ON target.Id = source.Id
            WHEN MATCHED THEN UPDATE SET {set}
            WHEN NOT MATCHED THEN INSERT ({cols}) VALUES ({vals});
            """;
    }

    private static string BuildUpdateSql<T>()
    {
        var set   = string.Join(", ", Props<T>().Where(p => p.Name != "Id")
            .Select(p => $"[{p.Name}] = @{p.Name}"));
        return $"UPDATE [{TableName<T>()}] SET {set} WHERE [Id] = @Id";
    }

    private static System.Reflection.PropertyInfo[] Props<T>() =>
        typeof(T).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

    private static string TableName<T>() => typeof(T).Name + "s";
}

// ─────────────────────────────────────────────────────────────────────────────
// DB-02 — SQL Server Store
// ─────────────────────────────────────────────────────────────────────────────

public sealed record SqlServerStoreOptions
{
    public required string ConnectionString { get; init; }
}

public sealed class SqlServerCommandStore : SqlServerCommandStoreBase
{
    private readonly SqlServerStoreOptions _opts;
    protected override string ConnectionString => _opts.ConnectionString;
    public override StorageProviderType ProviderType => StorageProviderType.SQLServer;

    public SqlServerCommandStore(SqlServerStoreOptions opts)
        => _opts = opts ?? throw new ArgumentNullException(nameof(opts));
}

// ─────────────────────────────────────────────────────────────────────────────
// DB-03 — SQL Server LocalDB Store
// Same dialect as SQL Server; connection string uses `(localdb)\...` format.
// ─────────────────────────────────────────────────────────────────────────────

public sealed record SqlServerLocalDbStoreOptions
{
    /// <summary>E.g. "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=AppDb;Integrated Security=True"</summary>
    public required string ConnectionString { get; init; }
}

public sealed class SqlServerLocalDbCommandStore : SqlServerCommandStoreBase
{
    private readonly SqlServerLocalDbStoreOptions _opts;
    protected override string ConnectionString => _opts.ConnectionString;
    public override StorageProviderType ProviderType => StorageProviderType.SQLServerLocalDB;

    public SqlServerLocalDbCommandStore(SqlServerLocalDbStoreOptions opts)
        => _opts = opts ?? throw new ArgumentNullException(nameof(opts));
}
