---
title: OnnxRerankers
category: entity
tags: [onnx, reranker, embedding, cosine-similarity]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# OnnxRerankers

## Summary
The `OnnxEmbeddingReranker` base class and its concrete subclasses (`BgeOnnxReranker`, `CrossEncoderOnnxReranker`, `MixedBreadOnnxReranker`) provide offline ONNX-based document reranking using cosine similarity of pre-computed embeddings. To avoid a compile-time dependency on `Microsoft.ML.OnnxRuntime`, scoring is approximated by measuring the directional similarity between query and candidate vectors obtained from an injected `IEmbedder` adapter. This design keeps the core library lean while still supporting effective search ranking for popular ONNX reranker models.

## Key Details
- **Base class**: `OnnxEmbeddingReranker` (abstract) implements `IReranker`.
- **Options**: `OnnxRerankerOptions` requires an `IEmbedder` instance.
- Scoring uses `SemanticChunker.CosineSimilarity()` on query and document embeddings.
- Embeddings are obtained in two batch calls (`EmbedAsync` and `EmbedBatchAsync`).
- Result is a list of `RerankResult` ordered by descending score, with optional `topK` truncation.
- Concrete subclasses each expose a distinct `RerankerType`:
  - `BgeOnnxReranker` → `RerankerType.BgeOnnx`
  - `CrossEncoderOnnxReranker` → `RerankerType.CrossEncoderOnnx`
  - `MixedBreadOnnxReranker` → `RerankerType.MixedBreadOnnx`
- The cosine-similarity approximation is noted as architecturally correct; a real cross-encoder scores query+document jointly.

## Cross-References
- [[IReranker]] — interface implemented by all rerankers
- [[RerankerType]] — enum values used by concrete ONNX rerankers
- [[IEmbedder]] — injected dependency providing embedding vectors
- [[RerankResult]] — output record containing id, text, score, ranks, and metadata
- [[RerankCandidate]] — input record for reranking
- [[SemanticChunker]] — provides the `CosineSimilarity` utility method

## Source Files
- `AGXIOME.Storage/Rerankers/OnnxRerankers.cs` (ingested 2026-05-14)