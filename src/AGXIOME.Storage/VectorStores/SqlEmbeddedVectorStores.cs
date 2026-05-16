using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Errors;
using AGXIOME.Shared.Models;
using Dapper;
using DuckDB.NET.Data;
using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace AGXIOME.Storage.VectorStores;

// ─────────────────────────────────────────────────────────────────────────────
// VD-19 — SqliteVecStore     (SQLite embedded vector store)
// VD-21 — DuckDbVectorStore  (DuckDB analytical embedded vector store)
//
// Both stores serialize vectors as JSON TEXT and apply brute-force cosine
// similarity search in-memory. This is correct for moderate collection sizes
// (<100k vectors). For larger datasets, replace with native extension
// (sqlite-vec for SQLite, VSS extension for DuckDB).
// ─────────────────────────────────────────────────────────────────────────────

// ─── VD-19 — SQLite Vec ───────────────────────────────────────────────────────

public sealed record SqliteVecOptions
{
    public required string ConnectionString { get; init; }
    /// <summary>If true, CREATE TABLE IF NOT EXISTS is executed on first use.</summary>
    public bool AutoCreateSchema { get; init; } = true;
}

/// <summary>VD-19 — SQLite embedded vector store.
/// Stores vectors as JSON TEXT. Cosine similarity computed in-memory on Search.
/// For production use, load the sqlite-vec extension for HNSW-backed ANN search.</summary>
public sealed class SqliteVecStore : IVectorStore
{
    private readonly SqliteVecOptions _opts;
    public VectorDbProviderType ProviderType => VectorDbProviderType.SqliteVec;

    public SqliteVecStore(SqliteVecOptions opts)
        => _opts = opts ?? throw new ArgumentNullException(nameof(opts));

    public async Task EnsureCollectionAsync(string collection, int dimensions, CancellationToken ct = default)
    {
        ValidateCollection(collection);
        await using var conn = new SqliteConnection(_opts.ConnectionString);
        await conn.OpenAsync(ct);
        await conn.ExecuteAsync($@"CREATE TABLE IF NOT EXISTS ""{collection}"" (
            ""id""           TEXT NOT NULL PRIMARY KEY,
            ""vector_json""  TEXT NOT NULL,
            ""text_content"" TEXT,
            ""metadata""     TEXT
        );");
    }

    public async Task DeleteCollectionAsync(string collection, CancellationToken ct = default)
    {
        ValidateCollection(collection);
        await using var conn = new SqliteConnection(_opts.ConnectionString);
        await conn.OpenAsync(ct);
        await conn.ExecuteAsync($@"DROP TABLE IF EXISTS ""{collection}"";");
    }

    public async Task<IReadOnlyList<string>> ListCollectionsAsync(CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_opts.ConnectionString);
        await conn.OpenAsync(ct);
        var tables = await conn.QueryAsync<string>(
            "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;");
        return tables.ToList();
    }

    public async Task UpsertAsync(string collection, VectorRecord record, CancellationToken ct = default)
    {
        ValidateCollection(collection);
        var vecJson = JsonSerializer.Serialize(record.Vector, JsonOpts);
        var metaJson = record.Metadata is null ? null : JsonSerializer.Serialize(record.Metadata, JsonOpts);
        await using var conn = new SqliteConnection(_opts.ConnectionString);
        await conn.OpenAsync(ct);
        await conn.ExecuteAsync(
            $@"INSERT INTO ""{collection}"" (id, vector_json, text_content, metadata)
               VALUES (@Id, @Vec, @Text, @Meta)
               ON CONFLICT(id) DO UPDATE SET
                 vector_json  = excluded.vector_json,
                 text_content = excluded.text_content,
                 metadata     = excluded.metadata;",
            new { Id = record.Id, Vec = vecJson, Text = record.Text, Meta = metaJson });
    }

    public async Task UpsertBatchAsync(string collection, IReadOnlyList<VectorRecord> records, CancellationToken ct = default)
    {
        foreach (var r in records) await UpsertAsync(collection, r, ct);
    }

    public async Task<VectorRecord?> GetAsync(string collection, string id, CancellationToken ct = default)
    {
        ValidateCollection(collection);
        await using var conn = new SqliteConnection(_opts.ConnectionString);
        await conn.OpenAsync(ct);
        var row = await conn.QueryFirstOrDefaultAsync<(string? Vec, string? Text, string? Meta)>(
            $@"SELECT vector_json, text_content, metadata FROM ""{collection}"" WHERE id = @Id;",
            new { Id = id });
        if (row.Vec is null) return null;
        var vec = JsonSerializer.Deserialize<float[]>(row.Vec, JsonOpts) ?? Array.Empty<float>();
        return new VectorRecord(id, vec, row.Text,
            row.Meta is null ? null : JsonSerializer.Deserialize<Dictionary<string, object>>(row.Meta, JsonOpts));
    }

    public async Task DeleteAsync(string collection, string id, CancellationToken ct = default)
    {
        ValidateCollection(collection);
        await using var conn = new SqliteConnection(_opts.ConnectionString);
        await conn.OpenAsync(ct);
        await conn.ExecuteAsync($@"DELETE FROM ""{collection}"" WHERE id = @Id;", new { Id = id });
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        string collection, float[] queryVector, int topK = 10, float minScore = 0f,
        VectorFilter? filter = null, CancellationToken ct = default)
    {
        ValidateCollection(collection);
        await using var conn = new SqliteConnection(_opts.ConnectionString);
        await conn.OpenAsync(ct);
        var rows = await conn.QueryAsync<(string Id, string Vec, string? Text)>(
            $@"SELECT id, vector_json, text_content FROM ""{collection}"";");
        return rows
            .Select(r =>
            {
                var vec = JsonSerializer.Deserialize<float[]>(r.Vec, JsonOpts) ?? Array.Empty<float>();
                return new VectorSearchResult(r.Id, CosineSimilarity(queryVector, vec), r.Text);
            })
            .Where(r => r.Score >= minScore)
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .ToList();
    }

    private static void ValidateCollection(string name)
    {
        VectorStoreGuards.ValidateCollectionName("SqliteVec", name, @"^[a-zA-Z_][a-zA-Z0-9_]*$");
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0) return 0f;
        float dot = 0f, normA = 0f, normB = 0f;
        for (int i = 0; i < a.Length; i++) { dot += a[i] * b[i]; normA += a[i] * a[i]; normB += b[i] * b[i]; }
        var denom = MathF.Sqrt(normA) * MathF.Sqrt(normB);
        return denom < float.Epsilon ? 0f : dot / denom;
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };
}

// ─── VD-21 — DuckDB Vector ────────────────────────────────────────────────────

public sealed record DuckDbVectorOptions
{
    /// <summary>DuckDB data source. Use "DataSource=:memory:" for in-process testing.</summary>
    public required string DataSource { get; init; }
}

/// <summary>VD-21 — DuckDB analytical embedded vector store.
/// Stores vectors as JSON TEXT with brute-force cosine similarity search.
/// DuckDB's VSS extension is not yet stable; upgrade when GA.</summary>
public sealed class DuckDbVectorStore : IVectorStore
{
    private readonly DuckDbVectorOptions _opts;
    // Persistent connection required: DuckDB in-memory mode is connection-scoped.
    private readonly DuckDBConnection _conn;
    public VectorDbProviderType ProviderType => VectorDbProviderType.DuckDBVector;

    public DuckDbVectorStore(DuckDbVectorOptions opts)
    {
        _opts = opts ?? throw new ArgumentNullException(nameof(opts));
        _conn = new DuckDBConnection($"DataSource={opts.DataSource}");
        _conn.Open();
    }

    public async Task EnsureCollectionAsync(string collection, int dimensions, CancellationToken ct = default)
    {
        ValidateCollection(collection);
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = $@"CREATE TABLE IF NOT EXISTS ""{collection}"" (
            ""id""           VARCHAR   NOT NULL PRIMARY KEY,
            ""vector_json""  VARCHAR   NOT NULL,
            ""text_content"" VARCHAR,
            ""metadata""     VARCHAR
        );";
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteCollectionAsync(string collection, CancellationToken ct = default)
    {
        ValidateCollection(collection);
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = $@"DROP TABLE IF EXISTS ""{collection}"";";
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<string>> ListCollectionsAsync(CancellationToken ct = default)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT table_name FROM information_schema.tables WHERE table_schema = 'main';";
        using var reader = await cmd.ExecuteReaderAsync(ct);
        var names = new List<string>();
        while (await reader.ReadAsync(ct)) names.Add(reader.GetString(0));
        return names;
    }

    public async Task UpsertAsync(string collection, VectorRecord record, CancellationToken ct = default)
    {
        ValidateCollection(collection);
        var vecJson = JsonSerializer.Serialize(record.Vector, JsonOpts);
        var metaJson = record.Metadata is null ? null : JsonSerializer.Serialize(record.Metadata, JsonOpts);
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = $@"INSERT INTO ""{collection}"" (id, vector_json, text_content, metadata)
               VALUES ('{Escape(record.Id)}', '{Escape(vecJson)}', {Lit(record.Text)}, {Lit(metaJson)})
               ON CONFLICT(""id"") DO UPDATE SET
                 ""vector_json""  = excluded.""vector_json"",
                 ""text_content"" = excluded.""text_content"",
                 ""metadata""     = excluded.""metadata"";";
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpsertBatchAsync(string collection, IReadOnlyList<VectorRecord> records, CancellationToken ct = default)
    {
        foreach (var r in records) await UpsertAsync(collection, r, ct);
    }

    public async Task<VectorRecord?> GetAsync(string collection, string id, CancellationToken ct = default)
    {
        ValidateCollection(collection);
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = $@"SELECT vector_json, text_content, metadata FROM ""{collection}"" WHERE id = '{Escape(id)}';";
        using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var vec = JsonSerializer.Deserialize<float[]>(reader.GetString(0), JsonOpts) ?? Array.Empty<float>();
        var text = reader.IsDBNull(1) ? null : reader.GetString(1);
        return new VectorRecord(id, vec, text);
    }

    public async Task DeleteAsync(string collection, string id, CancellationToken ct = default)
    {
        ValidateCollection(collection);
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = $@"DELETE FROM ""{collection}"" WHERE id = '{Escape(id)}';";
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        string collection, float[] queryVector, int topK = 10, float minScore = 0f,
        VectorFilter? filter = null, CancellationToken ct = default)
    {
        ValidateCollection(collection);
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = $@"SELECT id, vector_json, text_content FROM ""{collection}"";";
        using var reader = await cmd.ExecuteReaderAsync(ct);
        var rows = new List<(string Id, float[] Vec, string? Text)>();
        while (await reader.ReadAsync(ct))
        {
            var vec = JsonSerializer.Deserialize<float[]>(reader.GetString(1), JsonOpts) ?? Array.Empty<float>();
            rows.Add((reader.GetString(0), vec, reader.IsDBNull(2) ? null : reader.GetString(2)));
        }
        return rows
            .Select(r => new VectorSearchResult(r.Id, CosineSimilarity(queryVector, r.Vec), r.Text))
            .Where(r => r.Score >= minScore)
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .ToList();
    }

    private static void ValidateCollection(string name)
    {
        VectorStoreGuards.ValidateCollectionName("DuckDBVector", name, @"^[a-zA-Z_][a-zA-Z0-9_]*$");
    }

    // SQL-safe: escape single quotes by doubling them (DuckDB / ANSI SQL standard).
    private static string Escape(string s) => s.Replace("'", "''");
    private static string Lit(string? s) => s is null ? "NULL" : $"'{Escape(s)}'";

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0) return 0f;
        float dot = 0f, normA = 0f, normB = 0f;
        for (int i = 0; i < a.Length; i++) { dot += a[i] * b[i]; normA += a[i] * a[i]; normB += b[i] * b[i]; }
        var denom = MathF.Sqrt(normA) * MathF.Sqrt(normB);
        return denom < float.Epsilon ? 0f : dot / denom;
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };
}
