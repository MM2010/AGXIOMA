using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Errors;
using AGXIOME.Shared.Models;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Transport;
using Meilisearch;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AGXIOME.Storage.VectorStores;

// ─────────────────────────────────────────────────────────────────────────────
// VD-15 — ElasticsearchDenseStore
// VD-16 — MeilisearchVectorStore
//
// Both search-engine-based vector stores. Each maps a collection to an
// index name within the respective search engine.
// ─────────────────────────────────────────────────────────────────────────────

// ─── VD-15 — Elasticsearch Dense kNN ─────────────────────────────────────────

/// <summary>VD-15 — Elasticsearch dense vector store using kNN (HNSW) search.
/// Requires an Elasticsearch version that supports dense_vector and matches the Elastic client major version.
/// Collection = Elasticsearch index name.
/// Vectors stored with dot_product similarity; pre-normalize vectors for cosine behaviour.</summary>
public sealed class ElasticsearchDenseStore : IVectorStore
{
    private readonly ElasticsearchClient _client;
    public VectorDbProviderType ProviderType => VectorDbProviderType.ElasticsearchDense;

    private sealed class ElasticsearchVectorDocument
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("vector")]
        public float[] Vector { get; init; } = Array.Empty<float>();

        [JsonPropertyName("text_content")]
        public string? TextContent { get; init; }

        [JsonPropertyName("metadata")]
        public Dictionary<string, object>? Metadata { get; init; }
    }

    public ElasticsearchDenseStore(ElasticsearchClient client)
        => _client = client ?? throw new ArgumentNullException(nameof(client));

    public async Task EnsureCollectionAsync(string collection, int dimensions, CancellationToken ct = default)
    {
        var exists = await _client.Indices.ExistsAsync(collection, ct);
        if (exists.Exists) return;

        var response = await _client.Indices.CreateAsync(collection, cfg => cfg
            .Mappings(m => m
                .Properties(new Properties
                {
                    { "id",           new KeywordProperty() },
                    { "text_content", new TextProperty() },
                    { "metadata",     new ObjectProperty() },
                    { "vector",       new DenseVectorProperty
                        {
                            Dims = dimensions,
                            Index = true,
                            Similarity = DenseVectorSimilarity.DotProduct,
                        }
                    },
                })), ct);

            if (!response.IsValidResponse)
            {
                response.TryGetOriginalException(out var ex);
                throw AgxiomeStorageException.ProviderOperationFailed("Elasticsearch", "CreateIndex", collection, ex, isRetryable: true);
            }
    }

    public async Task DeleteCollectionAsync(string collection, CancellationToken ct = default)
    {
        var resp = await _client.Indices.DeleteAsync(collection, cancellationToken: ct);
        if (!resp.IsValidResponse && resp.ApiCallDetails.HttpStatusCode != 404)
        {
            resp.TryGetOriginalException(out var ex);
            throw AgxiomeStorageException.ProviderOperationFailed("Elasticsearch", "DeleteIndex", collection, ex, isRetryable: true);
        }
    }

    public async Task<IReadOnlyList<string>> ListCollectionsAsync(CancellationToken ct = default)
    {
        var stats = await _client.Indices.StatsAsync(cancellationToken: ct);
        return stats.Indices?.Keys.ToList() ?? new List<string>();
    }

    public async Task UpsertAsync(string collection, VectorRecord record, CancellationToken ct = default)
    {
        var doc = ToDocument(record);
        var response = await _client.IndexAsync(doc, idx => idx.Index(collection).Id(record.Id).Refresh(Refresh.WaitFor), ct);
        if (!response.IsValidResponse)
        {
            response.TryGetOriginalException(out var ex);
            throw AgxiomeStorageException.ProviderOperationFailed("Elasticsearch", "UpsertDocument", $"{collection}/{record.Id}", ex, isRetryable: true);
        }
    }

    public async Task UpsertBatchAsync(string collection, IReadOnlyList<VectorRecord> records, CancellationToken ct = default)
    {
        foreach (var record in records)
        {
            var response = await _client.IndexAsync(ToDocument(record), idx => idx.Index(collection).Id(record.Id), ct);
            if (!response.IsValidResponse)
            {
                response.TryGetOriginalException(out var ex);
                throw AgxiomeStorageException.ProviderOperationFailed("Elasticsearch", "BulkUpsert", $"{collection}/{record.Id}", ex, isRetryable: true);
            }
        }

        var refresh = await _client.Indices.RefreshAsync(collection, cancellationToken: ct);
        if (!refresh.IsValidResponse)
        {
            refresh.TryGetOriginalException(out var ex);
            throw AgxiomeStorageException.ProviderOperationFailed("Elasticsearch", "BulkRefresh", collection, ex, isRetryable: true);
        }
    }

    public async Task<VectorRecord?> GetAsync(string collection, string id, CancellationToken ct = default)
    {
        var resp = await _client.GetAsync<ElasticsearchVectorDocument>(new GetRequest(collection, id), ct);
        if (!resp.Found) return null;
        var src = resp.Source;
        return src is null
            ? null
            : new VectorRecord(src.Id, src.Vector, src.TextContent, src.Metadata);
    }

    public async Task DeleteAsync(string collection, string id, CancellationToken ct = default)
    {
        var response = await _client.DeleteAsync(new DeleteRequest(collection, id) { Refresh = Refresh.WaitFor }, ct);
        if (!response.IsValidResponse && response.ApiCallDetails.HttpStatusCode != 404)
        {
            response.TryGetOriginalException(out var ex);
            throw AgxiomeStorageException.ProviderOperationFailed("Elasticsearch", "DeleteDocument", $"{collection}/{id}", ex, isRetryable: true);
        }
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        string collection, float[] queryVector, int topK = 10, float minScore = 0f,
        VectorFilter? filter = null, CancellationToken ct = default)
    {
        var resp = await _client.SearchAsync<ElasticsearchVectorDocument>(s => s
            .Indices(collection)
            .Knn(k => k
                .Field("vector")
                .QueryVector(queryVector)
                .K(topK)
                .NumCandidates(topK * 10)), ct);

        if (!resp.IsValidResponse) return Array.Empty<VectorSearchResult>();
        return resp.Hits
            .Where(h => h.Score is null || h.Score >= minScore)
            .Select(h =>
            {
                var src = h.Source;
                return new VectorSearchResult(
                    h.Id ?? src?.Id ?? "",
                    (float)(h.Score ?? 0f),
                    src?.TextContent);
            }).ToList();
    }

    private static ElasticsearchVectorDocument ToDocument(VectorRecord record)
        => new()
        {
            Id = record.Id,
            Vector = record.Vector,
            TextContent = record.Text,
            Metadata = record.Metadata,
        };

}

// ─── VD-16 — Meilisearch Vector ───────────────────────────────────────────────

/// <summary>VD-16 — Meilisearch hybrid (semantic + full-text) vector store.
/// Requires Meilisearch v1.6+ with vector store feature enabled.
/// Collection = Meilisearch index UID.</summary>
public sealed class MeilisearchVectorStore : IVectorStore
{
    private readonly MeilisearchClient _client;
    private const double TASK_TIMEOUT_MS = 30000;
    private const int TASK_POLL_INTERVAL_MS = 200;
    public VectorDbProviderType ProviderType => VectorDbProviderType.MeilisearchVector;

    public MeilisearchVectorStore(MeilisearchClient client)
        => _client = client ?? throw new ArgumentNullException(nameof(client));

    public async Task EnsureCollectionAsync(string collection, int dimensions, CancellationToken ct = default)
    {
        var index = _client.Index(collection);
        // Create index if it does not exist; Meilisearch auto-creates but we ensure settings.
        var task = await index.UpdateSettingsAsync(new Settings
        {
            SearchableAttributes = new[] { "text_content" },
            FilterableAttributes = new[] { "id" },
        }, ct);
        await WaitForTaskCompletionAsync(task.TaskUid, "UpdateSettings", collection, ct);
    }

    public async Task DeleteCollectionAsync(string collection, CancellationToken ct = default)
    {
        var task = await _client.DeleteIndexAsync(collection, ct);
        await WaitForTaskCompletionAsync(task.TaskUid, "DeleteIndex", collection, ct);
    }

    public async Task<IReadOnlyList<string>> ListCollectionsAsync(CancellationToken ct = default)
    {
        var stats = await _client.GetAllIndexesAsync();
        return stats.Results.Select(i => i.Uid).ToList();
    }

    public async Task UpsertAsync(string collection, VectorRecord record, CancellationToken ct = default)
    {
        var doc = new MeilisearchVectorDoc
        {
            Id = record.Id,
            TextContent = record.Text,
            // Persist vectors as a normal field until AGXIOME wires a real Meilisearch embedder contract.
            Vectors = record.Vector,
        };
        var task = await _client.Index(collection).AddDocumentsAsync(new[] { doc }, primaryKey: "id", cancellationToken: ct);
        await WaitForTaskCompletionAsync(task.TaskUid, "AddDocument", collection, ct);
    }

    public async Task UpsertBatchAsync(string collection, IReadOnlyList<VectorRecord> records, CancellationToken ct = default)
    {
        var docs = records.Select(r => new MeilisearchVectorDoc
        {
            Id = r.Id,
            TextContent = r.Text,
            Vectors = r.Vector,
        }).ToList();
        var task = await _client.Index(collection).AddDocumentsAsync(docs, primaryKey: "id", cancellationToken: ct);
        await WaitForTaskCompletionAsync(task.TaskUid, "AddDocuments", collection, ct);
    }

    public async Task<VectorRecord?> GetAsync(string collection, string id, CancellationToken ct = default)
    {
        try
        {
            var doc = await _client.Index(collection).GetDocumentAsync<MeilisearchVectorDoc>(id, (List<string>?)null, ct);
            return new VectorRecord(id, doc.Vectors ?? Array.Empty<float>(), doc.TextContent);
        }
        catch
        {
            return null;
        }
    }

    public async Task DeleteAsync(string collection, string id, CancellationToken ct = default)
    {
        var task = await _client.Index(collection).DeleteOneDocumentAsync(id, ct);
        await WaitForTaskCompletionAsync(task.TaskUid, "DeleteDocument", collection, ct);
    }

    private async Task WaitForTaskCompletionAsync(int taskUid, string operation, string target, CancellationToken ct)
    {
        try
        {
            await _client.WaitForTaskAsync(taskUid, TASK_TIMEOUT_MS, TASK_POLL_INTERVAL_MS, ct);
        }
        catch (MeilisearchTimeoutError ex)
        {
            throw AgxiomeStorageException.ProviderOperationFailed("Meilisearch", operation, target, ex, isRetryable: true);
        }
        catch (Exception ex)
        {
            throw AgxiomeStorageException.ProviderOperationFailed("Meilisearch", operation, target, ex, isRetryable: true);
        }
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        string collection, float[] queryVector, int topK = 10, float minScore = 0f,
        VectorFilter? filter = null, CancellationToken ct = default)
    {
        // Meilisearch v1.6+ supports vector search when embedder is configured.
        // Here we issue a standard full-text wildcard search and rely on
        // _rankingScore for ordering; replace with semantic search when embedder config is injected.
        var results = await _client.Index(collection).SearchAsync<MeilisearchVectorDoc>(
            "",
            new SearchQuery
            {
                Limit = topK,
                ShowRankingScore = true,
            }, ct);
        return results.Hits
            .Select(h => new VectorSearchResult(
                h.Id,
                (float)(h.RankingScore ?? 0.0),
                h.TextContent))
            .Where(r => r.Score >= minScore)
            .ToList();
    }

    // Internal DTO — kept file-private to avoid namespace pollution.
    private sealed class MeilisearchVectorDoc
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public string Id { get; set; } = "";
        [System.Text.Json.Serialization.JsonPropertyName("text_content")]
        public string? TextContent { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("vector")]
        public float[]? Vectors { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("_rankingScore")]
        public double? RankingScore { get; set; }
    }
}
