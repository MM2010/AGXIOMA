using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage;

/// <summary>Knowledge base: ingest documents, chunk, embed, store in pgvector, search.</summary>
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
        var chunks = _chunker.Chunk(document.Content, document.Type);
        if (chunks.Count == 0) return;

        var texts = chunks.Select(c => c.Text).ToList();
        var embeddings = await _embedder.EmbedBatchAsync(texts, ct);

        for (int i = 0; i < chunks.Count; i++)
            await _vectorStore.UpsertAsync(document.Id, chunks[i], embeddings[i], ct);
    }

    public async Task IngestBatchAsync(IAsyncEnumerable<Document> documents, CancellationToken ct = default)
    {
        await foreach (var doc in documents)
            await IngestAsync(doc, ct);
    }

    public async Task<IReadOnlyList<KnowledgeChunk>> SearchAsync(string query, KnowledgeSearchOptions? options = null, CancellationToken ct = default)
    {
        var opts = options ?? _options.DefaultSearchOptions;
        var qVec = await _embedder.EmbedAsync(query, ct);
        return await _vectorStore.SearchAsync(qVec, opts, ct);
    }

    public async Task DeleteAsync(string documentId, CancellationToken ct = default)
        => await _vectorStore.DeleteAsync(documentId, ct);
}

/// <summary>Options for knowledge base configuration.</summary>
public record KnowledgeBaseOptions
{
    public int DefaultTopK { get; init; } = 10;
    public double MinRelevanceScore { get; init; } = 0.65;
    public int MaxContextTokens { get; init; } = 4096;
    public bool EnableMetadataFiltering { get; init; } = true;
    public KnowledgeSearchOptions DefaultSearchOptions { get; init; } = new();
}
