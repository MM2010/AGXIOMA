using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace AGXIOME.Storage.VectorStores;

// ─────────────────────────────────────────────────────────────────────────────
// VD-03 / VD-11 — Qdrant vector stores (Cloud + Self-Hosted).
//
// Uses the official Qdrant.Client SDK (gRPC + REST).
// Both cloud and self-hosted instances expose the same gRPC API; the only
// difference is endpoint URL and optional API key.
//
// Collection = Qdrant collection name.
// Similarity metric: Cosine (configured at collection creation time).
// ─────────────────────────────────────────────────────────────────────────────

public abstract class QdrantVectorStoreBase : IVectorStore
{
    // QdrantClient is thread-safe and owns an internal gRPC channel.
    private readonly QdrantClient _client;
    public abstract VectorDbProviderType ProviderType { get; }

    protected QdrantVectorStoreBase(QdrantClient client)
        => _client = client ?? throw new ArgumentNullException(nameof(client));

    public async Task EnsureCollectionAsync(string collection, int dimensions, CancellationToken ct = default)
    {
        var exists = await _client.CollectionExistsAsync(collection, ct);
        if (!exists)
        {
            await _client.CreateCollectionAsync(
                collection,
                new VectorParams
                {
                    Size = (ulong)dimensions,
                    Distance = Distance.Cosine,
                },
                cancellationToken: ct);
        }
    }

    public async Task DeleteCollectionAsync(string collection, CancellationToken ct = default)
        => await _client.DeleteCollectionAsync(collection, cancellationToken: ct);

    public async Task<IReadOnlyList<string>> ListCollectionsAsync(CancellationToken ct = default)
    {
        var collections = await _client.ListCollectionsAsync(ct);
        return collections.ToList();
    }

    public async Task UpsertAsync(string collection, VectorRecord record, CancellationToken ct = default)
    {
        var point = new PointStruct
        {
            Id = ToPointId(record.Id),
            Vectors = record.Vector,
        };
        if (record.Text is not null)
            point.Payload["text_content"] = record.Text;
        if (record.Metadata is not null)
            foreach (var kv in record.Metadata)
                point.Payload[kv.Key] = kv.Value?.ToString() ?? "";

        await _client.UpsertAsync(collection, new[] { point }, cancellationToken: ct);
    }

    public async Task UpsertBatchAsync(string collection, IReadOnlyList<VectorRecord> records, CancellationToken ct = default)
    {
        var points = records.Select(r =>
        {
            var p = new PointStruct
            {
                Id = ToPointId(r.Id),
                Vectors = r.Vector,
            };
            if (r.Text is not null) p.Payload["text_content"] = r.Text;
            return p;
        }).ToList();
        await _client.UpsertAsync(collection, points, cancellationToken: ct);
    }

    public async Task<VectorRecord?> GetAsync(string collection, string id, CancellationToken ct = default)
    {
        var points = await _client.RetrieveAsync(
            collection,
            new[] { ToPointId(id) },
            withVectors: true,
            cancellationToken: ct);
        if (points.Count == 0) return null;
        var p = points[0];
        var vec = p.Vectors?.Vector?.Data?.ToArray() ?? Array.Empty<float>();
        var text = p.Payload.TryGetValue("text_content", out var t) ? t.StringValue : null;
        return new VectorRecord(id, vec, text);
    }

    public async Task DeleteAsync(string collection, string id, CancellationToken ct = default)
        => await _client.DeleteAsync(collection, ToPointId(id), cancellationToken: ct);

    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        string collection, float[] queryVector, int topK = 10, float minScore = 0f,
        VectorFilter? filter = null, CancellationToken ct = default)
    {
        var results = await _client.SearchAsync(
            collection,
            queryVector,
            limit: (ulong)topK,
            scoreThreshold: minScore,
            cancellationToken: ct);

        return results.Select(r =>
        {
            var text = r.Payload.TryGetValue("text_content", out var t) ? t.StringValue : null;
            var id = r.Id.HasNum ? r.Id.Num.ToString() : r.Id.Uuid;
            return new VectorSearchResult(id, r.Score, text);
        }).ToList();
    }

    // Qdrant IDs are either UUID or uint64. Map string IDs via UUID parsing or hashed numeric fallback.
    private static PointId ToPointId(string id)
    {
        if (Guid.TryParse(id, out var guid))
            return new PointId { Uuid = guid.ToString() };
        // For non-UUID string IDs, use the string as UUID field directly.
        return new PointId { Uuid = id };
    }
}

/// <summary>VD-03 — Qdrant Cloud vector store.
/// <example>
/// var client = new QdrantClient("my-cluster.qdrant.io", 6334, apiKey: "key", https: true);
/// var store  = new QdrantCloudStore(client);
/// </example></summary>
public sealed class QdrantCloudStore : QdrantVectorStoreBase
{
    public override VectorDbProviderType ProviderType => VectorDbProviderType.QdrantCloud;
    public QdrantCloudStore(QdrantClient client) : base(client) { }
}

/// <summary>VD-11 — Qdrant self-hosted vector store.
/// <example>
/// var client = new QdrantClient("localhost", 6334);
/// var store  = new QdrantSelfHostedStore(client);
/// </example></summary>
public sealed class QdrantSelfHostedStore : QdrantVectorStoreBase
{
    public override VectorDbProviderType ProviderType => VectorDbProviderType.QdrantSelfHosted;
    public QdrantSelfHostedStore(QdrantClient client) : base(client) { }
}
