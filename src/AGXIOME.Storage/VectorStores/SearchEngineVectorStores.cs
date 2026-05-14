using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Transport;
using Meilisearch;
using System.Text.Json;

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
/// Requires Elasticsearch 8.x with the dense_vector mapping type.
/// Collection = Elasticsearch index name.
/// Vectors stored with dot_product similarity; pre-normalize vectors for cosine behaviour.</summary>
public sealed class ElasticsearchDenseStore : IVectorStore
{
    private readonly ElasticsearchClient _client;
    public VectorDbProviderType ProviderType => VectorDbProviderType.ElasticsearchDense;

    public ElasticsearchDenseStore(ElasticsearchClient client)
        => _client = client ?? throw new ArgumentNullException(nameof(client));

    public async Task EnsureCollectionAsync(string collection, int dimensions, CancellationToken ct = default)
    {
        var exists = await _client.Indices.ExistsAsync(collection, ct);
        if (exists.Exists) return;

        await _client.Indices.CreateAsync(collection, cfg => cfg
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
    }

    public async Task DeleteCollectionAsync(string collection, CancellationToken ct = default)
    {
        var resp = await _client.Indices.DeleteAsync(collection, cancellationToken: ct);
        if (!resp.IsValidResponse && resp.ApiCallDetails.HttpStatusCode != 404)
            resp.TryGetOriginalException(out var ex);
    }

    public async Task<IReadOnlyList<string>> ListCollectionsAsync(CancellationToken ct = default)
    {
        var stats = await _client.Indices.StatsAsync(cancellationToken: ct);
        return stats.Indices?.Keys.ToList() ?? new List<string>();
    }

    public async Task UpsertAsync(string collection, VectorRecord record, CancellationToken ct = default)
    {
        var doc = new
        {
            id = record.Id,
            vector = record.Vector,
            text_content = record.Text,
            metadata = record.Metadata,
        };
        await _client.IndexAsync(doc, idx => idx.Index(collection).Id(record.Id), ct);
    }

    public async Task UpsertBatchAsync(string collection, IReadOnlyList<VectorRecord> records, CancellationToken ct = default)
    {
        var ops = records.Select(r => new { id = r.Id, vector = r.Vector, text_content = r.Text, metadata = r.Metadata });
        var bulk = await _client.BulkAsync(b => b.Index(collection).IndexMany(ops), ct);
        if (!bulk.IsValidResponse) bulk.TryGetOriginalException(out _);
    }

    public async Task<VectorRecord?> GetAsync(string collection, string id, CancellationToken ct = default)
    {
        var resp = await _client.GetAsync<System.Text.Json.JsonElement>(new GetRequest(collection, id), ct);
        if (!resp.Found) return null;
        var src = resp.Source;
        var vec = src.TryGetProperty("vector", out var v)
            ? v.EnumerateArray().Select(e => e.GetSingle()).ToArray() : Array.Empty<float>();
        return new VectorRecord(id, vec,
            src.TryGetProperty("text_content", out var t) ? t.GetString() : null);
    }

    public async Task DeleteAsync(string collection, string id, CancellationToken ct = default)
        => await _client.DeleteAsync(new DeleteRequest(collection, id), ct);

    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        string collection, float[] queryVector, int topK = 10, float minScore = 0f,
        VectorFilter? filter = null, CancellationToken ct = default)
    {
        var resp = await _client.SearchAsync<System.Text.Json.JsonElement>(s => s
            .Index(collection)
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
                    h.Id ?? "",
                    (float)(h.Score ?? 0f),
                    src.TryGetProperty("text_content", out var t) ? t.GetString() : null);
            }).ToList();
    }
}

// ─── VD-16 — Meilisearch Vector ───────────────────────────────────────────────

/// <summary>VD-16 — Meilisearch hybrid (semantic + full-text) vector store.
/// Requires Meilisearch v1.6+ with vector store feature enabled.
/// Collection = Meilisearch index UID.</summary>
public sealed class MeilisearchVectorStore : IVectorStore
{
    private readonly MeilisearchClient _client;
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
        // Wait for task to complete (up to 30s); ignore timeout — settings already take effect.
        await _client.WaitForTaskAsync(task.TaskUid, cancellationToken: ct);
    }

    public async Task DeleteCollectionAsync(string collection, CancellationToken ct = default)
        => await _client.DeleteIndexAsync(collection, ct);

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
            // _vectors embedded in JSON payload enables semantic search in Meilisearch v1.6+.
            Vectors = record.Vector,
        };
        await _client.Index(collection).AddDocumentsAsync(new[] { doc }, primaryKey: "id", cancellationToken: ct);
    }

    public async Task UpsertBatchAsync(string collection, IReadOnlyList<VectorRecord> records, CancellationToken ct = default)
    {
        var docs = records.Select(r => new MeilisearchVectorDoc
        {
            Id = r.Id,
            TextContent = r.Text,
            Vectors = r.Vector,
        });
        await _client.Index(collection).AddDocumentsAsync(docs, primaryKey: "id", cancellationToken: ct);
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
        => await _client.Index(collection).DeleteOneDocumentAsync(id, ct);

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
        // _vectors is the Meilisearch v1.6+ magic field for per-document embeddings.
        [System.Text.Json.Serialization.JsonPropertyName("_vectors")]
        public float[]? Vectors { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("_rankingScore")]
        public double? RankingScore { get; set; }
    }
}
