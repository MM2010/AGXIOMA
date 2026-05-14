# P06 — Embedders (25+)

> **Priority**: 🔴 P0 | **Dipende da**: — | **Usato da**: P04 (Vector DB), P07 (Chunking)
> **Target**: 25+ embedder (Agno ha 18)

---

## Provider Esistenti (0) ❌

Nessun embedder dedicato in AGXIOME. L'embedding è delegato via LLMWIKY.

---

## Embedder da Implementare (25+)

### 🔴 Wave 1 — Cloud API (10)

| # | Embedder | Modello | Package | Dimensione |
|---|----------|---------|---------|-----------|
| E-01 | **OpenAI Embeddings** | `text-embedding-3-small/large` | HTTP API | 1536/3072 |
| E-02 | **OpenAI Embeddings v3** | `text-embedding-3-large` | HTTP API | 3072 |
| E-03 | **Anthropic Embeddings** | (via API) | HTTP API | — |
| E-04 | **Cohere Embeddings** | `embed-english-v3.0` | HTTP API | 1024/4096 |
| E-05 | **Mistral Embeddings** | `mistral-embed` | HTTP API | 1024 |
| E-06 | **Google Gemini Embed** | `text-embedding-004` | HTTP API | 768 |
| E-07 | **Voyage AI** | `voyage-3-large` | HTTP API | 1024/2048 |
| E-08 | **Jina AI** | `jina-embeddings-v3` | HTTP API | 1024/2048 |
| E-09 | **AWS Bedrock Titan** | `titan-embed-text-v2` | AWS SDK | 1024/1536 |
| E-10 | **Azure OpenAI Embed** | `text-embedding-3-large` | HTTP API | 3072 |

### 🔴 Wave 2 — Local & Open-Source (8)

| # | Embedder | Modello | Package | Dimensione |
|---|----------|---------|---------|-----------|
| E-11 | **ONNX BERT** | `all-MiniLM-L6-v2` | `Microsoft.ML.OnnxRuntime` | 384 |
| E-12 | **ONNX MPNet** | `all-mpnet-base-v2` | `Microsoft.ML.OnnxRuntime` | 768 |
| E-13 | **ONNX Instructor** | `instructor-xl` | `Microsoft.ML.OnnxRuntime` | 768 |
| E-14 | **Ollama Embed** | `nomic-embed-text` | HTTP API | 768 |
| E-15 | **Ollama Embed v2** | `mxbai-embed-large` | HTTP API | 1024 |
| E-16 | **llama.cpp Embed** | Server GGUF | HTTP API | 768+ |
| E-17 | **HuggingFace API** | Vari modelli | HTTP API | variabile |
| E-18 | **NVIDIA NIM Embed** | `NV-Embed-QA` | HTTP API | 1024 |

### 🟡 Wave 3 — Multilingual & Niche (7)

| # | Embedder | Modello | Package | Dimensione |
|---|----------|---------|---------|-----------|
| E-19 | **Multilingual E5** | `intfloat/multilingual-e5-large` | ONNX | 1024 |
| E-20 | **BGE Embedding** | `BAAI/bge-large-en-v1.5` | ONNX | 1024 |
| E-21 | **GTE Embedding** | `Alibaba-NLP/gte-large-en-v1.5` | ONNX | 1024 |
| E-22 | **SOLAR Embedding** | `upstage/solar-embedding` | HTTP API | 4096 |
| E-23 | **M3E Embedding** | `moka-ai/m3e-large` | ONNX | 1024 |
| E-24 | **BAAI/bge-m3** | Multi-lingual | ONNX | 1024 |
| E-25 | **Nomic Embed v1.5** | `nomic-ai/nomic-embed-text-v1.5` | ONNX | 768/1536 |

---

## Interfaccia Unificata

```csharp
public interface IEmbedder
{
    string Name { get; }
    int Dimension { get; }
    bool SupportsBatch { get; }
    int MaxBatchSize { get; }
    
    Task<EmbeddingResult> EmbedAsync(
        string text, 
        EmbeddingOptions? options,
        CancellationToken ct);
    
    Task<EmbeddingResult[]> EmbedBatchAsync(
        IList<string> texts, 
        EmbeddingOptions? options,
        CancellationToken ct);
}

public record EmbeddingResult
{
    public float[] Vector { get; init; }
    public int Dimension { get; init; }
    public long TokenCount { get; init; }
    public long ProcessingMs { get; init; }
}

public record EmbeddingOptions
{
    public EmbeddingType Type { get; init; } = EmbeddingType.Document; // Document vs Query
    public int? TruncateDimension { get; init; } // riduci dimensione
    public string? ModelName { get; init; }
}
```

---

## Pattern di Implementazione

```csharp
// Embedders/OpenAIEmbedder.cs
public sealed class OpenAIEmbedder : IEmbedder
{
    public string Name => "OpenAI";
    public int Dimension => _model switch
    {
        "text-embedding-3-small" => 1536,
        "text-embedding-3-large" => 3072,
        _ => 1536
    };
    
    public async Task<EmbeddingResult> EmbedAsync(
        string text, 
        EmbeddingOptions? options,
        CancellationToken ct)
    {
        // POST /v1/embeddings
        // Return vector + token count
    }
    
    public async Task<EmbeddingResult[]> EmbedBatchAsync(
        IList<string> texts, 
        EmbeddingOptions? options,
        CancellationToken ct)
    {
        // Batch POST /v1/embeddings (max 2048)
        // Rate limiting + retry
    }
}
```

```csharp
// Embedders/OnnxBertEmbedder.cs
public sealed class OnnxBertEmbedder : IEmbedder
{
    private readonly InferenceSession _session;
    
    // Carica modello ONNX all'avvio
    // Mean pooling su output BERT
    // L2 normalize
    // 384 dimensioni per MiniLM
}
```

---

## Test per Embedder

```csharp
// Ogni embedder ha:
[Fact] public async Task Embedder_Embed_ReturnsVector()
[Fact] public async Task Embedder_CorrectDimension()
[Fact] public async Task Embedder_BatchEmbed_AllReturned()
[Fact] public async Task Embedder_EmptyText_ErrorCode()
[Fact] public async Task Embedder_ConsistentLength()
[Fact] public async Task Embedder_LongText_Truncated()
[Fact] public async Task Embedder_SimilarTexts_SimilarVectors() // cosine > 0.8
```

**Totale embedder: 25 | File: ~35 | Test: ~175**
