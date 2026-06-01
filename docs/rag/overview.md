# RAG Pipeline (Retrieval-Augmented Generation)

The RAG pipeline enriches LLM responses with relevant context from your documents.

## Pipeline

```
Documents (PDF, DOCX, MD, HTML, ...)
    │
    ▼
Reader ──→ Chunker ──→ Embedder ──→ Vector Store
                                        │
User Query ──→ Embed ──→ Vector Search (top-K)
                              │
                        Context + Query
                              │
                            LLM ──→ Response (with citations)
```

## Full Pipeline Code

```csharp
// 1. Setup components
var reader = new PdfReader();
var chunker = new RecursiveChunker(chunkSize: 512, overlap: 64);
var embedder = new OpenAISmallEmbedder("sk-...");
var vectorStore = new PgVectorStore(connectionString, dimensions: 1536);

// 2. Ingest documents
var documents = await reader.ReadAsync("contracts/");
var chunks = chunker.Chunk(documents);

foreach (var chunk in chunks)
{
    var embedding = await embedder.EmbedAsync(chunk.Content);
    await vectorStore.UpsertAsync(new VectorRecord(
        chunk.Id, embedding, chunk.Content,
        new Dictionary<string, object> { ["source"] = chunk.Source }));
}

// 3. Query
var query = "What is the termination clause in the vendor contract?";
var queryEmbedding = await embedder.EmbedAsync(query);
var results = await vectorStore.SearchAsync(queryEmbedding, topK: 5);

// 4. Generate response with context
var context = string.Join("\n\n", results.Select(r => r.Text));
var prompt = $"""
    Answer the question using ONLY the provided context.
    If the answer is not in the context, say "I don't know."

    Context:
    {context}

    Question: {query}
    """;

var response = await agent.RunAsync(new AgentRequest(prompt));
```

## Ingestion with KnowledgeBase

```csharp
var kb = new PostgresKnowledgeBase(connectionString);

// Single document
await kb.IngestAsync(new Document("doc-1", File.ReadAllText("report.pdf")));

// Batch
var docs = Directory.GetFiles("docs/", "*.md")
    .Select(f => new Document(Path.GetFileName(f), File.ReadAllText(f)));
await kb.IngestBatchAsync(docs.ToAsyncEnumerable());

// Search
var results = await kb.SearchAsync("What is the refund policy?",
    new KnowledgeSearchOptions { TopK = 5, MinScore = 0.7f });
```

## Chunking Strategies

| Chunker | Best For | Overlap |
|---------|----------|---------|
| **RecursiveChunker** | General text | Yes |
| **SemanticChunker** | Meaningful boundaries | Yes |
| **SlidingWindowChunker** | Streaming/long docs | Yes |
| **CodeChunker** | Source code (functions/classes) | No |
| **DocumentChunker** | Structured docs (headings) | No |
| **AgenticChunker** | LLM-decided boundaries | No |

## Vector Store Backends

| Backend | Type | Setup |
|---------|------|-------|
| **pgvector** | PostgreSQL extension | `CREATE EXTENSION vector` |
| **Qdrant** | Self-hosted / Cloud | Docker / cloud |
| **Chroma** | Embedded | In-process |
| **Pinecone** | Cloud | API key |
| **Milvus** | Self-hosted / Cloud | Docker / Zilliz |

## Next

- [Document Ingestion](ingestion.md)
- [Chunking Strategies](chunking.md)
- [Vector Search](vector-search.md)
