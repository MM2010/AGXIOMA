# Storage & RAG — AGXIOME

Persistenza CQRS con PostgreSQL, Redis cache, outbox pattern, RAG con pgvector.

---

## CQRS Architecture

```
Command (write)                    Query (read)
    │                                  │
    ▼                                  ▼
┌──────────┐                    ┌────────────┐
│ Command   │                    │  Redis     │
│ Handler   │                    │  Cache     │
└────┬─────┘                    └──────┬─────┘
     │                                 │
     ▼                                 ▼
┌──────────┐                    ┌────────────┐
│ PostgreSQL │                    │  PostgreSQL │
│ (canonical)│                    │  (query)    │
└────┬──────┘                    └────────────┘
     │
     ▼
┌──────────┐
│  Outbox  │──► Event Bus
└──────────┘
```

## PostgreSQL

```sql
-- Sessioni
CREATE TABLE sessions (
    id VARCHAR(64) PRIMARY KEY,
    user_id VARCHAR(64) NOT NULL,
    component_id VARCHAR(64) NOT NULL,
    type VARCHAR(16) NOT NULL,
    name VARCHAR(255),
    session_state JSONB DEFAULT '{}',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Messaggi cronologia
CREATE TABLE session_messages (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id VARCHAR(64) REFERENCES sessions(id) ON DELETE CASCADE,
    role VARCHAR(32) NOT NULL,
    content TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Knowledge chunks (pgvector)
CREATE TABLE knowledge_chunks (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    doc_id VARCHAR(64) NOT NULL,
    chunk_index INT NOT NULL,
    content TEXT NOT NULL,
    embedding VECTOR(1536),
    metadata_json JSONB DEFAULT '{}',
    UNIQUE (doc_id, chunk_index)
);
CREATE INDEX idx_chunks_embedding ON knowledge_chunks
    USING IVFFLAT (embedding vector_cosine_ops) WITH (lists = 100);

-- Outbox
CREATE TABLE outbox_messages (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    event_type VARCHAR(255) NOT NULL,
    event_body_json JSONB NOT NULL,
    processed_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

## Redis Cache

```csharp
var cache = new RedisCache("localhost:6379,abortConnect=false");

await cache.SetAsync("agent:config:123", config, TimeSpan.FromMinutes(5));
var cached = await cache.GetAsync<AgentConfig>("agent:config:123");
await cache.InvalidateAsync("agent:config:*");
```

## Unit of Work

```csharp
using var uow = new UnitOfWork(connectionString);
await uow.BeginAsync(ct);
try
{
    await uow.Commands.SaveAsync(entity, ct);
    await uow.CommitAsync(ct);
}
catch
{
    await uow.RollbackAsync(ct);
    throw;
}
```

## Outbox Pattern

```csharp
var outbox = new OutboxWriter(connectionString);

// In una transazione:
await uow.Commands.SaveAsync(aggregate, ct);
await outbox.WriteAsync(domainEvent, "agent.run.completed", ct);
await uow.CommitAsync(ct);

// Poller in background:
var poller = new OutboxPoller(connectionString);
var processed = await poller.ProcessBatchAsync(batchSize: 50, ct);
```

## RAG — Knowledge Base

### Ingest

```csharp
var knowledge = new KnowledgeBase(
    new HeadingChunker(),
    llmwikiyEngine,       // o qualsiasi IEmbedder
    new PgVectorStore(connectionString)
);

await knowledge.IngestAsync(new Document(
    "doc-1",
    "# AI Introduction\nArtificial intelligence is...",
    DocumentType.Markdown
));
```

### Search

```csharp
var results = await knowledge.SearchAsync(
    "What is artificial intelligence?",
    new KnowledgeSearchOptions { TopK = 5, MinScore = 0.65 }
);

foreach (var chunk in results)
    Console.WriteLine($"[{chunk.Score:P1}] {chunk.Content}");
```

### Chunker disponibili

| Chunker | Descrizione |
|---------|-------------|
| `FixedSizeChunker(512, 64)` | Split per conteggio parole + overlap |
| `HeadingChunker(1024)` | Split per heading markdown `#`/`##` |
| `SentenceChunker(512)` | Split per frase `. ! ?` |

### Vector store

- **PgVectorStore**: ANN search su PostgreSQL 16+ con pgvector
- Indice IVFFlat configurable (`lists = 100`)
- Search types: `Vector` (default), `Keyword`, `Hybrid`
- Metadata filtering push-down
- Score = cosine similarity (1.0 = identico, 0.0 = ortogonale)
