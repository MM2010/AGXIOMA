using AGXIOME.Shared.Models;

namespace AGXIOME.Shared.Interfaces;

// ─────────────────────────────────────────────────────────────────────────────
// IEmbedder — produces dense float vectors for texts.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Single-text embedding contract.
/// All implementations must be thread-safe and async-only (no blocking I/O).</summary>
public interface IEmbedder
{
    EmbedderType EmbedderType { get; }
    int Dimensions { get; }

    /// <summary>Embed a single text. Empty input → zero vector of Dimensions length.</summary>
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);

    /// <summary>Embed a batch. Order preserved, parallel internally where provider allows.</summary>
    Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default);
}

// ─────────────────────────────────────────────────────────────────────────────
// IChunker — splits documents into overlapping/non-overlapping chunks.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Result of a chunking operation. Each chunk carries its source offset for replay.</summary>
public record DocumentChunk(
    string Content,
    int ChunkIndex,
    int StartOffset,
    int EndOffset,
    Dictionary<string, object>? Metadata = null
);

/// <summary>Chunker contract. Implementations are stateless value-type alike — one instance, many calls.</summary>
public interface IChunker
{
    ChunkerType ChunkerType { get; }

    /// <summary>Split <paramref name="text"/> into chunks respecting <paramref name="maxTokens"/> and
    /// <paramref name="overlap"/> (token count). Returns at least one chunk even for empty input.</summary>
    IReadOnlyList<DocumentChunk> Chunk(string text, int maxTokens = 512, int overlap = 64);
}

// ─────────────────────────────────────────────────────────────────────────────
// IReranker — re-orders retrieval results by relevance to a query.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>A single candidate document/passage to rerank.</summary>
public record RerankCandidate(
    string Id,
    string Text,
    float InitialScore = 0f,
    Dictionary<string, object>? Metadata = null
);

/// <summary>Reranked result with its new score and original rank.</summary>
public record RerankResult(
    string Id,
    string Text,
    float Score,
    int OriginalRank,
    int NewRank,
    Dictionary<string, object>? Metadata = null
);

/// <summary>Reranker contract. Top-K is enforced by the implementation; 0 = return all.</summary>
public interface IReranker
{
    RerankerType RerankerType { get; }

    Task<IReadOnlyList<RerankResult>> RerankAsync(
        string query,
        IReadOnlyList<RerankCandidate> candidates,
        int topK = 0,
        CancellationToken ct = default);
}

// ─────────────────────────────────────────────────────────────────────────────
// IVectorStore — upsert / search / delete vectors with metadata.
// ─────────────────────────────────────────────────────────────────────────────

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

// ─────────────────────────────────────────────────────────────────────────────
// IDocumentReader — reads heterogeneous sources into structured DocumentContent.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>A section within a document (heading → content mapping).</summary>
public record DocumentSection(
    string? Heading,
    string Content,
    int Level = 0,
    Dictionary<string, object>? Metadata = null
);

/// <summary>Full output of a document reader.</summary>
public record DocumentContent(
    string SourceUri,
    DocumentReaderType ReaderType,
    string RawText,
    IReadOnlyList<DocumentSection> Sections,
    Dictionary<string, object>? Metadata = null
);

/// <summary>Document reader contract. URI can be a file path, HTTP URL, or provider-specific URI.
/// Throw <see cref="NotSupportedException"/> if the URI scheme is unsupported.</summary>
public interface IDocumentReader
{
    DocumentReaderType ReaderType { get; }
    IReadOnlyList<string> SupportedExtensions { get; }

    Task<DocumentContent> ReadAsync(string uri, CancellationToken ct = default);
    Task<DocumentContent> ReadFromStreamAsync(Stream stream, string? mimeHint = null, CancellationToken ct = default);
}

// ─────────────────────────────────────────────────────────────────────────────
// IToolkit — prebuilt toolkit contract (Agno parity + beyond).
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Result of a toolkit tool invocation.</summary>
public record ToolkitResult(
    bool Success,
    object? Data,
    string? ErrorMessage = null,
    TimeSpan? Duration = null,
    Dictionary<string, object>? Metadata = null
);

/// <summary>Descriptor for a single callable tool inside a toolkit.</summary>
public record ToolkitToolDescriptor(
    string Name,
    string Description,
    System.Text.Json.Nodes.JsonObject ParameterSchema
);

/// <summary>Toolkit contract. One toolkit contains 1-N named tools callable by the agent.
/// Implementations must NOT store mutable per-call state (stateless invocations).</summary>
public interface IToolkit
{
    string Name { get; }
    ToolkitCategory Category { get; }
    string Version { get; }
    IReadOnlyList<ToolkitToolDescriptor> Tools { get; }

    Task<ToolkitResult> ExecuteAsync(string toolName, Dictionary<string, object?> parameters,
        CancellationToken ct = default);
}

// ─────────────────────────────────────────────────────────────────────────────
// IDbProvider — generic async CRUD+query over any storage backend.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Paged query result.</summary>
public record DbPage<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

/// <summary>DB provider contract. The storage backend is opaque — SQL, NoSQL, KV, or time-series.
/// Implementations handle connection lifecycle and pooling internally.</summary>
public interface IDbProvider
{
    StorageProviderType ProviderType { get; }

    Task<T?> GetByIdAsync<T>(string collection, string id, CancellationToken ct = default) where T : class;
    Task<IReadOnlyList<T>> QueryAsync<T>(string collection, string? filter = null,
        int limit = 100, CancellationToken ct = default) where T : class;
    Task<DbPage<T>> QueryPagedAsync<T>(string collection, string? filter, int page, int size,
        CancellationToken ct = default) where T : class;
    Task UpsertAsync<T>(string collection, string id, T entity, CancellationToken ct = default) where T : class;
    Task DeleteAsync(string collection, string id, CancellationToken ct = default);
    Task<bool> ExistsAsync(string collection, string id, CancellationToken ct = default);
    Task<long> CountAsync(string collection, string? filter = null, CancellationToken ct = default);

    /// <summary>Ensure collection/table/index exists with the given schema hint.</summary>
    Task EnsureCollectionAsync(string collection, Type entityType, CancellationToken ct = default);
}
