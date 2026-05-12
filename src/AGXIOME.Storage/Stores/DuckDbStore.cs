using System.Text.Json;
using DuckDB.NET.Data;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage.Stores;

// ─────────────────────────────────────────────────────────────────────────────
// DB-22 — DuckDB Store
// Uses DuckDB.NET. Embedded analytical store. Parquet export supported via
// COPY TO statement. Schema auto-created from entity type.
// ─────────────────────────────────────────────────────────────────────────────

public sealed record DuckDbStoreOptions
{
    /// <summary>File path for persistent DuckDB, or ":memory:" for in-process.</summary>
    public required string DataSource { get; init; }
    public bool AutoCreateSchema { get; init; } = true;
}

public sealed class DuckDbCommandStore : ICommandStore, IDisposable
{
    private readonly DuckDbStoreOptions _opts;
    private readonly DuckDBConnection   _conn;

    public StorageProviderType ProviderType => StorageProviderType.DuckDB;

    public DuckDbCommandStore(DuckDbStoreOptions opts)
    {
        _opts = opts ?? throw new ArgumentNullException(nameof(opts));
        _conn = new DuckDBConnection($"DataSource={opts.DataSource}");
        _conn.Open();
    }

    public async Task SaveAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        if (_opts.AutoCreateSchema) EnsureTable<T>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = BuildInsertSql(entity);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = BuildUpdateSql(entity);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync<T>(string id, CancellationToken ct = default) where T : class
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = $"DELETE FROM \"{TableName<T>()}\" WHERE \"Id\" = '{EscapeStr(id)}'";
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private void EnsureTable<T>()
    {
        var props = Props<T>();
        var cols  = string.Join(", ", props.Select(p =>
        {
            var type = p.PropertyType == typeof(int) || p.PropertyType == typeof(long) ? "BIGINT"
                     : p.PropertyType == typeof(double) || p.PropertyType == typeof(float) ? "DOUBLE"
                     : p.PropertyType == typeof(bool) ? "BOOLEAN"
                     : "VARCHAR";
            // Id is PRIMARY KEY so ON CONFLICT(Id) works for upserts.
            var pk = p.Name == "Id" ? " PRIMARY KEY" : string.Empty;
            return $"\"{p.Name}\" {type}{pk}";
        }));
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = $"CREATE TABLE IF NOT EXISTS \"{TableName<T>()}\" ({cols})";
        cmd.ExecuteNonQuery();
    }

    private static string BuildInsertSql<T>(T entity)
    {
        var props   = Props<T>();
        var cols    = string.Join(", ", props.Select(p => $"\"{p.Name}\""));
        var vals    = string.Join(", ", props.Select(p => ToSqlLiteral(p.GetValue(entity))));
        var updates = string.Join(", ", props.Where(p => p.Name != "Id").Select(p => $"\"{p.Name}\" = excluded.\"{p.Name}\""));
        // ON CONFLICT upsert — requires Id PRIMARY KEY declared in EnsureTable.
        return $"INSERT INTO \"{TableName<T>()}\" ({cols}) VALUES ({vals}) ON CONFLICT(\"Id\") DO UPDATE SET {updates}";
    }

    private static string BuildUpdateSql<T>(T entity)
    {
        var props = Props<T>().Where(p => p.Name != "Id");
        var set   = string.Join(", ", props.Select(p => $"\"{p.Name}\" = {ToSqlLiteral(p.GetValue(entity))}"));
        var id    = typeof(T).GetProperty("Id")?.GetValue(entity)?.ToString() ?? string.Empty;
        return $"UPDATE \"{TableName<T>()}\" SET {set} WHERE \"Id\" = '{EscapeStr(id)}'";
    }

    private static string ToSqlLiteral(object? val) => val switch
    {
        null         => "NULL",
        string  s    => $"'{EscapeStr(s)}'",
        bool    b    => b ? "TRUE" : "FALSE",
        int     i    => i.ToString(),
        long    l    => l.ToString(),
        double  d    => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
        float   f    => f.ToString(System.Globalization.CultureInfo.InvariantCulture),
        _            => $"'{EscapeStr(JsonSerializer.Serialize(val))}'"
    };

    private static string EscapeStr(string s) => s.Replace("'", "''");

    private static System.Reflection.PropertyInfo[] Props<T>() =>
        typeof(T).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

    private static string TableName<T>() => typeof(T).Name.ToLowerInvariant() + "s";

    public void Dispose() => _conn.Dispose();
}
