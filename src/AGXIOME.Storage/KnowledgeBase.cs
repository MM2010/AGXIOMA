using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage;

/// <summary>Knowledge base: ingest documents, chunk, embed, store in pgvector, search.
///
/// Architecture note: each Document is stored in its own vector collection (collection name = docId).
/// This gives O(1) delete and clean isolation per document.
/// SearchAsync iterates all known collections in parallel — acceptable for moderate corpus sizes.
/// For large corpora (1M+ chunks) switch to a single-collection model with metadata filtering.</summary>
public sealed class KnowledgeBase : IKnowledgeBase
{
    private readonly IChunker _chunker;
    private readonly IEmbedder _embedder;
    private readonly IVectorStore _vectorStore;
    private readonly KnowledgeBaseOptions _options;

    public KnowledgeBase(IChunker chunker, IEmbedder embedder, IVectorStore vectorStore, KnowledgeBaseOptions? options = null)
    {
        _chunker = chunker;
        _embedder = embedder;
        _vectorStore = vectorStore;
        _options = options ?? new KnowledgeBaseOptions();
    }

    public async Task IngestAsync(Document document, CancellationToken ct = default)
    {
        var chunks = _chunker.Chunk(document.Content, _options.ChunkMaxTokens, _options.ChunkOverlap);
        if (chunks.Count == 0) return;

        // Ensure a collection exists for this document (idempotent DDL).
        await _vectorStore.EnsureCollectionAsync(document.Id, _embedder.Dimensions, ct);

        var texts = chunks.Select(c => c.Content).ToList();
        var embeddings = await _embedder.EmbedBatchAsync(texts, ct);

        var records = chunks.Select((c, i) => new VectorRecord(
            Id: i.ToString(),
            Vector: embeddings[i],
            Text: c.Content,
            Metadata: document.Metadata is null ? null
                : document.Metadata.ToDictionary(kv => kv.Key, kv => (object)kv.Value)
        )).ToList();

        await _vectorStore.UpsertBatchAsync(document.Id, records, ct);
    }

    public async Task IngestBatchAsync(IAsyncEnumerable<Document> documents, CancellationToken ct = default)
    {
        await foreach (var doc in documents.WithCancellation(ct))
            await IngestAsync(doc, ct);
    }

    public async Task<IReadOnlyList<KnowledgeChunk>> SearchAsync(
        string query,
        KnowledgeSearchOptions? options = null,
        CancellationToken ct = default)
    {
        var opts = options ?? _options.DefaultSearchOptions;
        var qVec = await _embedder.EmbedAsync(query, ct);

        // Search across all known document collections and merge results.
        var collections = await _vectorStore.ListCollectionsAsync(ct);
        if (collections.Count == 0) return [];

        var tasks = collections.Select(col =>
            _vectorStore.SearchAsync(col, qVec, opts.TopK, (float)opts.MinScore, null, ct));
        var allResults = await Task.WhenAll(tasks);

        return allResults
            .SelectMany((results, colIdx) => results.Select(r => new KnowledgeChunk(
                DocumentId: collections[colIdx],
                Content: r.Text ?? string.Empty,
                Score: r.Score,
                Metadata: r.Metadata?.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? string.Empty)
            )))
            .OrderByDescending(c => c.Score)
            .Take(opts.TopK)
            .ToList();
    }

    public async Task DeleteAsync(string documentId, CancellationToken ct = default)
        => await _vectorStore.DeleteCollectionAsync(documentId, ct);
}

/// <summary>Options for knowledge base configuration.</summary>
public record KnowledgeBaseOptions
{
    public int DefaultTopK { get; init; } = 10;
    public double MinRelevanceScore { get; init; } = 0.65;
    public int MaxContextTokens { get; init; } = 4096;
    public bool EnableMetadataFiltering { get; init; } = true;
    /// <summary>Max tokens per chunk passed to the chunker.</summary>
    public int ChunkMaxTokens { get; init; } = 512;
    /// <summary>Token overlap between consecutive chunks.</summary>
    public int ChunkOverlap { get; init; } = 64;
    public KnowledgeSearchOptions DefaultSearchOptions { get; init; } = new();
}
