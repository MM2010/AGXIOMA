using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using Dapper;
using Npgsql;
using System.Text.Json;

namespace AGXIOME.Storage.VectorStores;

// ─────────────────────────────────────────────────────────────────────────────
// VD-06..VD-09 — pgvector variants for cloud-managed PostgreSQL services.
//
// Supabase, Neon, Timescale, and CockroachDB all expose a standard PostgreSQL
// wire protocol with pgvector extension support.  The only differences between
// providers are connection-string format and ProviderType label; the underlying
// SQL is 100% identical to PgVectorStore.
//
// We extract the shared logic into an internal abstract base so the four
// concrete stores are trivial one-liners without any code duplication.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Shared Npgsql+pgvector implementation used by all pgvector-compatible variant stores.</summary>
public abstract class NpgsqlPgVectorBase : IVectorStore
{
    private readonly string _connectionString;

    public abstract VectorDbProviderType ProviderType { get; }

    protected NpgsqlPgVectorBase(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    public async Task EnsureCollectionAsync(string collection, int dimensions, CancellationToken ct = default)
    {
        ValidateCollection(collection);
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await conn.ExecuteAsync("CREATE EXTENSION IF NOT EXISTS vector;");
        var sql = $@"CREATE TABLE IF NOT EXISTS {collection} (
            id           TEXT    NOT NULL PRIMARY KEY,
            vector       vector({dimensions}) NOT NULL,
            text_content TEXT,
            metadata     JSONB
        );";
        await conn.ExecuteAsync(sql);
    }

    public async Task DeleteCollectionAsync(string collection, CancellationToken ct = default)
    {
        ValidateCollection(collection);
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await conn.ExecuteAsync($"DROP TABLE IF EXISTS {collection};");
    }

    public async Task<IReadOnlyList<string>> ListCollectionsAsync(CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        var names = await conn.QueryAsync<string>(@"
            SELECT DISTINCT table_name
            FROM information_schema.columns
            WHERE udt_name = 'vector'
              AND table_schema = 'public'
            ORDER BY table_name;");
        return names.ToList();
    }

    public async Task UpsertAsync(string collection, VectorRecord record, CancellationToken ct = default)
    {
        ValidateCollection(collection);
        var vecStr = FormatVector(record.Vector);
        var metaJson = record.Metadata is null ? null : JsonSerializer.Serialize(record.Metadata);
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await conn.ExecuteAsync(
            $@"INSERT INTO {collection} (id, vector, text_content, metadata)
               VALUES (@Id, @Vec::vector, @Text, @Meta::jsonb)
               ON CONFLICT (id) DO UPDATE
                 SET vector = EXCLUDED.vector,
                     text_content = EXCLUDED.text_content,
                     metadata = EXCLUDED.metadata;",
            new { Id = record.Id, Vec = vecStr, Text = record.Text, Meta = metaJson },
            commandTimeout: 30);
    }

    public async Task UpsertBatchAsync(string collection, IReadOnlyList<VectorRecord> records, CancellationToken ct = default)
    {
        foreach (var record in records)
            await UpsertAsync(collection, record, ct);
    }

    public async Task<VectorRecord?> GetAsync(string collection, string id, CancellationToken ct = default)
    {
        ValidateCollection(collection);
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        var row = await conn.QueryFirstOrDefaultAsync<(string Id, string Vec, string? Text, string? Meta)>(
            $"SELECT id, vector::text, text_content, metadata FROM {collection} WHERE id = @Id;",
            new { Id = id }, commandTimeout: 30);
        if (row == default) return null;
        return new VectorRecord(
            row.Id,
            ParseVector(row.Vec),
            row.Text,
            row.Meta is null ? null : JsonSerializer.Deserialize<Dictionary<string, object>>(row.Meta));
    }

    public async Task DeleteAsync(string collection, string id, CancellationToken ct = default)
    {
        ValidateCollection(collection);
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await conn.ExecuteAsync(
            $"DELETE FROM {collection} WHERE id = @Id;",
            new { Id = id }, commandTimeout: 30);
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        string collection,
        float[] queryVector,
        int topK = 10,
        float minScore = 0f,
        VectorFilter? filter = null,
        CancellationToken ct = default)
    {
        ValidateCollection(collection);
        var vecStr = FormatVector(queryVector);
        // Cosine similarity = 1 − cosine distance.
        var sql = $@"SELECT id,
                            1 - (vector <=> @Vec::vector) AS score,
                            text_content,
                            metadata
                     FROM {collection}
                     WHERE 1 - (vector <=> @Vec::vector) >= @MinScore
                     ORDER BY vector <=> @Vec::vector
                     LIMIT @TopK;";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        var rows = await conn.QueryAsync<(string Id, double Score, string? Text, string? Meta)>(
            sql, new { Vec = vecStr, MinScore = (double)minScore, TopK = topK }, commandTimeout: 30);
        return rows.Select(r => new VectorSearchResult(
            r.Id,
            (float)r.Score,
            r.Text,
            r.Meta is null ? null : JsonSerializer.Deserialize<Dictionary<string, object>>(r.Meta)))
            .ToList();
    }

    private static void ValidateCollection(string collection)
    {
        if (string.IsNullOrWhiteSpace(collection)
            || !System.Text.RegularExpressions.Regex.IsMatch(collection, @"^[a-zA-Z_][a-zA-Z0-9_.]*$"))
            throw new ArgumentException($"Invalid collection name: '{collection}'.");
    }

    private static string FormatVector(float[] v) =>
        "[" + string.Join(",", v.Select(f => f.ToString("F8", System.Globalization.CultureInfo.InvariantCulture))) + "]";

    private static float[] ParseVector(string pgVec)
    {
        var trimmed = pgVec.Trim('[', ']');
        return trimmed.Split(',').Select(s => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
    }
}

// ─── Concrete stores ─────────────────────────────────────────────────────────

/// <summary>VD-06 — Supabase pgvector store. Connects to Supabase PostgreSQL with pgvector extension.</summary>
public sealed class SupabasePgVectorStore : NpgsqlPgVectorBase
{
    public override VectorDbProviderType ProviderType => VectorDbProviderType.SupabasePgVector;
    public SupabasePgVectorStore(string connectionString) : base(connectionString) { }
}

/// <summary>VD-07 — Neon pgvector store. Connects to Neon serverless PostgreSQL with pgvector extension.</summary>
public sealed class NeonPgVectorStore : NpgsqlPgVectorBase
{
    public override VectorDbProviderType ProviderType => VectorDbProviderType.NeonPgVector;
    public NeonPgVectorStore(string connectionString) : base(connectionString) { }
}

/// <summary>VD-08 — Timescale pgvector store. Connects to TimescaleDB (Timescale Cloud or self-hosted) with pgvector.</summary>
public sealed class TimescalePgVectorStore : NpgsqlPgVectorBase
{
    public override VectorDbProviderType ProviderType => VectorDbProviderType.TimescalePgVector;
    public TimescalePgVectorStore(string connectionString) : base(connectionString) { }
}

/// <summary>VD-09 — CockroachDB pgvector store. Connects to CockroachDB with pgvector extension.</summary>
public sealed class CockroachPgVectorStore : NpgsqlPgVectorBase
{
    public override VectorDbProviderType ProviderType => VectorDbProviderType.CockroachPgVector;
    public CockroachPgVectorStore(string connectionString) : base(connectionString) { }
}
