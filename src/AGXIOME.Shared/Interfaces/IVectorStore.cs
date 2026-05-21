using AGXIOME.Shared.Models;

namespace AGXIOME.Shared.Interfaces;

/// <summary>A single record stored in a vector database.</summary>
public record VectorRecord(
    string Id,
    float[] Vector,
    string? Text = null,
    Dictionary<string, object>? Metadata = null
);

/// <summary>Filter expression for metadata-based pre-filtering during vector search.</summary>
public record VectorFilter(
    string? FieldName = null,
    object? FieldValue = null,
    VectorFilterOperator Operator = VectorFilterOperator.Equals,
    IReadOnlyList<VectorFilter>? And = null,
    IReadOnlyList<VectorFilter>? Or = null
);

/// <summary>Comparison operator used in <see cref="VectorFilter"/> expressions.</summary>
public enum VectorFilterOperator
{
    Equals = 0,
    NotEquals = 1,
    GreaterThan = 2,
    LessThan = 3,
    In = 4,
    NotIn = 5,
    Contains = 6,
}

/// <summary>A search result from a vector store.</summary>
public record VectorSearchResult(
    string Id,
    float Score,
    string? Text = null,
    Dictionary<string, object>? Metadata = null
);

/// <summary>Vector store contract. Collection = logical namespace (like a table/index).
/// All write operations must be idempotent on Id.</summary>
public interface IVectorStore
{
    VectorDbProviderType ProviderType { get; }

    Task EnsureCollectionAsync(string collection, int dimensions, CancellationToken ct = default);
    Task DeleteCollectionAsync(string collection, CancellationToken ct = default);
    Task<IReadOnlyList<string>> ListCollectionsAsync(CancellationToken ct = default);

    Task UpsertAsync(string collection, VectorRecord record, CancellationToken ct = default);
    Task UpsertBatchAsync(string collection, IReadOnlyList<VectorRecord> records, CancellationToken ct = default);

    Task<VectorRecord?> GetAsync(string collection, string id, CancellationToken ct = default);
    Task DeleteAsync(string collection, string id, CancellationToken ct = default);

    /// <summary>ANN search. If <paramref name="filter"/> is provided, applies metadata pre-filter.</summary>
    Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        string collection,
        float[] queryVector,
        int topK = 10,
        float minScore = 0f,
        VectorFilter? filter = null,
        CancellationToken ct = default);
}
