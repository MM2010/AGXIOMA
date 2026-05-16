using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Errors;
using AGXIOME.Shared.Models;
using System.Collections.Concurrent;

namespace AGXIOME.Storage.VectorStores;

// ─────────────────────────────────────────────────────────────────────────────
// VD-25 — HNSWLibStore   (in-memory HNSW-inspired flat index)
// VD-26 — FaissStore     (in-memory flat exact L2/cosine index)
// VD-27 — AnnoyStore     (in-memory tree-based approximate NN)
//
// All three providers implement the IVectorStore interface with pure C#
// in-memory storage. No external dependencies are required.
//
// Architectural note:
//   HNSWLib, FAISS, and Annoy are native C++ libraries without stable .NET 10
//   NuGet wrappers at the time of implementation.  These stores deliver the
//   correct contract behaviour (exact cosine similarity) and are fully testable
//   without a live server.  When a production-grade .NET binding becomes
//   available, swap the search kernel while keeping the public interface intact.
//
// Performance:
//   All three use brute-force cosine similarity (O(n·d) per query).  This is
//   exact (not approximate) and is suitable for development and moderate
//   collection sizes (<100k vectors).  For production ANN use a dedicated
//   hosted vector service instead.
// ─────────────────────────────────────────────────────────────────────────────

// Shared thread-safe collection storage.
internal sealed class InMemoryVectorCollection
{
    private readonly ConcurrentDictionary<string, VectorRecord> _store = new(StringComparer.Ordinal);

    public bool TryAdd(VectorRecord r) { _store[r.Id] = r; return true; }
    public bool TryGet(string id, out VectorRecord? r) => _store.TryGetValue(id, out r);
    public bool TryRemove(string id) => _store.TryRemove(id, out _);
    public IEnumerable<VectorRecord> All() => _store.Values;
}

// ─── VD-25 — HNSWLib ─────────────────────────────────────────────────────────

/// <summary>VD-25 — HNSWLib in-memory vector store.
/// Implements HNSW-style interface using exact brute-force cosine similarity.
/// Fully functional without native bindings; upgrade to HNSW kernel for O(log n) ANN.</summary>
public sealed class HNSWLibStore : IVectorStore
{
    private readonly ConcurrentDictionary<string, InMemoryVectorCollection> _collections = new(StringComparer.Ordinal);
    public VectorDbProviderType ProviderType => VectorDbProviderType.HNSWLib;

    public Task EnsureCollectionAsync(string collection, int dimensions, CancellationToken ct = default)
    {
        _collections.GetOrAdd(collection, _ => new InMemoryVectorCollection());
        return Task.CompletedTask;
    }

    public Task DeleteCollectionAsync(string collection, CancellationToken ct = default)
    {
        _collections.TryRemove(collection, out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> ListCollectionsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<string>>(_collections.Keys.OrderBy(k => k).ToList());

    public Task UpsertAsync(string collection, VectorRecord record, CancellationToken ct = default)
    {
        GetOrThrow(collection).TryAdd(record);
        return Task.CompletedTask;
    }

    public Task UpsertBatchAsync(string collection, IReadOnlyList<VectorRecord> records, CancellationToken ct = default)
    {
        var col = GetOrThrow(collection);
        foreach (var r in records) col.TryAdd(r);
        return Task.CompletedTask;
    }

    public Task<VectorRecord?> GetAsync(string collection, string id, CancellationToken ct = default)
    {
        GetOrThrow(collection).TryGet(id, out var r);
        return Task.FromResult(r);
    }

    public Task DeleteAsync(string collection, string id, CancellationToken ct = default)
    {
        GetOrThrow(collection).TryRemove(id);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        string collection, float[] queryVector, int topK = 10, float minScore = 0f,
        VectorFilter? filter = null, CancellationToken ct = default)
    {
        var results = GetOrThrow(collection).All()
            .Select(r => new VectorSearchResult(r.Id, CosineSimilarity(queryVector, r.Vector), r.Text))
            .Where(r => r.Score >= minScore)
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .ToList();
        return Task.FromResult<IReadOnlyList<VectorSearchResult>>(results);
    }

    private InMemoryVectorCollection GetOrThrow(string name) =>
        _collections.TryGetValue(name, out var c) ? c
            : throw AgxiomeStorageException.CollectionMissing("HNSWLib", name);

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0) return 0f;
        float dot = 0f, normA = 0f, normB = 0f;
        for (int i = 0; i < a.Length; i++) { dot += a[i] * b[i]; normA += a[i] * a[i]; normB += b[i] * b[i]; }
        var denom = MathF.Sqrt(normA) * MathF.Sqrt(normB);
        return denom < float.Epsilon ? 0f : dot / denom;
    }
}

// ─── VD-26 — FAISS ───────────────────────────────────────────────────────────

/// <summary>VD-26 — FAISS in-memory flat vector index (exact cosine similarity).
/// Models FAISS IndexFlatIP (inner product after L2-normalisation).
/// No native FAISS bindings required; swap kernel when stable .NET binding lands.</summary>
public sealed class FaissStore : IVectorStore
{
    private readonly ConcurrentDictionary<string, InMemoryVectorCollection> _collections = new(StringComparer.Ordinal);
    public VectorDbProviderType ProviderType => VectorDbProviderType.FAISS;

    public Task EnsureCollectionAsync(string collection, int dimensions, CancellationToken ct = default)
    {
        _collections.GetOrAdd(collection, _ => new InMemoryVectorCollection());
        return Task.CompletedTask;
    }

    public Task DeleteCollectionAsync(string collection, CancellationToken ct = default)
    {
        _collections.TryRemove(collection, out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> ListCollectionsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<string>>(_collections.Keys.OrderBy(k => k).ToList());

    public Task UpsertAsync(string collection, VectorRecord record, CancellationToken ct = default)
    {
        GetOrThrow(collection).TryAdd(NormalizeRecord(record));
        return Task.CompletedTask;
    }

    public Task UpsertBatchAsync(string collection, IReadOnlyList<VectorRecord> records, CancellationToken ct = default)
    {
        var col = GetOrThrow(collection);
        foreach (var r in records) col.TryAdd(NormalizeRecord(r));
        return Task.CompletedTask;
    }

    public Task<VectorRecord?> GetAsync(string collection, string id, CancellationToken ct = default)
    {
        GetOrThrow(collection).TryGet(id, out var r);
        return Task.FromResult(r);
    }

    public Task DeleteAsync(string collection, string id, CancellationToken ct = default)
    {
        GetOrThrow(collection).TryRemove(id);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        string collection, float[] queryVector, int topK = 10, float minScore = 0f,
        VectorFilter? filter = null, CancellationToken ct = default)
    {
        // FAISS IndexFlatIP uses inner product on L2-normalised vectors = cosine similarity.
        var normQuery = L2Normalize(queryVector);
        var results = GetOrThrow(collection).All()
            .Select(r => new VectorSearchResult(r.Id, InnerProduct(normQuery, r.Vector), r.Text))
            .Where(r => r.Score >= minScore)
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .ToList();
        return Task.FromResult<IReadOnlyList<VectorSearchResult>>(results);
    }

    private static VectorRecord NormalizeRecord(VectorRecord r) =>
        new(r.Id, L2Normalize(r.Vector), r.Text, r.Metadata);

    private static float[] L2Normalize(float[] v)
    {
        float norm = 0f;
        for (int i = 0; i < v.Length; i++) norm += v[i] * v[i];
        norm = MathF.Sqrt(norm);
        if (norm < float.Epsilon) return v;
        var result = new float[v.Length];
        for (int i = 0; i < v.Length; i++) result[i] = v[i] / norm;
        return result;
    }

    private static float InnerProduct(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0f;
        float sum = 0f;
        for (int i = 0; i < a.Length; i++) sum += a[i] * b[i];
        return sum;
    }

    private InMemoryVectorCollection GetOrThrow(string name) =>
        _collections.TryGetValue(name, out var c) ? c
            : throw AgxiomeStorageException.CollectionMissing("FAISS", name);
}

// ─── VD-27 — Annoy ───────────────────────────────────────────────────────────

/// <summary>VD-27 — Annoy (Approximate Nearest Neighbours Oh Yeah) in-memory store.
/// Models Annoy's random projection tree interface with exact brute-force search.
/// No C# binding to the native Annoy library is currently stable;
/// swap the search kernel for approximate search when a binding ships.</summary>
public sealed class AnnoyStore : IVectorStore
{
    private readonly ConcurrentDictionary<string, InMemoryVectorCollection> _collections = new(StringComparer.Ordinal);
    public VectorDbProviderType ProviderType => VectorDbProviderType.Annoy;

    public Task EnsureCollectionAsync(string collection, int dimensions, CancellationToken ct = default)
    {
        _collections.GetOrAdd(collection, _ => new InMemoryVectorCollection());
        return Task.CompletedTask;
    }

    public Task DeleteCollectionAsync(string collection, CancellationToken ct = default)
    {
        _collections.TryRemove(collection, out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> ListCollectionsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<string>>(_collections.Keys.OrderBy(k => k).ToList());

    public Task UpsertAsync(string collection, VectorRecord record, CancellationToken ct = default)
    {
        GetOrThrow(collection).TryAdd(record);
        return Task.CompletedTask;
    }

    public Task UpsertBatchAsync(string collection, IReadOnlyList<VectorRecord> records, CancellationToken ct = default)
    {
        var col = GetOrThrow(collection);
        foreach (var r in records) col.TryAdd(r);
        return Task.CompletedTask;
    }

    public Task<VectorRecord?> GetAsync(string collection, string id, CancellationToken ct = default)
    {
        GetOrThrow(collection).TryGet(id, out var r);
        return Task.FromResult(r);
    }

    public Task DeleteAsync(string collection, string id, CancellationToken ct = default)
    {
        GetOrThrow(collection).TryRemove(id);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        string collection, float[] queryVector, int topK = 10, float minScore = 0f,
        VectorFilter? filter = null, CancellationToken ct = default)
    {
        // Annoy uses angular distance = 2(1 - cosine_similarity) for angular metric.
        // Return cosine similarity for consistency with other stores.
        var results = GetOrThrow(collection).All()
            .Select(r => new VectorSearchResult(r.Id, CosineSimilarity(queryVector, r.Vector), r.Text))
            .Where(r => r.Score >= minScore)
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .ToList();
        return Task.FromResult<IReadOnlyList<VectorSearchResult>>(results);
    }

    private InMemoryVectorCollection GetOrThrow(string name) =>
        _collections.TryGetValue(name, out var c) ? c
            : throw AgxiomeStorageException.CollectionMissing("Annoy", name);

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0) return 0f;
        float dot = 0f, normA = 0f, normB = 0f;
        for (int i = 0; i < a.Length; i++) { dot += a[i] * b[i]; normA += a[i] * a[i]; normB += b[i] * b[i]; }
        var denom = MathF.Sqrt(normA) * MathF.Sqrt(normB);
        return denom < float.Epsilon ? 0f : dot / denom;
    }
}
