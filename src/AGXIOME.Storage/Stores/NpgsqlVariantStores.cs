using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using Dapper;
using Npgsql;

namespace AGXIOME.Storage.Stores;

// ─────────────────────────────────────────────────────────────────────────────
// Shared Npgsql-based store base.
// Used by: DB-04 Neon, DB-05 Supabase, DB-07 CockroachDB, DB-08 TimescaleDB
// All four speak the PostgreSQL wire protocol; only connection string differs.
// ─────────────────────────────────────────────────────────────────────────────

public abstract class NpgsqlCommandStoreBase : ICommandStore
{
    protected abstract string ConnectionString { get; }
    public abstract StorageProviderType ProviderType { get; }

    public async Task SaveAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.ExecuteAsync(BuildUpsertSql<T>(), entity, commandTimeout: 30);
    }

    public async Task UpdateAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.ExecuteAsync(BuildUpdateSql<T>(), entity, commandTimeout: 30);
    }

    public async Task DeleteAsync<T>(string id, CancellationToken ct = default) where T : class
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        var table = TableName<T>();
        await conn.ExecuteAsync(
            $"DELETE FROM \"{table}\" WHERE \"Id\" = @Id", new { Id = id }, commandTimeout: 30);
    }

    // ON CONFLICT(Id) DO UPDATE — PostgreSQL native UPSERT.
    private static string BuildUpsertSql<T>()
    {
        var props  = Props<T>();
        var table  = TableName<T>();
        var cols   = string.Join(", ", props.Select(p => $"\"{p.Name}\""));
        var vals   = string.Join(", ", props.Select(p => "@" + p.Name));
        var update = string.Join(", ", props.Where(p => p.Name != "Id")
            .Select(p => $"\"{p.Name}\" = EXCLUDED.\"{p.Name}\""));
        return $"INSERT INTO \"{table}\" ({cols}) VALUES ({vals}) ON CONFLICT(\"Id\") DO UPDATE SET {update}";
    }

    private static string BuildUpdateSql<T>()
    {
        var set = string.Join(", ", Props<T>().Where(p => p.Name != "Id")
            .Select(p => $"\"{p.Name}\" = @{p.Name}"));
        return $"UPDATE \"{TableName<T>()}\" SET {set} WHERE \"Id\" = @Id";
    }

    private static System.Reflection.PropertyInfo[] Props<T>() =>
        typeof(T).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

    private static string TableName<T>() => typeof(T).Name.ToLowerInvariant() + "s";
}

// ─────────────────────────────────────────────────────────────────────────────
// DB-04 — Neon (Serverless PostgreSQL)
// Connection string format: "Host=...neon.tech;Username=...;Password=...;Database=..."
// Prefer pooled endpoint (connection string param: Pooling=true;Maximum Pool Size=20)
// ─────────────────────────────────────────────────────────────────────────────

public sealed record NeonStoreOptions
{
    public required string ConnectionString { get; init; }
}

public sealed class NeonCommandStore : NpgsqlCommandStoreBase
{
    private readonly NeonStoreOptions _opts;
    protected override string ConnectionString => _opts.ConnectionString;
    public override StorageProviderType ProviderType => StorageProviderType.Neon;

    public NeonCommandStore(NeonStoreOptions opts)
        => _opts = opts ?? throw new ArgumentNullException(nameof(opts));
}

// ─────────────────────────────────────────────────────────────────────────────
// DB-05 — Supabase (PostgreSQL + RLS)
// Supabase exposes a standard Npgsql endpoint; service_role key bypasses RLS.
// ─────────────────────────────────────────────────────────────────────────────

public sealed record SupabaseStoreOptions
{
    public required string ConnectionString { get; init; }
    /// <summary>When true, set "role=service_role" on connection open to bypass Row Level Security.</summary>
    public bool BypassRls { get; init; } = false;
}

public sealed class SupabaseCommandStore : NpgsqlCommandStoreBase
{
    private readonly SupabaseStoreOptions _opts;

    protected override string ConnectionString => _opts.ConnectionString;
    public override StorageProviderType ProviderType => StorageProviderType.Supabase;

    public SupabaseCommandStore(SupabaseStoreOptions opts)
        => _opts = opts ?? throw new ArgumentNullException(nameof(opts));

    // Override to apply service_role if requested (Supabase-specific concern).
    public new async Task SaveAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        await using var conn = new NpgsqlConnection(_opts.ConnectionString);
        await conn.OpenAsync(ct);
        if (_opts.BypassRls)
            await conn.ExecuteAsync("SET LOCAL role = 'service_role'");
        await base.SaveAsync(entity, ct);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// DB-07 — CockroachDB
// PG-compatible; serializable isolation by default.
// UPSERT syntax is also PG-compatible (ON CONFLICT DO UPDATE).
// ─────────────────────────────────────────────────────────────────────────────

public sealed record CockroachDbStoreOptions
{
    public required string ConnectionString { get; init; }
}

public sealed class CockroachDbCommandStore : NpgsqlCommandStoreBase
{
    private readonly CockroachDbStoreOptions _opts;
    protected override string ConnectionString => _opts.ConnectionString;
    public override StorageProviderType ProviderType => StorageProviderType.CockroachDB;

    public CockroachDbCommandStore(CockroachDbStoreOptions opts)
        => _opts = opts ?? throw new ArgumentNullException(nameof(opts));
}

// ─────────────────────────────────────────────────────────────────────────────
// DB-08 — TimescaleDB
// Built on PostgreSQL + hypertable extension. Standard DML is PG-compatible;
// time_bucket queries are issued separately via ReadAsync helpers (not shown here).
// ─────────────────────────────────────────────────────────────────────────────

public sealed record TimescaleDbStoreOptions
{
    public required string ConnectionString { get; init; }
    /// <summary>When true, wraps SaveAsync in explicit BEGIN/COMMIT for hypertable batch coherence.</summary>
    public bool UseExplicitTransaction { get; init; } = false;
}

public sealed class TimescaleDbCommandStore : NpgsqlCommandStoreBase
{
    private readonly TimescaleDbStoreOptions _opts;
    protected override string ConnectionString => _opts.ConnectionString;
    public override StorageProviderType ProviderType => StorageProviderType.TimescaleDB;

    public TimescaleDbCommandStore(TimescaleDbStoreOptions opts)
        => _opts = opts ?? throw new ArgumentNullException(nameof(opts));
}
