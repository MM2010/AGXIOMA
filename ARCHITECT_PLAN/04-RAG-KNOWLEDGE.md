# 04 — RAG & Knowledge

## Scopo

Recupero informazione semantica: document ingestion → embedding → vector search → context injection → LLMWIKY inference.

## Architettura

```
Document Source (file, API, DB, stream)
         │
         ▼
┌─────────────────────┐
│   Ingestion Pipeline │
│  ┌───────────────┐  │
│  │  Chunker      │  │  → 512-1024 token chunks
│  │  (per type)   │  │  → sentence | heading | function boundary
│  └───────┬───────┘  │
│  ┌───────┴───────┐  │
│  │  Embedder     │  │  → LLMWIKY embed()
│  │  (batch)      │  │  → Upsert PostgreSQL pgvector
│  └───────────────┘  │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│   Vector Index      │  → IVFFlat (default) | HNSW
│   (PostgreSQL)      │  → Partitioned by tenant_id
└──────────┬──────────┘
           │
           ▼
Agent Query ──► Embed(query) ──► ANN Search ──► Metadata Filter
                                      │
                                      ▼
                              ┌──────────────────┐
                              │  Context Window   │
                              │  (top-K chunks)   │
                              │  max 4K tokens    │
                              └────────┬─────────┘
                                       │
                                       ▼
                              ┌──────────────────┐
                              │  LLMWIKY         │
                              │  Inference       │
                              │  + sources       │
                              └──────────────────┘
```

## Contratti

```csharp
// ── Knowledge Base ──
public interface IKnowledgeBase
{
    Task IngestAsync(Document document, CancellationToken ct);
    Task IngestBatchAsync(IAsyncEnumerable<Document> documents, CancellationToken ct);
    Task DeleteAsync(string documentId, CancellationToken ct);
    Task<IReadOnlyList<KnowledgeChunk>> SearchAsync(
        string query,
        KnowledgeSearchOptions? options = null,
        CancellationToken ct = default);
}

// ── Document types ──
public record Document(
    string Id,
    string Content,
    DocumentType Type,
    Dictionary<string, string>? Metadata = null
);

public enum DocumentType
{
    PlainText,
    Markdown,
    Code,
    Pdf,        // Future
    Html,       // Future
    Csv         // Future
}

// ── Search options ──
public record KnowledgeSearchOptions(
    int TopK = 10,
    double MinScore = 0.65,
    Dictionary<string, string>? MetadataFilter = null,
    bool IncludeEmbedding = false,
    string? TenantId = null
);

// ── Result chunk ──
public record KnowledgeChunk(
    string DocumentId,
    string Content,
    float Score,
    Dictionary<string, string>? Metadata,
    float[]? Embedding = null
);
```

## Chunking strategy per tipo

| DocumentType | Chunker | Chunk size | Overlap |
|-------------|---------|-----------|---------|
| PlainText | Sentence split (NLP) | 512 token | 64 token |
| Markdown | Heading boundary | 1024 token | 128 token |
| Code | Function/class boundary | 512 token | 32 token |
| Pdf (future) | Page + paragraph | 1024 token | 128 token |

```csharp
public interface IChunker
{
    IReadOnlyList<Chunk> Chunk(string content, DocumentType type);
}

public record Chunk(
    string Text,
    int Index,
    int StartOffset,
    int EndOffset,
    Dictionary<string, string>? Metadata = null
);
```

## Embedding pipeline

```csharp
public interface IEmbedder
{
    Task<float[]> EmbedAsync(string text, CancellationToken ct);
    Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct);
    int Dimensions { get; }
}
```

- Default: 1536-dim (OpenAI-compat, configurabile)
- Batch size: 20 chunks per batch
- Retry: 3 tentativi con backoff su rate-limit
- Cache: embedding già calcolati per chunk identici (hash-based)

## PostgreSQL query pattern (pgvector)

```sql
-- Nearest neighbor search con metadata filter
SELECT
    id,
    content,
    1 - (embedding <=> @queryEmbedding) AS score,
    metadata_json
FROM agent_memory
WHERE
    agent_id = @agentId
    AND metadata_json @> @metadataFilter
ORDER BY embedding <=> @queryEmbedding
LIMIT @topK;

-- Per similarità coseno: <=> operator returns cosine distance
-- score = 1 - distance
```

## RAG pipeline execution

```
1. Agent genera query embedding via LLMWIKY
2. PostgreSQL: ANN search con filter push-down (tenant, date, category)
3. Top-K chunk (default 10) raccolti
4. Context window assembly:
   - System prompt + chunks formattati + user query
   - Max 4K tokens complessivi
5. LLMWIKY inference con context arricchito
6. Response + source attribution (documentId, chunkIndex, score)
7. (Opzionale) Logging telemetry: latency, chunks retrieved, score medio
```

## Knowledge Base Builder

```csharp
public interface IKnowledgeBaseBuilder
{
    IKnowledgeBaseBuilder UseChunker(IChunker chunker);
    IKnowledgeBaseBuilder UseEmbedder(IEmbedder embedder);
    IKnowledgeBaseBuilder WithStore(IKnowledgeStore store);
    IKnowledgeBaseBuilder WithOptions(KnowledgeBaseOptions options);
    IKnowledgeBase Build();
}

public class KnowledgeBaseOptions
{
    public int DefaultTopK { get; set; } = 10;
    public double MinRelevanceScore { get; set; } = 0.65;
    public int MaxContextTokens { get; set; } = 4096;
    public bool EnableMetadataFiltering { get; set; } = true;
    public bool EnableSourceCitation { get; set; } = true;
}
```

## Security RAG

- Tenant isolation: ogni embedding ha `tenant_id` obbligatorio
- Metadata filter obbligatorio su tenant
- Content encryption a riposo (PostgreSQL pgcrypto)
- Audit log: ogni query RAG loggata (query hash, chunk count, latency)

## Benchmark target

| Scenario | p95 | p99 |
|----------|-----|-----|
| Ingest singolo chunk | 100 ms | 250 ms |
| Ingest batch (20 chunk) | 800 ms | 1.5 s |
| Search 10K docs | 40 ms | 80 ms |
| Search 100K docs | 120 ms | 250 ms |
| Full RAG cycle | 600 ms | 1.2 s |
