# P09 — RAG + Knowledge

## File: `KnowledgeBase.cs`

Namespace: `AGXIOME.Knowledge`

## Classi

### `KnowledgeBase : IKnowledgeBase`

```csharp
public sealed class KnowledgeBase : IKnowledgeBase
{
    private readonly IChunker _chunker;
    private readonly IEmbedder _embedder;
    private readonly IVectorStore _vectorStore;
    private readonly KnowledgeBaseOptions _options;

    public async Task IngestAsync(Document doc, CancellationToken ct)
    {
        // 1. Chunk
        var chunks = _chunker.Chunk(doc.Content, doc.Type);
        // 2. Embed batch
        var texts = chunks.Select(c => c.Text).ToList();
        var embeddings = await _embedder.EmbedBatchAsync(texts, ct);
        // 3. Store in pgvector
        for (int i = 0; i < chunks.Count; i++)
            await _vectorStore.UpsertAsync(doc.Id, chunks[i], embeddings[i], ct);
    }

    public async Task<IReadOnlyList<KnowledgeChunk>> SearchAsync(
        string query, KnowledgeSearchOptions? options = null, CancellationToken ct = default)
    {
        // 1. Embed query
        var qVec = await _embedder.EmbedAsync(query, ct);
        // 2. ANN search con metadata filter
        return await _vectorStore.SearchAsync(qVec, options ?? new(), ct);
    }
}
```

### Chunkers

```csharp
public interface IChunker
{
    IReadOnlyList<Chunk> Chunk(string content, DocumentType type);
}

// Implementazioni:
public class SentenceChunker : IChunker { /* Split per frase, 512 token */ }
public class HeadingChunker : IChunker { /* Split per heading markdown */ }
public class FunctionChunker : IChunker { /* Split per funzione/classe */ }
public class FixedSizeChunker : IChunker { /* Split per token count fisso */ }
public class RecursiveChunker : IChunker { /* Ricorsivo con separatori */ }
public class SemanticChunker : IChunker { /* Embedding similarity split */ }
```

### Embedders

```csharp
public interface IEmbedder
{
    int Dimensions { get; }
    Task<float[]> EmbedAsync(string text, CancellationToken ct);
    Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct);
}

// Implementazioni: LLMWIKYEngine (IEmbedder integrato)
```

### Vector Store (pgvector)

```csharp
public interface IVectorStore
{
    Task UpsertAsync(string docId, Chunk chunk, float[] embedding, CancellationToken ct);
    Task<IReadOnlyList<KnowledgeChunk>> SearchAsync(
        float[] queryVec, KnowledgeSearchOptions options, CancellationToken ct);
    Task DeleteAsync(string docId, CancellationToken ct);
}
```

### Search Types

```csharp
public enum SearchType { Vector, Keyword, Hybrid }

public record KnowledgeSearchOptions(
    int TopK = 10,
    double MinScore = 0.65,
    Dictionary<string, string>? MetadataFilter = null,
    SearchType SearchType = SearchType.Vector,
    string? TenantId = null
);
```

## RAG Pipeline

```
Query → Embed → ANN Search (IVFFlat/HNSW) → Metadata Filter
      → Top-K chunks → Context window (max 4K tokens)
      → LLMWIKY inference → Response + source citations
```

## Agentic RAG

```csharp
// Default: agente decide quando cercare (search_knowledge_base tool)
// Traditional RAG: contesto sempre iniettato
public enum RAGMode
{
    Agentic,     // Agente decide
    Traditional  // Sempre iniettato
}
```

## Test

| Test | Cosa verifica |
|------|--------------|
| Ingest + Search | Chunk retrieved |
| Metadata filter | Solo chunk con metadati match |
| Hybrid search | Vector + keyword combined |
| Chunker per tipo | Split corretto per markdown |
| Batch embedding | Tutti embeddati |
