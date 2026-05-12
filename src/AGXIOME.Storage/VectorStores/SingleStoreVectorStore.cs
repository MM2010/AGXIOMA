using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using Dapper;
using MySqlConnector;
using System.Text.Json;

namespace AGXIOME.Storage.VectorStores;

// ─────────────────────────────────────────────────────────────────────────────
// VD-10 — SingleStore vector store.
//
// SingleStore supports VECTOR(dim) column type with DOT_PRODUCT, L2_DISTANCE
// and EUCLIDEAN_DISTANCE functions for similarity search.
// Uses MySqlConnector (MySQL-wire compatible) and Dapper.
//
// Collection = SingleStore table name.
// Vectors stored as JSON text and deserialized server-side via JSON_EXTRACT;
// for production, declare the column as VECTOR(dim) and use vector_similarity.
// ─────────────────────────────────────────────────────────────────────────────

public sealed record SingleStoreVectorOptions
{
    public required string ConnectionString { get; init; }
    /// <summary>Number of vector dimensions. Used when auto-creating tables.</summary>
    public int Dimensions { get; init; } = 1536;
}

/// <summary>VD-10 — SingleStore SQL+vector store.
/// Stores embeddings as JSON arrays with brute-force dot-product similarity search.
/// For production, upgrade to native VECTOR column + DOT_PRODUCT() function.</summary>
public sealed class SingleStoreVectorStore : IVectorStore
{
    private readonly SingleStoreVectorOptions _opts;
    public VectorDbProviderType ProviderType => VectorDbProviderType.SingleStoreVector;

    public SingleStoreVectorStore(SingleStoreVectorOptions opts)
        => _opts = opts ?? throw new ArgumentNullException(nameof(opts));

    public async Task EnsureCollectionAsync(string collection, int dimensions, CancellationToken ct = default)
    {
        var sql = $@"CREATE TABLE IF NOT EXISTS `{collection}` (
            `id`           VARCHAR(255) NOT NULL PRIMARY KEY,
            `vector_json`  LONGTEXT     NOT NULL,
            `text_content` TEXT,
            `metadata`     JSON
        );";
        await using var conn = new MySqlConnection(_opts.ConnectionString);
        await conn.ExecuteAsync(sql);
    }

    public async Task DeleteCollectionAsync(string collection, CancellationToken ct = default)
    {
        await using var conn = new MySqlConnection(_opts.ConnectionString);
        await conn.ExecuteAsync($"DROP TABLE IF EXISTS `{collection}`;");
    }

    public async Task<IReadOnlyList<string>> ListCollectionsAsync(CancellationToken ct = default)
    {
        await using var conn = new MySqlConnection(_opts.ConnectionString);
        var tables = await conn.QueryAsync<string>(
            "SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE();");
        return tables.ToList();
    }

    public async Task UpsertAsync(string collection, VectorRecord record, CancellationToken ct = default)
    {
        var vecJson = JsonSerializer.Serialize(record.Vector, JsonOpts);
        var metaJson = record.Metadata is null ? null : JsonSerializer.Serialize(record.Metadata, JsonOpts);
        await using var conn = new MySqlConnection(_opts.ConnectionString);
        await conn.ExecuteAsync(
            $@"INSERT INTO `{collection}` (id, vector_json, text_content, metadata)
               VALUES (@Id, @Vec, @Text, @Meta)
               ON DUPLICATE KEY UPDATE vector_json = @Vec, text_content = @Text, metadata = @Meta;",
            new { Id = record.Id, Vec = vecJson, Text = record.Text, Meta = metaJson },
            commandTimeout: 30);
    }

    public async Task UpsertBatchAsync(string collection, IReadOnlyList<VectorRecord> records, CancellationToken ct = default)
    {
        foreach (var r in records) await UpsertAsync(collection, r, ct);
    }

    public async Task<VectorRecord?> GetAsync(string collection, string id, CancellationToken ct = default)
    {
        await using var conn = new MySqlConnection(_opts.ConnectionString);
        var row = await conn.QueryFirstOrDefaultAsync<(string? Vec, string? Text, string? Meta)>(
            $"SELECT vector_json, text_content, metadata FROM `{collection}` WHERE id = @Id;",
            new { Id = id }, commandTimeout: 30);
        if (row.Vec is null) return null;
        var vec = JsonSerializer.Deserialize<float[]>(row.Vec, JsonOpts) ?? Array.Empty<float>();
        return new VectorRecord(id, vec, row.Text,
            row.Meta is null ? null : JsonSerializer.Deserialize<Dictionary<string, object>>(row.Meta, JsonOpts));
    }

    public async Task DeleteAsync(string collection, string id, CancellationToken ct = default)
    {
        await using var conn = new MySqlConnection(_opts.ConnectionString);
        await conn.ExecuteAsync(
            $"DELETE FROM `{collection}` WHERE id = @Id;",
            new { Id = id }, commandTimeout: 30);
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        string collection, float[] queryVector, int topK = 10, float minScore = 0f,
        VectorFilter? filter = null, CancellationToken ct = default)
    {
        // Brute-force: retrieve all vectors and compute cosine similarity in-memory.
        // In production, replace with SingleStore native DOT_PRODUCT() + vector index.
        await using var conn = new MySqlConnection(_opts.ConnectionString);
        var rows = await conn.QueryAsync<(string Id, string Vec, string? Text)>(
            $"SELECT id, vector_json, text_content FROM `{collection}`;",
            commandTimeout: 60);

        return rows
            .Select(r =>
            {
                var vec = JsonSerializer.Deserialize<float[]>(r.Vec, JsonOpts) ?? Array.Empty<float>();
                var score = CosineSimilarity(queryVector, vec);
                return new VectorSearchResult(r.Id, score, r.Text);
            })
            .Where(r => r.Score >= minScore)
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .ToList();
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
