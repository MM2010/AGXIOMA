using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AGXIOME.Storage.VectorStores;

// ─────────────────────────────────────────────────────────────────────────────
// HTTP-based vector store implementations.
//
// All stores in this file communicate via REST APIs and accept an injected
// HttpClient — this enables unit tests to supply a TestHttpMessageHandler
// without requiring a live server.
//
// Providers covered:
//   VD-01  PineconeStore              (Pinecone REST v1)
//   VD-02  WeaviateCloudStore         (Weaviate REST v1 — cloud endpoint)
//   VD-04  MilvusCloudStore           (Milvus REST v2 — Zilliz cloud)
//   VD-05  ChromaDbCloudStore         (Chroma HTTP API v1 — managed)
//   VD-12  WeaviateSelfHostedStore    (same protocol, custom endpoint)
//   VD-13  MilvusSelfHostedStore      (same protocol, custom endpoint)
//   VD-14  ChromaDbSelfHostedStore    (same protocol, custom endpoint)
//   VD-18  VespaStore                 (Vespa Document + Query REST API)
//   VD-20  LanceDbStore               (LanceDB HTTP server mode)
//   VD-28  ScaNNStore                 (ScaNN gRPC/HTTP wrapper — HTTP endpoint)
// ─────────────────────────────────────────────────────────────────────────────

// Shared JSON options — camelCase, ignore nulls, InvariantCulture via NumberHandling.
file static class Json
{
    internal static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };
}

// ─── VD-01 — Pinecone ────────────────────────────────────────────────────────

/// <summary>Pinecone serverless vector store using the Pinecone REST API v1.
/// Collection maps to a Pinecone namespace within a pre-created index.
/// The base URL should be the index host URL (e.g. https://index-name-xxxx.svc.pinecone.io).
/// Set Authorization header on the HttpClient: "Api-Key {pinecone_key}".</summary>
public sealed class PineconeStore : IVectorStore
{
    private readonly HttpClient _http;
    public VectorDbProviderType ProviderType => VectorDbProviderType.Pinecone;

    public PineconeStore(HttpClient http)
        => _http = http ?? throw new ArgumentNullException(nameof(http));

    // Pinecone namespaces are created implicitly on first upsert; no schema creation needed.
    public Task EnsureCollectionAsync(string collection, int dimensions, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task DeleteCollectionAsync(string collection, CancellationToken ct = default)
        => _http.PostAsJsonAsync($"vectors/delete", new { deleteAll = true, @namespace = collection }, Json.Opts, ct).ContinueWith(_ => { }, ct);

    public async Task<IReadOnlyList<string>> ListCollectionsAsync(CancellationToken ct = default)
    {
        // Pinecone doesn't expose namespace listing in the standard REST v1 API.
        return Array.Empty<string>();
    }

    public async Task UpsertAsync(string collection, VectorRecord record, CancellationToken ct = default)
    {
        var body = new
        {
            vectors = new[]
            {
                new
                {
                    id = record.Id,
                    values = record.Vector,
                    metadata = record.Metadata,
                }
            },
            @namespace = collection,
        };
        var resp = await _http.PostAsJsonAsync("vectors/upsert", body, Json.Opts, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task UpsertBatchAsync(string collection, IReadOnlyList<VectorRecord> records, CancellationToken ct = default)
    {
        var body = new
        {
            vectors = records.Select(r => new { id = r.Id, values = r.Vector, metadata = r.Metadata }),
            @namespace = collection,
        };
        var resp = await _http.PostAsJsonAsync("vectors/upsert", body, Json.Opts, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<VectorRecord?> GetAsync(string collection, string id, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"vectors/fetch?ids={Uri.EscapeDataString(id)}&namespace={Uri.EscapeDataString(collection)}", ct);
        resp.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        if (!doc.RootElement.TryGetProperty("vectors", out var vectors) || !vectors.TryGetProperty(id, out var v))
            return null;
        var vals = v.GetProperty("values").EnumerateArray().Select(e => e.GetSingle()).ToArray();
        return new VectorRecord(id, vals);
    }

    public async Task DeleteAsync(string collection, string id, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("vectors/delete",
            new { ids = new[] { id }, @namespace = collection }, Json.Opts, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        string collection, float[] queryVector, int topK = 10, float minScore = 0f,
        VectorFilter? filter = null, CancellationToken ct = default)
    {
        var body = new
        {
            vector = queryVector,
            topK,
            includeMetadata = true,
            @namespace = collection,
        };
        var resp = await _http.PostAsJsonAsync("query", body, Json.Opts, ct);
        resp.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var results = new List<VectorSearchResult>();
        if (!doc.RootElement.TryGetProperty("matches", out var matches)) return results;
        foreach (var m in matches.EnumerateArray())
        {
            var score = m.TryGetProperty("score", out var s) ? s.GetSingle() : 0f;
            if (score < minScore) continue;
            var mid = m.GetProperty("id").GetString() ?? "";
            results.Add(new VectorSearchResult(mid, score));
        }
        return results;
    }
}

// ─── VD-02 / VD-12 — Weaviate ────────────────────────────────────────────────

/// <summary>Shared Weaviate REST v1 implementation. Collection = Weaviate class name.
/// Inject HttpClient with base address set to Weaviate endpoint (e.g. http://localhost:8080/).
/// For cloud, also set Authorization header: "Bearer {token}".</summary>
public abstract class WeaviateVectorStoreBase : IVectorStore
{
    private readonly HttpClient _http;
    public abstract VectorDbProviderType ProviderType { get; }

    protected WeaviateVectorStoreBase(HttpClient http)
        => _http = http ?? throw new ArgumentNullException(nameof(http));

    public async Task EnsureCollectionAsync(string collection, int dimensions, CancellationToken ct = default)
    {
        // Idempotently create the class schema.
        var body = new
        {
            @class = collection,
            vectorizer = "none",  // we supply vectors manually
            properties = new object[]
            {
                new { name = "text_content", dataType = new[] { "text" } },
                new { name = "metadata", dataType = new[] { "text" } },
            },
        };
        var resp = await _http.PostAsJsonAsync("v1/schema", body, Json.Opts, ct);
        // 422 = class already exists — treat as success.
        if (resp.StatusCode != System.Net.HttpStatusCode.UnprocessableEntity)
            resp.EnsureSuccessStatusCode();
    }

    public async Task DeleteCollectionAsync(string collection, CancellationToken ct = default)
    {
        var resp = await _http.DeleteAsync($"v1/schema/{Uri.EscapeDataString(collection)}", ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<string>> ListCollectionsAsync(CancellationToken ct = default)
    {
        var resp = await _http.GetAsync("v1/schema", ct);
        resp.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var classes = doc.RootElement.TryGetProperty("classes", out var c) ? c : default;
        if (classes.ValueKind != JsonValueKind.Array) return Array.Empty<string>();
        return classes.EnumerateArray()
            .Select(x => x.TryGetProperty("class", out var n) ? n.GetString() : null)
            .Where(n => n is not null).Select(n => n!).ToList();
    }

    public async Task UpsertAsync(string collection, VectorRecord record, CancellationToken ct = default)
    {
        var body = new
        {
            id = record.Id,
            @class = collection,
            vector = record.Vector,
            properties = new { text_content = record.Text, metadata = record.Metadata is null ? null : JsonSerializer.Serialize(record.Metadata) },
        };
        // PUT to update if exists, POST to create.
        var put = await _http.PutAsJsonAsync($"v1/objects/{Uri.EscapeDataString(collection)}/{Uri.EscapeDataString(record.Id)}", body, Json.Opts, ct);
        if (!put.IsSuccessStatusCode)
        {
            var post = await _http.PostAsJsonAsync("v1/objects", body, Json.Opts, ct);
            post.EnsureSuccessStatusCode();
        }
    }

    public async Task UpsertBatchAsync(string collection, IReadOnlyList<VectorRecord> records, CancellationToken ct = default)
    {
        var body = new
        {
            objects = records.Select(r => new
            {
                id = r.Id,
                @class = collection,
                vector = r.Vector,
                properties = new { text_content = r.Text },
            }),
        };
        var resp = await _http.PostAsJsonAsync("v1/batch/objects", body, Json.Opts, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<VectorRecord?> GetAsync(string collection, string id, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"v1/objects/{Uri.EscapeDataString(collection)}/{Uri.EscapeDataString(id)}?include=vector", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var vec = doc.RootElement.TryGetProperty("vector", out var v)
            ? v.EnumerateArray().Select(e => e.GetSingle()).ToArray()
            : Array.Empty<float>();
        return new VectorRecord(id, vec);
    }

    public async Task DeleteAsync(string collection, string id, CancellationToken ct = default)
    {
        var resp = await _http.DeleteAsync($"v1/objects/{Uri.EscapeDataString(collection)}/{Uri.EscapeDataString(id)}", ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        string collection, float[] queryVector, int topK = 10, float minScore = 0f,
        VectorFilter? filter = null, CancellationToken ct = default)
    {
        // Weaviate GraphQL nearVector query.
        var gql = $$"""
            { Get { {{collection}}(nearVector: { vector: [{{string.Join(",", queryVector.Select(f => f.ToString(System.Globalization.CultureInfo.InvariantCulture)))}}], certainty: {{minScore.ToString(System.Globalization.CultureInfo.InvariantCulture)}} }, limit: {{topK}}) { _additional { id certainty } text_content } } }
            """;
        var resp = await _http.PostAsJsonAsync("v1/graphql", new { query = gql }, Json.Opts, ct);
        resp.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var results = new List<VectorSearchResult>();
        if (!doc.RootElement.TryGetProperty("data", out var data)
            || !data.TryGetProperty("Get", out var get)
            || !get.TryGetProperty(collection, out var items))
            return results;
        foreach (var item in items.EnumerateArray())
        {
            var add = item.TryGetProperty("_additional", out var a) ? a : default;
            var rid = add.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
            var score = add.TryGetProperty("certainty", out var cert) ? cert.GetSingle() : 0f;
            results.Add(new VectorSearchResult(rid, score));
        }
        return results;
    }
}

/// <summary>VD-02 — Weaviate Cloud managed vector store.</summary>
public sealed class WeaviateCloudStore : WeaviateVectorStoreBase
{
    public override VectorDbProviderType ProviderType => VectorDbProviderType.WeaviateCloud;
    public WeaviateCloudStore(HttpClient http) : base(http) { }
}

/// <summary>VD-12 — Weaviate self-hosted vector store (same REST protocol as cloud).</summary>
public sealed class WeaviateSelfHostedStore : WeaviateVectorStoreBase
{
    public override VectorDbProviderType ProviderType => VectorDbProviderType.WeaviateSelfHosted;
    public WeaviateSelfHostedStore(HttpClient http) : base(http) { }
}

// ─── VD-04 / VD-13 — Milvus ──────────────────────────────────────────────────

/// <summary>Shared Milvus REST v2 implementation (Zilliz cloud and self-hosted share the same API contract).
/// HttpClient base address should point to the Milvus REST endpoint.
/// Set Authorization header for Zilliz: "Bearer {token}".</summary>
public abstract class MilvusVectorStoreBase : IVectorStore
{
    private readonly HttpClient _http;
    public abstract VectorDbProviderType ProviderType { get; }

    protected MilvusVectorStoreBase(HttpClient http)
        => _http = http ?? throw new ArgumentNullException(nameof(http));

    public async Task EnsureCollectionAsync(string collection, int dimensions, CancellationToken ct = default)
    {
        var body = new
        {
            collectionName = collection,
            schema = new
            {
                autoId = false,
                enableDynamicField = true,
                fields = new object[]
                {
                    new
                    {
                        fieldName = "id",
                        dataType = "VarChar",
                        isPrimary = true,
                        elementTypeParams = new
                        {
                            max_length = "128",
                        },
                    },
                    new
                    {
                        fieldName = "vector",
                        dataType = "FloatVector",
                        elementTypeParams = new
                        {
                            dim = dimensions.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        },
                    },
                },
            },
            indexParams = new object[]
            {
                new
                {
                    fieldName = "vector",
                    indexName = "vector",
                    metricType = "COSINE",
                },
            },
        };
        var resp = await _http.PostAsJsonAsync("v2/vectordb/collections/create", body, Json.Opts, ct);
        // Code 65535 = collection already exists — safe to ignore.
        if (!resp.IsSuccessStatusCode)
        {
            var content = await resp.Content.ReadAsStringAsync(ct);
            if (!content.Contains("65535")) resp.EnsureSuccessStatusCode();
        }

        await PostMilvusAdminCommandAsync("v2/vectordb/collections/load", collection, ct);
    }

    public async Task DeleteCollectionAsync(string collection, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("v2/vectordb/collections/drop",
            new { collectionName = collection }, Json.Opts, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<string>> ListCollectionsAsync(CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("v2/vectordb/collections/list", new { }, Json.Opts, ct);
        resp.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        if (!doc.RootElement.TryGetProperty("data", out var data)) return Array.Empty<string>();
        return data.EnumerateArray().Select(e => e.GetString() ?? "").Where(s => s.Length > 0).ToList();
    }

    public async Task UpsertAsync(string collection, VectorRecord record, CancellationToken ct = default)
    {
        var body = new
        {
            collectionName = collection,
            data = new[] { new { id = record.Id, vector = record.Vector, text_content = record.Text } },
        };
        var resp = await _http.PostAsJsonAsync("v2/vectordb/entities/upsert", body, Json.Opts, ct);
        resp.EnsureSuccessStatusCode();
        await FlushAndLoadCollectionAsync(collection, ct);
    }

    public async Task UpsertBatchAsync(string collection, IReadOnlyList<VectorRecord> records, CancellationToken ct = default)
    {
        var body = new
        {
            collectionName = collection,
            data = records.Select(r => new { id = r.Id, vector = r.Vector, text_content = r.Text }),
        };
        var resp = await _http.PostAsJsonAsync("v2/vectordb/entities/upsert", body, Json.Opts, ct);
        resp.EnsureSuccessStatusCode();
        await FlushAndLoadCollectionAsync(collection, ct);
    }

    public async Task<VectorRecord?> GetAsync(string collection, string id, CancellationToken ct = default)
    {
        var byIdBody = new { collectionName = collection, id = new[] { id }, outputFields = new[] { "id", "vector", "text_content" } };
        var byIdResponse = await _http.PostAsJsonAsync("v2/vectordb/entities/get", byIdBody, Json.Opts, ct);
        byIdResponse.EnsureSuccessStatusCode();
        using var byIdDoc = await JsonDocument.ParseAsync(await byIdResponse.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var byIdRecord = ParseMilvusRecord(id, byIdDoc);
        if (byIdRecord is not null)
            return byIdRecord;

        var escapedId = id.Replace("\"", "\\\"");
        var queryBody = new
        {
            collectionName = collection,
            filter = $"id in [\"{escapedId}\"]",
            outputFields = new[] { "id", "vector", "text_content" }
        };
        var queryResponse = await _http.PostAsJsonAsync("v2/vectordb/entities/query", queryBody, Json.Opts, ct);
        queryResponse.EnsureSuccessStatusCode();
        using var queryDoc = await JsonDocument.ParseAsync(await queryResponse.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return ParseMilvusRecord(id, queryDoc);
    }

    private static VectorRecord? ParseMilvusRecord(string id, JsonDocument doc)
    {
        if (!doc.RootElement.TryGetProperty("data", out var data))
            return null;

        var arr = data.EnumerateArray().ToList();
        if (arr.Count == 0)
            return null;

        var first = arr[0];
        var vec = first.TryGetProperty("vector", out var v)
            ? v.EnumerateArray().Select(e => e.GetSingle()).ToArray()
            : Array.Empty<float>();

        return new VectorRecord(id, vec,
            first.TryGetProperty("text_content", out var t) ? t.GetString() : null);
    }

    public async Task DeleteAsync(string collection, string id, CancellationToken ct = default)
    {
        var escapedId = id.Replace("\"", "\\\"");
        var resp = await _http.PostAsJsonAsync("v2/vectordb/entities/delete",
            new { collectionName = collection, filter = $"id in [\"{escapedId}\"]" }, Json.Opts, ct);
        resp.EnsureSuccessStatusCode();
        await FlushAndLoadCollectionAsync(collection, ct);
    }

    private async Task FlushAndLoadCollectionAsync(string collection, CancellationToken ct)
    {
        await PostMilvusAdminCommandAsync("v2/vectordb/collections/flush", collection, ct);
        await PostMilvusAdminCommandAsync("v2/vectordb/collections/load", collection, ct);
    }

    private async Task PostMilvusAdminCommandAsync(string endpoint, string collection, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync(endpoint, new { collectionName = collection }, Json.Opts, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        string collection, float[] queryVector, int topK = 10, float minScore = 0f,
        VectorFilter? filter = null, CancellationToken ct = default)
    {
        var body = new
        {
            collectionName = collection,
            data = new[] { queryVector },
            annsField = "vector",
            limit = topK,
            outputFields = new[] { "id", "text_content" },
        };
        var resp = await _http.PostAsJsonAsync("v2/vectordb/entities/search", body, Json.Opts, ct);
        resp.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var results = new List<VectorSearchResult>();
        if (!doc.RootElement.TryGetProperty("data", out var data)) return results;
        foreach (var item in data.EnumerateArray())
        {
            var score = item.TryGetProperty("distance", out var d) ? d.GetSingle() : 0f;
            if (score < minScore) continue;
            var rid = item.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
            var text = item.TryGetProperty("text_content", out var tEl) ? tEl.GetString() : null;
            results.Add(new VectorSearchResult(rid, score, text));
        }
        return results;
    }
}

/// <summary>VD-04 — Milvus Cloud (Zilliz) vector store.</summary>
public sealed class MilvusCloudStore : MilvusVectorStoreBase
{
    public override VectorDbProviderType ProviderType => VectorDbProviderType.MilvusCloud;
    public MilvusCloudStore(HttpClient http) : base(http) { }
}

/// <summary>VD-13 — Milvus self-hosted vector store.</summary>
public sealed class MilvusSelfHostedStore : MilvusVectorStoreBase
{
    public override VectorDbProviderType ProviderType => VectorDbProviderType.MilvusSelfHosted;
    public MilvusSelfHostedStore(HttpClient http) : base(http) { }
}

// ─── VD-05 / VD-14 — Chroma ──────────────────────────────────────────────────

/// <summary>Shared Chroma HTTP API implementation. Collection = Chroma collection name.
/// HttpClient base address must point to the Chroma server (e.g. http://localhost:8000/).</summary>
public abstract class ChromaVectorStoreBase : IVectorStore
{
    private const string ChromaCollectionsPath = "api/v2/tenants/default_tenant/databases/default_database/collections";
    private readonly HttpClient _http;
    public abstract VectorDbProviderType ProviderType { get; }

    protected ChromaVectorStoreBase(HttpClient http)
        => _http = http ?? throw new ArgumentNullException(nameof(http));

    private async Task<string?> GetCollectionIdAsync(string name, CancellationToken ct)
    {
        var resp = await _http.GetAsync(ChromaCollectionsPath, ct);
        if (!resp.IsSuccessStatusCode)
            return null;

        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        foreach (var collection in doc.RootElement.EnumerateArray())
        {
            if (!collection.TryGetProperty("name", out var collectionName)
                || !string.Equals(collectionName.GetString(), name, StringComparison.Ordinal))
            {
                continue;
            }

            return collection.TryGetProperty("id", out var id) ? id.GetString() : null;
        }

        return null;
    }

    private async Task<string> GetRequiredCollectionIdAsync(string collection, CancellationToken ct)
        => await GetCollectionIdAsync(collection, ct)
            ?? throw new InvalidOperationException($"Chroma collection '{collection}' was not found.");

    private static string GetCollectionRecordsPath(string collectionId, string action)
        => $"{ChromaCollectionsPath}/{Uri.EscapeDataString(collectionId)}/{action}";

    private static IReadOnlyList<string> ParseChromaCollectionNames(JsonDocument doc)
        => doc.RootElement.EnumerateArray()
            .Select(e => e.TryGetProperty("name", out var n) ? n.GetString() : null)
            .Where(n => n is not null)
            .Select(n => n!)
            .ToList();

    private static VectorRecord? ParseChromaRecord(string id, JsonDocument doc)
    {
        var ids = doc.RootElement.TryGetProperty("ids", out var idsEl) ? idsEl.EnumerateArray().ToList() : [];
        if (ids.Count == 0)
            return null;

        var embs = doc.RootElement.TryGetProperty("embeddings", out var embEl) ? embEl.EnumerateArray().ToList() : [];
        var docs = doc.RootElement.TryGetProperty("documents", out var docsEl) ? docsEl.EnumerateArray().ToList() : [];
        var vec = embs.Count > 0 ? embs[0].EnumerateArray().Select(e => e.GetSingle()).ToArray() : Array.Empty<float>();
        var text = docs.Count > 0 ? docs[0].GetString() : null;
        return new VectorRecord(id, vec, text);
    }

    public async Task EnsureCollectionAsync(string collection, int dimensions, CancellationToken ct = default)
    {
        var body = new
        {
            name = collection,
            metadata = new Dictionary<string, string> { ["hnsw:space"] = "cosine" },
            get_or_create = true,
        };
        var resp = await _http.PostAsJsonAsync(ChromaCollectionsPath, body, Json.Opts, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task DeleteCollectionAsync(string collection, CancellationToken ct = default)
    {
        var resp = await _http.DeleteAsync($"{ChromaCollectionsPath}/{Uri.EscapeDataString(collection)}", ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<string>> ListCollectionsAsync(CancellationToken ct = default)
    {
        var resp = await _http.GetAsync(ChromaCollectionsPath, ct);
        resp.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return ParseChromaCollectionNames(doc);
    }

    public async Task UpsertAsync(string collection, VectorRecord record, CancellationToken ct = default)
    {
        var collectionId = await GetRequiredCollectionIdAsync(collection, ct);
        var body = new
        {
            ids = new[] { record.Id },
            embeddings = new[] { record.Vector },
            documents = record.Text is not null ? new[] { record.Text } : null,
            metadatas = record.Metadata is not null ? new[] { record.Metadata } : null,
        };
        var resp = await _http.PostAsJsonAsync(GetCollectionRecordsPath(collectionId, "upsert"), body, Json.Opts, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task UpsertBatchAsync(string collection, IReadOnlyList<VectorRecord> records, CancellationToken ct = default)
    {
        var collectionId = await GetRequiredCollectionIdAsync(collection, ct);
        var body = new
        {
            ids = records.Select(r => r.Id).ToArray(),
            embeddings = records.Select(r => r.Vector).ToArray(),
            documents = records.Select(r => r.Text).ToArray(),
        };
        var resp = await _http.PostAsJsonAsync(GetCollectionRecordsPath(collectionId, "upsert"), body, Json.Opts, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<VectorRecord?> GetAsync(string collection, string id, CancellationToken ct = default)
    {
        var collectionId = await GetRequiredCollectionIdAsync(collection, ct);
        var body = new { ids = new[] { id }, include = new[] { "embeddings", "documents" } };
        var resp = await _http.PostAsJsonAsync(GetCollectionRecordsPath(collectionId, "get"), body, Json.Opts, ct);
        resp.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return ParseChromaRecord(id, doc);
    }

    public async Task DeleteAsync(string collection, string id, CancellationToken ct = default)
    {
        var collectionId = await GetRequiredCollectionIdAsync(collection, ct);
        var body = new { ids = new[] { id } };
        var resp = await _http.PostAsJsonAsync(GetCollectionRecordsPath(collectionId, "delete"), body, Json.Opts, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        string collection, float[] queryVector, int topK = 10, float minScore = 0f,
        VectorFilter? filter = null, CancellationToken ct = default)
    {
        var collectionId = await GetRequiredCollectionIdAsync(collection, ct);
        var body = new
        {
            query_embeddings = new[] { queryVector },
            n_results = topK,
            include = new[] { "distances", "documents" },
        };
        var resp = await _http.PostAsJsonAsync(GetCollectionRecordsPath(collectionId, "query"), body, Json.Opts, ct);
        resp.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var results = new List<VectorSearchResult>();
        if (!doc.RootElement.TryGetProperty("ids", out var idsRoot)) return results;
        var ids = idsRoot.EnumerateArray().First().EnumerateArray().ToList();
        var dists = doc.RootElement.TryGetProperty("distances", out var distRoot)
            ? distRoot.EnumerateArray().First().EnumerateArray().ToList()
            : new List<JsonElement>();
        for (int i = 0; i < ids.Count; i++)
        {
            // Chroma returns L2 distance; convert to similarity: 1/(1+d)
            var dist = dists.Count > i ? dists[i].GetSingle() : 0f;
            var score = 1f / (1f + dist);
            if (score < minScore) continue;
            results.Add(new VectorSearchResult(ids[i].GetString() ?? "", score));
        }
        return results;
    }
}

/// <summary>VD-05 — Chroma DB managed cloud vector store.</summary>
public sealed class ChromaDbCloudStore : ChromaVectorStoreBase
{
    public override VectorDbProviderType ProviderType => VectorDbProviderType.ChromaDB;
    public ChromaDbCloudStore(HttpClient http) : base(http) { }
}

/// <summary>VD-14 — Chroma DB self-hosted vector store.</summary>
public sealed class ChromaDbSelfHostedStore : ChromaVectorStoreBase
{
    public override VectorDbProviderType ProviderType => VectorDbProviderType.ChromaSelfHosted;
    public ChromaDbSelfHostedStore(HttpClient http) : base(http) { }
}

// ─── VD-18 — Vespa ───────────────────────────────────────────────────────────

/// <summary>VD-18 — Vespa big data + vector search store (Document and Query REST API).
/// Collection maps to a Vespa document schema name.
/// HttpClient base address must point to the Vespa container endpoint (e.g. http://localhost:8080/).</summary>
public sealed class VespaStore : IVectorStore
{
    private readonly HttpClient _http;
    public VectorDbProviderType ProviderType => VectorDbProviderType.Vespa;

    public VespaStore(HttpClient http)
        => _http = http ?? throw new ArgumentNullException(nameof(http));

    // Vespa schemas are defined at deployment time via application package.
    public Task EnsureCollectionAsync(string collection, int dimensions, CancellationToken ct = default) => Task.CompletedTask;
    public Task DeleteCollectionAsync(string collection, CancellationToken ct = default) => Task.CompletedTask;
    public Task<IReadOnlyList<string>> ListCollectionsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

    public async Task UpsertAsync(string collection, VectorRecord record, CancellationToken ct = default)
    {
        var body = new
        {
            fields = new
            {
                embedding = new
                {
                    values = record.Vector,
                },
                text_content = record.Text,
            }
        };
        var resp = await _http.PostAsJsonAsync(
            $"document/v1/{Uri.EscapeDataString(collection)}/{Uri.EscapeDataString(collection)}/docid/{Uri.EscapeDataString(record.Id)}",
            body, Json.Opts, ct);
        await EnsureSuccessAsync(resp, $"upsert '{record.Id}'", ct);
    }

    public async Task UpsertBatchAsync(string collection, IReadOnlyList<VectorRecord> records, CancellationToken ct = default)
    {
        foreach (var r in records) await UpsertAsync(collection, r, ct);
    }

    public async Task<VectorRecord?> GetAsync(string collection, string id, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync(
            $"document/v1/{Uri.EscapeDataString(collection)}/{Uri.EscapeDataString(collection)}/docid/{Uri.EscapeDataString(id)}", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(resp, $"get '{id}'", ct);
        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        if (!doc.RootElement.TryGetProperty("fields", out var fields)) return null;
        var vec = fields.TryGetProperty("embedding", out var emb)
            ? ReadVespaVector(emb)
            : Array.Empty<float>();
        return new VectorRecord(id, vec,
            fields.TryGetProperty("text_content", out var t) ? t.GetString() : null);
    }

    public async Task DeleteAsync(string collection, string id, CancellationToken ct = default)
    {
        var resp = await _http.DeleteAsync(
            $"document/v1/{Uri.EscapeDataString(collection)}/{Uri.EscapeDataString(collection)}/docid/{Uri.EscapeDataString(id)}", ct);
        await EnsureSuccessAsync(resp, $"delete '{id}'", ct);
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        string collection, float[] queryVector, int topK = 10, float minScore = 0f,
        VectorFilter? filter = null, CancellationToken ct = default)
    {
        var yql = $"select * from {collection} where {{targetHits:{topK}}}nearestNeighbor(embedding,query_embedding)";
        var vecStr = "[" + string.Join(",", queryVector.Select(f => f.ToString(System.Globalization.CultureInfo.InvariantCulture))) + "]";
        var tensorValue = $"tensor<float>(x[{queryVector.Length}]):{vecStr}";
        var url = $"search/?yql={Uri.EscapeDataString(yql)}&ranking=vector&hits={topK}&input.query(query_embedding)={Uri.EscapeDataString(tensorValue)}";
        var resp = await _http.GetAsync(url, ct);
        await EnsureSuccessAsync(resp, $"search '{collection}'", ct);
        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var results = new List<VectorSearchResult>();
        if (!doc.RootElement.TryGetProperty("root", out var root)
            || !root.TryGetProperty("children", out var children)) return results;
        foreach (var hit in children.EnumerateArray())
        {
            var score = hit.TryGetProperty("relevance", out var rel) ? rel.GetSingle() : 0f;
            if (score < minScore) continue;
            var rid = hit.TryGetProperty("id", out var idEl) ? NormalizeVespaDocumentId(idEl.GetString()) : "";
            results.Add(new VectorSearchResult(rid, score));
        }
        return results;
    }

    private static string NormalizeVespaDocumentId(string? rawId)
    {
        if (string.IsNullOrWhiteSpace(rawId))
            return string.Empty;

        var separatorIndex = rawId.LastIndexOf("::", StringComparison.Ordinal);
        return separatorIndex >= 0 && separatorIndex + 2 < rawId.Length
            ? rawId[(separatorIndex + 2)..]
            : rawId;
    }

    private static float[] ReadVespaVector(JsonElement embedding)
    {
        if (embedding.ValueKind == JsonValueKind.Array)
            return embedding.EnumerateArray().Select(e => e.GetSingle()).ToArray();

        if (embedding.ValueKind == JsonValueKind.Object
            && embedding.TryGetProperty("values", out var values)
            && values.ValueKind == JsonValueKind.Array)
        {
            return values.EnumerateArray().Select(e => e.GetSingle()).ToArray();
        }

        return Array.Empty<float>();
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string operation, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(ct);
        throw new HttpRequestException(
            $"Vespa {operation} failed with {(int)response.StatusCode} ({response.ReasonPhrase}). Body: {TruncateForError(body)}",
            null,
            response.StatusCode);
    }

    private static string TruncateForError(string? value, int maxLength = 2000)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "<empty>";

        var normalized = value.Replace("\r", string.Empty).Trim();
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength] + "...<truncated>";
    }
}

// ─── VD-20 — LanceDB ─────────────────────────────────────────────────────────

/// <summary>VD-20 — LanceDB HTTP server vector store.
/// LanceDB can run in server mode (lancedb-server) exposing a REST API.
/// HttpClient base address must point to the LanceDB server (e.g. http://localhost:8080/).
/// NOTE: LanceDB is primarily a Python-native library; the HTTP server mode is experimental.
/// This implementation targets the LanceDB Cloud REST API contract (v1).</summary>
public sealed class LanceDbStore : IVectorStore
{
    private readonly HttpClient _http;
    public VectorDbProviderType ProviderType => VectorDbProviderType.LanceDB;

    public LanceDbStore(HttpClient http)
        => _http = http ?? throw new ArgumentNullException(nameof(http));

    public async Task EnsureCollectionAsync(string collection, int dimensions, CancellationToken ct = default)
    {
        var body = new { name = collection };
        var resp = await _http.PostAsJsonAsync("v1/table/create", body, Json.Opts, ct);
        if (resp.StatusCode != System.Net.HttpStatusCode.Conflict)
            resp.EnsureSuccessStatusCode();
    }

    public async Task DeleteCollectionAsync(string collection, CancellationToken ct = default)
    {
        var resp = await _http.DeleteAsync($"v1/table/{Uri.EscapeDataString(collection)}", ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<string>> ListCollectionsAsync(CancellationToken ct = default)
    {
        var resp = await _http.GetAsync("v1/table/list", ct);
        resp.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return doc.RootElement.TryGetProperty("tables", out var t)
            ? t.EnumerateArray().Select(e => e.GetString() ?? "").ToList()
            : Array.Empty<string>();
    }

    public async Task UpsertAsync(string collection, VectorRecord record, CancellationToken ct = default)
    {
        var body = new { rows = new[] { new { id = record.Id, vector = record.Vector, text = record.Text } } };
        var resp = await _http.PostAsJsonAsync($"v1/table/{Uri.EscapeDataString(collection)}/insert", body, Json.Opts, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task UpsertBatchAsync(string collection, IReadOnlyList<VectorRecord> records, CancellationToken ct = default)
    {
        var body = new { rows = records.Select(r => new { id = r.Id, vector = r.Vector, text = r.Text }) };
        var resp = await _http.PostAsJsonAsync($"v1/table/{Uri.EscapeDataString(collection)}/insert", body, Json.Opts, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<VectorRecord?> GetAsync(string collection, string id, CancellationToken ct = default)
    {
        var body = new { filter = $"id = '{id}'" };
        var resp = await _http.PostAsJsonAsync($"v1/table/{Uri.EscapeDataString(collection)}/query", body, Json.Opts, ct);
        resp.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        if (!doc.RootElement.TryGetProperty("data", out var data)) return null;
        var rows = data.EnumerateArray().ToList();
        if (rows.Count == 0) return null;
        var row = rows[0];
        var vec = row.TryGetProperty("vector", out var v) ? v.EnumerateArray().Select(e => e.GetSingle()).ToArray() : Array.Empty<float>();
        return new VectorRecord(id, vec, row.TryGetProperty("text", out var t) ? t.GetString() : null);
    }

    public async Task DeleteAsync(string collection, string id, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync($"v1/table/{Uri.EscapeDataString(collection)}/delete",
            new { filter = $"id = '{id}'" }, Json.Opts, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        string collection, float[] queryVector, int topK = 10, float minScore = 0f,
        VectorFilter? filter = null, CancellationToken ct = default)
    {
        var body = new { vector = queryVector, limit = topK, metric = "cosine" };
        var resp = await _http.PostAsJsonAsync($"v1/table/{Uri.EscapeDataString(collection)}/search", body, Json.Opts, ct);
        resp.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var results = new List<VectorSearchResult>();
        if (!doc.RootElement.TryGetProperty("data", out var data)) return results;
        foreach (var row in data.EnumerateArray())
        {
            var score = row.TryGetProperty("_distance", out var d) ? 1f - d.GetSingle() : 0f;
            if (score < minScore) continue;
            results.Add(new VectorSearchResult(
                row.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "",
                score,
                row.TryGetProperty("text", out var t) ? t.GetString() : null));
        }
        return results;
    }
}

// ─── VD-28 — ScaNN ───────────────────────────────────────────────────────────

/// <summary>VD-28 — ScaNN (Scalable Nearest Neighbors) HTTP wrapper.
/// ScaNN is a Google-developed ANN library. This implementation targets
/// a custom HTTP server wrapping ScaNN (e.g. running via scann-server or a custom service).
/// HttpClient base address should point to the ScaNN service endpoint.</summary>
public sealed class ScaNNStore : IVectorStore
{
    private readonly HttpClient _http;
    public VectorDbProviderType ProviderType => VectorDbProviderType.ScaNN;

    public ScaNNStore(HttpClient http)
        => _http = http ?? throw new ArgumentNullException(nameof(http));

    public async Task EnsureCollectionAsync(string collection, int dimensions, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("collections",
            new { name = collection, dimensions }, Json.Opts, ct);
        if (resp.StatusCode != System.Net.HttpStatusCode.Conflict)
            resp.EnsureSuccessStatusCode();
    }

    public async Task DeleteCollectionAsync(string collection, CancellationToken ct = default)
    {
        var resp = await _http.DeleteAsync($"collections/{Uri.EscapeDataString(collection)}", ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<string>> ListCollectionsAsync(CancellationToken ct = default)
    {
        var resp = await _http.GetAsync("collections", ct);
        resp.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return doc.RootElement.ValueKind == JsonValueKind.Array
            ? doc.RootElement.EnumerateArray().Select(e => e.GetString() ?? "").ToList()
            : Array.Empty<string>();
    }

    public async Task UpsertAsync(string collection, VectorRecord record, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync($"collections/{Uri.EscapeDataString(collection)}/vectors",
            new { id = record.Id, vector = record.Vector }, Json.Opts, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task UpsertBatchAsync(string collection, IReadOnlyList<VectorRecord> records, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync($"collections/{Uri.EscapeDataString(collection)}/vectors/batch",
            new { vectors = records.Select(r => new { id = r.Id, vector = r.Vector }) }, Json.Opts, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<VectorRecord?> GetAsync(string collection, string id, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"collections/{Uri.EscapeDataString(collection)}/vectors/{Uri.EscapeDataString(id)}", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var vec = doc.RootElement.TryGetProperty("vector", out var v)
            ? v.EnumerateArray().Select(e => e.GetSingle()).ToArray() : Array.Empty<float>();
        return new VectorRecord(id, vec);
    }

    public async Task DeleteAsync(string collection, string id, CancellationToken ct = default)
    {
        var resp = await _http.DeleteAsync(
            $"collections/{Uri.EscapeDataString(collection)}/vectors/{Uri.EscapeDataString(id)}", ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        string collection, float[] queryVector, int topK = 10, float minScore = 0f,
        VectorFilter? filter = null, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync($"collections/{Uri.EscapeDataString(collection)}/search",
            new { vector = queryVector, topK }, Json.Opts, ct);
        resp.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var results = new List<VectorSearchResult>();
        if (!doc.RootElement.TryGetProperty("results", out var res)) return results;
        foreach (var item in res.EnumerateArray())
        {
            var score = item.TryGetProperty("score", out var s) ? s.GetSingle() : 0f;
            if (score < minScore) continue;
            results.Add(new VectorSearchResult(
                item.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "", score));
        }
        return results;
    }
}
