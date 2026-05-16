using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Errors;
using AGXIOME.Shared.Models;
using Dapper;
using Microsoft.Data.SqlClient;
using RocksDbSharp;
using System.Text;
using System.Collections.Concurrent;
using System.Text.Json;

namespace AGXIOME.Storage.VectorStores;

// ─────────────────────────────────────────────────────────────────────────────
// VD-29 — RocksDbHnswStore    (RocksDB persistent KV + in-memory cosine index)
// VD-30 — SqlServerVectorStore (SQL Server 2025 native VECTOR type)
// ─────────────────────────────────────────────────────────────────────────────

// ─── VD-29 — RocksDB + HNSW ──────────────────────────────────────────────────

/// <summary>VD-29 — RocksDB persistent key-value store with in-memory cosine vector index.
/// RocksDB stores the authoritative serialised VectorRecord bytes on disk.
/// An in-memory ConcurrentDictionary holds vectors for fast ANN search (exact cosine).
/// On startup the index is rebuilt from the RocksDB store.
///
/// Key format: "collection\0id" (null-byte separator, never appears in base64 IDs).</summary>
public sealed class RocksDbHnswStore : IVectorStore, IDisposable
{
    private readonly RocksDb _db;
    // In-memory index: collection → { id → vector }. Rebuilt from RocksDB on construction.
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, float[]>> _index = new(StringComparer.Ordinal);
    public VectorDbProviderType ProviderType => VectorDbProviderType.RocksDBHNSW;

    /// <param name="dbPath">Path to the RocksDB data directory. Created if it does not exist.</param>
    public RocksDbHnswStore(string dbPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dbPath);
        var options = new DbOptions().SetCreateIfMissing(true);
        _db = RocksDb.Open(options, dbPath);
        RebuildIndex();
    }

    // Rebuild in-memory vector index from persisted RocksDB records at startup.
    private void RebuildIndex()
    {
        using var iter = _db.NewIterator();
        for (iter.SeekToFirst(); iter.Valid(); iter.Next())
        {
            var key = iter.StringKey();
            var sep = key.IndexOf('\0');
            if (sep < 0) continue;
            var col = key[..sep];
            var id  = key[(sep + 1)..];
            try
            {
                var rec = JsonSerializer.Deserialize<PersistedRecord>(iter.Value(), JsonOpts);
                if (rec is not null)
                    _index.GetOrAdd(col, _ => new ConcurrentDictionary<string, float[]>(StringComparer.Ordinal))
                          [id] = rec.Vector;
            }
            catch { /* skip malformed records */ }
        }
    }

    public Task EnsureCollectionAsync(string collection, int dimensions, CancellationToken ct = default)
    {
        _index.GetOrAdd(collection, _ => new ConcurrentDictionary<string, float[]>(StringComparer.Ordinal));
        return Task.CompletedTask;
    }

    public Task DeleteCollectionAsync(string collection, CancellationToken ct = default)
    {
        _index.TryRemove(collection, out _);
        // Delete all RocksDB keys for this collection.
        using var iter = _db.NewIterator();
        var prefix = collection + "\0";
        for (iter.Seek(prefix); iter.Valid() && iter.StringKey().StartsWith(prefix, StringComparison.Ordinal); iter.Next())
            _db.Remove(iter.StringKey());
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> ListCollectionsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<string>>(_index.Keys.OrderBy(k => k).ToList());

    public Task UpsertAsync(string collection, VectorRecord record, CancellationToken ct = default)
    {
        var col = _index.GetOrAdd(collection, _ => new ConcurrentDictionary<string, float[]>(StringComparer.Ordinal));
        col[record.Id] = record.Vector;
        var dbKey = $"{collection}\0{record.Id}";
        var value = JsonSerializer.SerializeToUtf8Bytes(new PersistedRecord(record.Vector, record.Text, record.Metadata), JsonOpts);
        _db.Put(Encoding.UTF8.GetBytes(dbKey), value);
        return Task.CompletedTask;
    }

    public Task UpsertBatchAsync(string collection, IReadOnlyList<VectorRecord> records, CancellationToken ct = default)
    {
        var col = _index.GetOrAdd(collection, _ => new ConcurrentDictionary<string, float[]>(StringComparer.Ordinal));
        using var batch = new WriteBatch();
        foreach (var r in records)
        {
            col[r.Id] = r.Vector;
            var dbKey = $"{collection}\0{r.Id}";
            var value = JsonSerializer.SerializeToUtf8Bytes(new PersistedRecord(r.Vector, r.Text, r.Metadata), JsonOpts);
            batch.Put(Encoding.UTF8.GetBytes(dbKey), value);
        }
        _db.Write(batch);
        return Task.CompletedTask;
    }

    public Task<VectorRecord?> GetAsync(string collection, string id, CancellationToken ct = default)
    {
        var dbKey = $"{collection}\0{id}";
        var bytes = _db.Get(dbKey);
        if (bytes is null) return Task.FromResult<VectorRecord?>(null);
        var rec = JsonSerializer.Deserialize<PersistedRecord>(bytes, JsonOpts);
        return Task.FromResult<VectorRecord?>(rec is null ? null
            : new VectorRecord(id, rec.Vector, rec.Text, rec.Metadata));
    }

    public Task DeleteAsync(string collection, string id, CancellationToken ct = default)
    {
        if (_index.TryGetValue(collection, out var col))
            col.TryRemove(id, out _);
        _db.Remove($"{collection}\0{id}");
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        string collection, float[] queryVector, int topK = 10, float minScore = 0f,
        VectorFilter? filter = null, CancellationToken ct = default)
    {
        if (!_index.TryGetValue(collection, out var col))
            return Task.FromResult<IReadOnlyList<VectorSearchResult>>(Array.Empty<VectorSearchResult>());

        var results = col
            .Select(kv => new VectorSearchResult(kv.Key, CosineSimilarity(queryVector, kv.Value)))
            .Where(r => r.Score >= minScore)
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .ToList();
        return Task.FromResult<IReadOnlyList<VectorSearchResult>>(results);
    }

    public void Dispose() => _db.Dispose();

    private sealed record PersistedRecord(
        float[] Vector,
        string? Text,
        Dictionary<string, object>? Metadata);

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

// ─── VD-30 — SQL Server Vector ────────────────────────────────────────────────

/// <summary>VD-30 — SQL Server 2025+ native vector store using VECTOR(dim) column type.
/// Requires SQL Server 2025 or Azure SQL with vector search enabled (preview).
/// Collection = table name.
/// Uses VECTOR_DISTANCE('cosine', ...) for similarity search.</summary>
public sealed class SqlServerVectorStore : IVectorStore
{
    private readonly string _connectionString;
    public VectorDbProviderType ProviderType => VectorDbProviderType.SQLServerVector;

    public SqlServerVectorStore(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    public async Task EnsureCollectionAsync(string collection, int dimensions, CancellationToken ct = default)
    {
        ValidateCollection(collection);
        var sql = $@"IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[{collection}]') AND type = N'U')
        CREATE TABLE [dbo].[{collection}] (
            [id]           NVARCHAR(255) NOT NULL PRIMARY KEY,
            [vector]       VECTOR({dimensions}) NOT NULL,
            [text_content] NVARCHAR(MAX),
            [metadata]     NVARCHAR(MAX)
        );";
        await using var conn = new SqlConnection(_connectionString);
        await conn.ExecuteAsync(sql);
    }

    public async Task DeleteCollectionAsync(string collection, CancellationToken ct = default)
    {
        ValidateCollection(collection);
        await using var conn = new SqlConnection(_connectionString);
        await conn.ExecuteAsync($"DROP TABLE IF EXISTS [dbo].[{collection}];");
    }

    public async Task<IReadOnlyList<string>> ListCollectionsAsync(CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_connectionString);
        var tables = await conn.QueryAsync<string>(
            "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE';");
        return tables.ToList();
    }

    public async Task UpsertAsync(string collection, VectorRecord record, CancellationToken ct = default)
    {
        ValidateCollection(collection);
        // SQL Server VECTOR literal: '[f1, f2, ...]'
        var vecLiteral = VectorLiteral(record.Vector);
        var metaJson = record.Metadata is null ? null
            : System.Text.Json.JsonSerializer.Serialize(record.Metadata);
        await using var conn = new SqlConnection(_connectionString);
        await conn.ExecuteAsync(
            $@"MERGE [dbo].[{collection}] AS target
               USING (VALUES (@Id, CAST(@Vec AS VECTOR({record.Vector.Length})), @Text, @Meta))
                 AS source (id, vector, text_content, metadata) ON target.id = source.id
               WHEN MATCHED     THEN UPDATE SET vector = source.vector, text_content = source.text_content, metadata = source.metadata
               WHEN NOT MATCHED THEN INSERT (id, vector, text_content, metadata) VALUES (source.id, source.vector, source.text_content, source.metadata);",
            new { Id = record.Id, Vec = vecLiteral, Text = record.Text, Meta = metaJson },
            commandTimeout: 30);
    }

    public async Task UpsertBatchAsync(string collection, IReadOnlyList<VectorRecord> records, CancellationToken ct = default)
    {
        foreach (var r in records) await UpsertAsync(collection, r, ct);
    }

    public async Task<VectorRecord?> GetAsync(string collection, string id, CancellationToken ct = default)
    {
        ValidateCollection(collection);
        await using var conn = new SqlConnection(_connectionString);
        var row = await conn.QueryFirstOrDefaultAsync<(string? Vec, string? Text, string? Meta)>(
            $"SELECT CAST([vector] AS NVARCHAR(MAX)), [text_content], [metadata] FROM [dbo].[{collection}] WHERE [id] = @Id;",
            new { Id = id }, commandTimeout: 30);
        if (row.Vec is null) return null;
        var vec = ParseVectorLiteral(row.Vec);
        return new VectorRecord(id, vec, row.Text,
            row.Meta is null ? null : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(row.Meta));
    }

    public async Task DeleteAsync(string collection, string id, CancellationToken ct = default)
    {
        ValidateCollection(collection);
        await using var conn = new SqlConnection(_connectionString);
        await conn.ExecuteAsync(
            $"DELETE FROM [dbo].[{collection}] WHERE [id] = @Id;",
            new { Id = id }, commandTimeout: 30);
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        string collection, float[] queryVector, int topK = 10, float minScore = 0f,
        VectorFilter? filter = null, CancellationToken ct = default)
    {
        ValidateCollection(collection);
        var vecLiteral = VectorLiteral(queryVector);
        // VECTOR_DISTANCE returns 0 for identical vectors (lower = closer); 1 - dist = similarity.
        var sql = $@"SELECT TOP (@TopK) [id], [text_content],
                            1.0 - VECTOR_DISTANCE('cosine', [vector], CAST(@Vec AS VECTOR({queryVector.Length}))) AS score
                     FROM [dbo].[{collection}]
                     HAVING score >= @MinScore
                     ORDER BY score DESC;";
        await using var conn = new SqlConnection(_connectionString);
        var rows = await conn.QueryAsync<(string Id, string? Text, float Score)>(
            sql, new { TopK = topK, Vec = vecLiteral, MinScore = (double)minScore }, commandTimeout: 30);
        return rows.Select(r => new VectorSearchResult(r.Id, r.Score, r.Text)).ToList();
    }

    private static void ValidateCollection(string name)
    {
        VectorStoreGuards.ValidateCollectionName("SQLServerVector", name, @"^[a-zA-Z_][a-zA-Z0-9_]*$");
    }

    private static string VectorLiteral(float[] v) =>
        "[" + string.Join(",", v.Select(f => f.ToString(System.Globalization.CultureInfo.InvariantCulture))) + "]";

    private static float[] ParseVectorLiteral(string lit)
    {
        var trimmed = lit.Trim('[', ']');
        return trimmed.Split(',').Select(s => float.Parse(s.Trim(), System.Globalization.CultureInfo.InvariantCulture)).ToArray();
    }
}
