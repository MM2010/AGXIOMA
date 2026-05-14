---
title: SemanticChunker
category: entity
tags: [chunker, semantic-splitting, embedding, IChunker]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# SemanticChunker

## Summary
The `SemanticChunker` implements `IChunker` to split text into semantically coherent segments. It uses an optional `IEmbedder` to compute cosine similarity between sentence embeddings, inserting boundaries where similarity falls below a configurable threshold. When no embedder is provided, it degrades gracefully to syntactic sentence-boundary splitting, ensuring it always returns a valid result.

## Key Details
- **Algorithm**: Splits text into sentences, embeds each sentence (batch call), computes pairwise cosine similarity, inserts boundaries where similarity < `SimilarityThreshold` (default 0.75), then merges oversized segments via `RecursiveChunker` fallback.
- **Graceful Degradation**: If no `IEmbedder` is supplied, behaves like `SentenceChunker` – no embedding calls, boundaries inserted every ~3 sentences.
- **Async-First**: Preferred `ChunkAsync` method for production use; synchronous `Chunk` blocks on the async path (safe for console/test but not ASP.NET Core).
- **ChunkerType** returns `ChunkerType.Semantic`.
- **Overlap**: Applied as tail-carry from the previous segment using a character budget (default `overlap * 4`). Source offsets are approximate.
- **Token Budget**: Uses `maxTokens * 4` as a character budget per segment. Segments exceeding that budget are recursively split without overlap.
- **Threshold Impact**: Lower threshold = fewer, larger chunks; higher threshold = more, finer-grained chunks.

## Cross-References
- [[IChunker]] — the interface that SemanticChunker implements.
- [[IEmbedder]] — external embedding service used for semantic similarity.
- [[SentenceChunker]] — fallback behavior when no embedder is provided.
- [[RecursiveChunker]] — fallback used for oversized segments.

## Source Files
- `AGXIOME.Storage/Chunkers/SemanticChunker.cs` (ingested 2026-05-14)