using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using StackExchange.Redis;
using System.Text.Json;

namespace AGXIOME.Storage.VectorStores;

// ─────────────────────────────────────────────────────────────────────────────
// VD-22 — RedisStackVectorStore   (Redis Stack FT.CREATE / FT.SEARCH vector)
// VD-23 — MongoDbAtlasVectorStore (MongoDB Atlas $vectorSearch)
// VD-24 — CosmosDbVCoreStore      (Azure Cosmos DB vCore vector search)
// ─────────────────────────────────────────────────────────────────────────────

// ─── VD-22 — Redis Stack ─────────────────────────────────────────────────────

/// <summary>VD-22 — Redis Stack vector store using RediSearch FT.CREATE / FT.SEARCH kNN.
/// Collection = RediSearch index name. Records stored as Redis Hashes with key "{collection}:{id}".
/// Requires Redis Stack 7.x or Redis Cloud with Search module enabled.</summary>
public sealed class RedisStackVectorStore : IVectorStore
{
    private readonly IDatabase _db;
    private readonly IServer _server;
    public VectorDbProviderType ProviderType => VectorDbProviderType.RedisStack;

    /// <param name="db">IDatabase from StackExchange.Redis ConnectionMultiplexer.</param>
    /// <param name="server">IServer instance used for administrative commands (FT.CREATE, FT.DROPINDEX).</param>
    public RedisStackVectorStore(IDatabase db, IServer server)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _server = server ?? throw new ArgumentNullException(nameof(server));
    }

    public async Task EnsureCollectionAsync(string collection, int dimensions, CancellationToken ct = default)
    {
        // FT.CREATE {index} ON HASH PREFIX 1 "{collection}:" SCHEMA vector VECTOR HNSW 6 TYPE FLOAT32 DIM {dim} DISTANCE_METRIC COSINE
        try
        {
            await _server.ExecuteAsync("FT.CREATE",
                collection,
                "ON", "HASH",
                "PREFIX", "1", $"{collection}:",
                "SCHEMA",
                "vector", "VECTOR", "HNSW", "6",
                "TYPE", "FLOAT32",
                "DIM", dimensions.ToString(),
                "DISTANCE_METRIC", "COSINE");
        }
        catch (RedisServerException ex) when (ex.Message.Contains("Index already exists"))
        {
            // Idempotent — index already created.
        }
    }

    public async Task DeleteCollectionAsync(string collection, CancellationToken ct = default)
    {
        try
        {
            await _server.ExecuteAsync("FT.DROPINDEX", collection, "DD");
        }
        catch (RedisServerException ex) when (ex.Message.Contains("Unknown Index"))
        {
            // Index does not exist — safe to ignore.
        }
    }

    public async Task<IReadOnlyList<string>> ListCollectionsAsync(CancellationToken ct = default)
    {
        var result = await _server.ExecuteAsync("FT._LIST");
        return result.Resp2Type == ResultType.Array
            ? ((RedisResult[])result!).Select(r => (string)r!).ToList()
            : Array.Empty<string>();
    }

    public async Task UpsertAsync(string collection, VectorRecord record, CancellationToken ct = default)
    {
        var key = $"{collection}:{record.Id}";
        var vectorBytes = VectorToBytes(record.Vector);
        var entries = new List<HashEntry>
        {
            new("id", record.Id),
            new("vector", vectorBytes),
        };
        if (record.Text is not null) entries.Add(new("text_content", record.Text));
        if (record.Metadata is not null) entries.Add(new("metadata", JsonSerializer.Serialize(record.Metadata)));
        await _db.HashSetAsync(key, entries.ToArray());
    }

    public async Task UpsertBatchAsync(string collection, IReadOnlyList<VectorRecord> records, CancellationToken ct = default)
    {
        var batch = _db.CreateBatch();
        var tasks = records.Select(r => UpsertAsync(collection, r, ct)).ToList();
        batch.Execute();
        await Task.WhenAll(tasks);
    }

    public async Task<VectorRecord?> GetAsync(string collection, string id, CancellationToken ct = default)
    {
        var hash = await _db.HashGetAllAsync($"{collection}:{id}");
        if (hash.Length == 0) return null;
        var dict = hash.ToDictionary(e => (string)e.Name!, e => e.Value);
        var vecBytes = dict.TryGetValue("vector", out var vb) ? (byte[]?)vb : null;
        var vec = vecBytes is not null ? BytesToVector(vecBytes) : Array.Empty<float>();
        var text = dict.TryGetValue("text_content", out var t) ? (string?)t : null;
        return new VectorRecord(id, vec, text);
    }

    public async Task DeleteAsync(string collection, string id, CancellationToken ct = default)
        => await _db.KeyDeleteAsync($"{collection}:{id}");

    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        string collection, float[] queryVector, int topK = 10, float minScore = 0f,
        VectorFilter? filter = null, CancellationToken ct = default)
    {
        var vecBytes = VectorToBytes(queryVector);
        // FT.SEARCH {index} "*=>[KNN {topK} @vector $blob AS score]" PARAMS 2 blob {bytes} DIALECT 2
        var result = await _server.ExecuteAsync("FT.SEARCH",
            collection,
            $"*=>[KNN {topK} @vector $blob AS score]",
            "PARAMS", "2", "blob", vecBytes,
            "SORTBY", "score",
            "DIALECT", "2",
            "LIMIT", "0", topK.ToString());

        var results = new List<VectorSearchResult>();
        if (result.Resp2Type != ResultType.Array) return results;
        var arr = (RedisResult[])result!;
        // Result format: [count, key, [field, value, ...], key, [field, value, ...], ...]
        for (int i = 1; i < arr.Length; i += 2)
        {
            var key = (string)arr[i]!;
            var id = key.StartsWith(collection + ":", StringComparison.Ordinal) ? key[(collection.Length + 1)..] : key;
            if (i + 1 >= arr.Length) break;
            var fields = (RedisResult[])arr[i + 1]!;
            float score = 0f;
            string? text = null;
            for (int j = 0; j < fields.Length - 1; j += 2)
            {
                var fname = (string)fields[j]!;
                if (fname == "score") float.TryParse((string)fields[j + 1]!, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out score);
                else if (fname == "text_content") text = (string?)fields[j + 1];
            }
            // Redis returns cosine distance (0=identical); convert to similarity.
            var similarity = 1f - score;
            if (similarity >= minScore) results.Add(new VectorSearchResult(id, similarity, text));
        }
        return results;
    }

    // IEEE 754 float32 little-endian bytes — required by RediSearch VECTOR type.
    private static byte[] VectorToBytes(float[] vec)
    {
        var bytes = new byte[vec.Length * 4];
        Buffer.BlockCopy(vec, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static float[] BytesToVector(byte[] bytes)
    {
        var vec = new float[bytes.Length / 4];
        Buffer.BlockCopy(bytes, 0, vec, 0, bytes.Length);
        return vec;
    }
}

// ─── VD-23 — MongoDB Atlas ────────────────────────────────────────────────────

/// <summary>VD-23 — MongoDB Atlas vector store using $vectorSearch aggregation pipeline.
/// Requires Atlas with a Vector Search index configured on the "vector" field.
/// Collection = MongoDB collection name within the configured database.</summary>
public sealed class MongoDbAtlasVectorStore : IVectorStore
{
    private readonly IMongoDatabase _db;
    public VectorDbProviderType ProviderType => VectorDbProviderType.MongoDBAtlas;

    public MongoDbAtlasVectorStore(IMongoDatabase database)
        => _db = database ?? throw new ArgumentNullException(nameof(database));

    public Task EnsureCollectionAsync(string collection, int dimensions, CancellationToken ct = default)
    {
        // MongoDB creates collections implicitly on first insert.
        // Atlas Vector Search index must be created via Atlas UI or API separately.
        return Task.CompletedTask;
    }

    public async Task DeleteCollectionAsync(string collection, CancellationToken ct = default)
        => await _db.DropCollectionAsync(collection, ct);

    public async Task<IReadOnlyList<string>> ListCollectionsAsync(CancellationToken ct = default)
    {
        var names = await _db.ListCollectionNamesAsync(cancellationToken: ct);
        return await names.ToListAsync(ct);
    }

    public async Task UpsertAsync(string collection, VectorRecord record, CancellationToken ct = default)
    {
        var col = _db.GetCollection<BsonDocument>(collection);
        var doc = new BsonDocument
        {
            { "_id", record.Id },
            { "vector", new BsonArray(record.Vector.Select(f => (BsonValue)f)) },
            { "text_content", record.Text ?? BsonNull.Value.AsString },
        };
        if (record.Metadata is not null)
            doc["metadata"] = BsonDocument.Parse(JsonSerializer.Serialize(record.Metadata));

        var filter = Builders<BsonDocument>.Filter.Eq("_id", record.Id);
        await col.ReplaceOneAsync(filter, doc, new ReplaceOptions { IsUpsert = true }, ct);
    }

    public async Task UpsertBatchAsync(string collection, IReadOnlyList<VectorRecord> records, CancellationToken ct = default)
    {
        var col = _db.GetCollection<BsonDocument>(collection);
        var ops = records.Select(r =>
        {
            var doc = new BsonDocument
            {
                { "_id", r.Id },
                { "vector", new BsonArray(r.Vector.Select(f => (BsonValue)f)) },
                { "text_content", r.Text ?? BsonNull.Value.AsString },
            };
            return new ReplaceOneModel<BsonDocument>(
                Builders<BsonDocument>.Filter.Eq("_id", r.Id), doc)
            { IsUpsert = true };
        }).ToList<WriteModel<BsonDocument>>();
        await col.BulkWriteAsync(ops, cancellationToken: ct);
    }

    public async Task<VectorRecord?> GetAsync(string collection, string id, CancellationToken ct = default)
    {
        var col = _db.GetCollection<BsonDocument>(collection);
        var doc = await col.Find(Builders<BsonDocument>.Filter.Eq("_id", id))
                           .FirstOrDefaultAsync(ct);
        if (doc is null) return null;
        var vec = doc["vector"].AsBsonArray.Select(v => (float)v.AsDouble).ToArray();
        var text = doc.TryGetValue("text_content", out var t) && t != BsonNull.Value ? t.AsString : null;
        return new VectorRecord(id, vec, text);
    }

    public async Task DeleteAsync(string collection, string id, CancellationToken ct = default)
    {
        var col = _db.GetCollection<BsonDocument>(collection);
        await col.DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("_id", id), ct);
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        string collection, float[] queryVector, int topK = 10, float minScore = 0f,
        VectorFilter? filter = null, CancellationToken ct = default)
    {
        var col = _db.GetCollection<BsonDocument>(collection);
        // Atlas $vectorSearch requires a pre-created vector search index named "vector_index".
        var pipeline = new[]
        {
            new BsonDocument("$vectorSearch", new BsonDocument
            {
                { "index", "vector_index" },
                { "path", "vector" },
                { "queryVector", new BsonArray(queryVector.Select(f => (BsonValue)(double)f)) },
                { "numCandidates", topK * 10 },
                { "limit", topK },
            }),
            new BsonDocument("$addFields", new BsonDocument("score",
                new BsonDocument("$meta", "vectorSearchScore"))),
        };
        var cursor = await col.AggregateAsync<BsonDocument>(pipeline, cancellationToken: ct);
        var results = new List<VectorSearchResult>();
        await cursor.ForEachAsync(doc =>
        {
            var score = doc.TryGetValue("score", out var s) ? (float)s.AsDouble : 0f;
            if (score < minScore) return;
            var id = doc["_id"].AsString;
            var text = doc.TryGetValue("text_content", out var t) && t != BsonNull.Value ? t.AsString : null;
            results.Add(new VectorSearchResult(id, score, text));
        }, ct);
        return results;
    }
}

// ─── VD-24 — Cosmos DB vCore ─────────────────────────────────────────────────

/// <summary>VD-24 — Azure Cosmos DB vCore vector store.
/// Uses Cosmos DB vCore (MongoDB-compatible API) with native vector search.
/// Requires Cosmos DB vCore account with vector search enabled.
/// Collection = container name; PartitionKey = "/id".</summary>
public sealed class CosmosDbVCoreStore : IVectorStore
{
    private readonly CosmosClient _client;
    private readonly string _databaseId;
    public VectorDbProviderType ProviderType => VectorDbProviderType.CosmosDBVCore;

    public CosmosDbVCoreStore(CosmosClient client, string databaseId)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseId);
        _databaseId = databaseId;
    }

    public async Task EnsureCollectionAsync(string collection, int dimensions, CancellationToken ct = default)
    {
        var db = _client.GetDatabase(_databaseId);
        // CreateContainerIfNotExistsAsync creates container with partition key /id.
        await db.CreateContainerIfNotExistsAsync(
            new ContainerProperties(collection, "/id"),
            cancellationToken: ct);
    }

    public async Task DeleteCollectionAsync(string collection, CancellationToken ct = default)
    {
        var container = _client.GetContainer(_databaseId, collection);
        await container.DeleteContainerAsync(cancellationToken: ct);
    }

    public async Task<IReadOnlyList<string>> ListCollectionsAsync(CancellationToken ct = default)
    {
        var db = _client.GetDatabase(_databaseId);
        var iter = db.GetContainerQueryIterator<ContainerProperties>("SELECT * FROM c");
        var names = new List<string>();
        while (iter.HasMoreResults)
        {
            var page = await iter.ReadNextAsync(ct);
            names.AddRange(page.Select(c => c.Id));
        }
        return names;
    }

    public async Task UpsertAsync(string collection, VectorRecord record, CancellationToken ct = default)
    {
        var container = _client.GetContainer(_databaseId, collection);
        var item = new
        {
            id = record.Id,
            vector = record.Vector,
            text_content = record.Text,
            metadata = record.Metadata,
        };
        await container.UpsertItemAsync(item, new PartitionKey(record.Id), cancellationToken: ct);
    }

    public async Task UpsertBatchAsync(string collection, IReadOnlyList<VectorRecord> records, CancellationToken ct = default)
    {
        foreach (var r in records) await UpsertAsync(collection, r, ct);
    }

    public async Task<VectorRecord?> GetAsync(string collection, string id, CancellationToken ct = default)
    {
        var container = _client.GetContainer(_databaseId, collection);
        try
        {
            var resp = await container.ReadItemAsync<JsonElement>(id, new PartitionKey(id), cancellationToken: ct);
            var doc = resp.Resource;
            var vec = doc.TryGetProperty("vector", out var v)
                ? v.EnumerateArray().Select(e => e.GetSingle()).ToArray()
                : Array.Empty<float>();
            return new VectorRecord(id, vec,
                doc.TryGetProperty("text_content", out var t) ? t.GetString() : null);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string collection, string id, CancellationToken ct = default)
    {
        var container = _client.GetContainer(_databaseId, collection);
        try
        {
            await container.DeleteItemAsync<object>(id, new PartitionKey(id), cancellationToken: ct);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Already deleted — idempotent.
        }
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        string collection, float[] queryVector, int topK = 10, float minScore = 0f,
        VectorFilter? filter = null, CancellationToken ct = default)
    {
        var container = _client.GetContainer(_databaseId, collection);
        // Cosmos DB vCore vector search uses VectorDistance() system function (preview).
        var vecJson = "[" + string.Join(",", queryVector.Select(f => f.ToString(System.Globalization.CultureInfo.InvariantCulture))) + "]";
        var sql = $"SELECT TOP {topK} c.id, c.text_content, VectorDistance(c.vector, {vecJson}) AS score " +
                  $"FROM c ORDER BY VectorDistance(c.vector, {vecJson})";
        var iter = container.GetItemQueryIterator<JsonElement>(sql);
        var results = new List<VectorSearchResult>();
        while (iter.HasMoreResults)
        {
            var page = await iter.ReadNextAsync(ct);
            foreach (var item in page)
            {
                var score = item.TryGetProperty("score", out var s) ? s.GetSingle() : 0f;
                // VectorDistance returns distance (lower = closer); convert to similarity.
                var similarity = 1f - score;
                if (similarity < minScore) continue;
                results.Add(new VectorSearchResult(
                    item.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "",
                    similarity,
                    item.TryGetProperty("text_content", out var t) ? t.GetString() : null));
            }
        }
        return results;
    }
}
