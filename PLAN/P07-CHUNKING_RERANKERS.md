# P07 — Chunking & Rerankers (12+ chunker, 8+ reranker)

> **Priority**: 🟡 P1 | **Dipende da**: — | **Usato da**: P05 (Readers), P06 (Embedders)
> **Target**: 12 chunker (Agno 7), 8 reranker (Agno 4)

---

## Chunker Esistenti (3) ✅

| # | Chunker | Status |
|---|---------|--------|
| 1 | **FixedSizeChunker** | ✅ Done |
| 2 | **HeadingChunker** | ✅ Done |
| 3 | **SentenceChunker** | ✅ Done |

---

## Chunker da Implementare (9)

### 🟡 Wave 1 — Avanzati (5)

| # | Chunker | Strategia | Package |
|---|---------|-----------|---------|
| C-01 | **RecursiveChunker** | Divide ricorsivamente con separatori `\n\n → \n → . → ? → !` | — |
| C-02 | **SemanticChunker** | Spezza dove cambia significato (embeddings similarity < threshold) | IEmbedder |
| C-03 | **AgenticChunker** | LLM decide dove spezzare in base al contenuto | ILLMProvider |
| C-04 | **CodeChunker** | Spezza per function/class/method usando Roslyn | `Microsoft.CodeAnalysis` |
| C-05 | **DocumentChunker** | Multi-livello: headings → paragraphs → sentences | — |

### 🟢 Wave 2 — Specializzati (4)

| # | Chunker | Strategia | Note |
|---|---------|-----------|------|
| C-06 | **CSV Row Chunker** | Ogni riga = chunk | Con intestazione |
| C-07 | **JSON Path Chunker** | Per path JSON | Preserva struttura |
| C-08 | **SlidingWindowChunker** | Finestra scorrevole con overlap configurable | — |
| C-09 | **HybridChunker** | Combina semantic + recursive | Best of both |

---

## Reranker da Implementare (8)

| # | Reranker | Provider | Package |
|---|----------|----------|---------|
| RK-01 | **Cohere Rerank** | `cohere:rerank-english-v3.0` | HTTP API |
| RK-02 | **Cohere Rerank Multilingual** | `cohere:rerank-multilingual-v3.0` | HTTP API |
| RK-03 | **Jina Reranker** | `jina-reranker-v2` | HTTP API |
| RK-04 | **Voyage Rerank** | `voyage-rerank-2` | HTTP API |
| RK-05 | **BGE Reranker** | `BAAI/bge-reranker-v2-m3` | ONNX |
| RK-06 | **Cross-Encoder** | `cross-encoder/ms-marco-MiniLM-L6-v2` | ONNX |
| RK-07 | **MixedBread Rerank** | `mixedbread-ai/mxbai-rerank-xsmall-v1` | ONNX |
| RK-08 | **LLM Reranker** | LLM-based relevance scoring | ILLMProvider |

---

## Interfaccia Unificata

```csharp
// ——— Chunker ———
public interface IChunker
{
    string Name { get; }
    Task<IList<Chunk>> ChunkAsync(string text, ChunkingOptions? options, CancellationToken ct);
}

public record Chunk
{
    public string Text { get; init; }
    public int Index { get; init; }
    public int StartChar { get; init; }
    public int EndChar { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}

public record ChunkingOptions
{
    public int ChunkSize { get; init; } = 500;
    public int Overlap { get; init; } = 50;
    public ChunkingStrategy Strategy { get; init; } = ChunkingStrategy.Fixed;
}

// ——— Reranker ———
public interface IReranker
{
    string Name { get; }
    Task<IList<RerankedResult>> RerankAsync(
        string query, 
        IList<string> documents, 
        RerankOptions? options,
        CancellationToken ct);
}

public record RerankedResult
{
    public int Index { get; init; }
    public string Document { get; init; }
    public float RelevanceScore { get; init; }
}

public record RerankOptions
{
    public int TopN { get; init; } = 10;
    public bool ReturnDocuments { get; init; } = true;
}
```

---

## Test per Chunker

```csharp
// Ogni chunker ha:
[Fact] public async Task Chunker_SplitsCorrectLength()
[Fact] public async Task Chunker_Overlap_IncludesContext()
[Fact] public async Task Chunker_EmptyString_ReturnsEmpty()
[Fact] public async Task Chunker_ShortText_SingleChunk()
[Fact] public async Task Chunker_Metadata_Preserved()
```

## Test per Reranker

```csharp
// Ogni reranker ha:
[Fact] public async Task Reranker_ReturnsSorted()
[Fact] public async Task Reranker_RelevantFirst()
[Fact] public async Task Reranker_TopN_Limit()
[Fact] public async Task Reranker_EmptyDocuments()
[Fact] public async Task Reranker_ScoresInRange()
```

**Totale chunker: 12 | Reranker: 8 | File: ~25 | Test: ~100**
