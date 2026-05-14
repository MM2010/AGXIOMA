---
title: OnnxEmbedders
category: entity
tags: [onnx, embedder, inference, BERT, MiniLM, MPNet, Instructor]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# OnnxEmbedders

## Summary
ONNX-based local embedding models that run inference in‑process using ONNX Runtime via late binding (reflection). The abstract base class `OnnxEmbedderBase` implements the standard BERT pipeline: tokenize → run session → mean pool → L2 normalise. Three concrete embedders are provided—MiniLM (384d), MPNet (768d), and Instructor (768d)—all configured via an `OnnxEmbedderOptions` record. The reflection shim avoids a hard compile‑time dependency on `Microsoft.ML.OnnxRuntime`, allowing the Storage project to remain lightweight; consumers must supply the ONNX Runtime package and a tokenizer at runtime.

## Key Details
- Abstract base: `OnnxEmbedderBase` implements `IEmbedder` and provides `EmbedAsync` and `EmbedBatchAsync`.
- `EmbedBatchAsync` runs synchronous ONNX inference on a thread‑pool thread to avoid blocking the caller’s sync context.
- Session initialisation is lazy and uses `Activator.CreateInstance` on `InferenceSession` via reflection.
- The `RunInference` method currently returns a zero vector as a placeholder; a real tokenizer (e.g., `FastBertTokenizer`) must be wired in for production use.
- Static helpers: `MeanPool` (averages token‑level embeddings) and `L2Normalize` (unit‑length normalisation in‑place).
- `OnnxEmbedderOptions` provides a `ModelPath` string for each concrete embedder.
- Concrete classes:
  - `OnnxBertMiniLMEmbedder` — `EmbedderType.OnnxBertMiniLM`, 384 dimensions.
  - `OnnxMPNetEmbedder` — `EmbedderType.OnnxMPNet`, 768 dimensions.
  - `OnnxInstructorEmbedder` — `EmbedderType.OnnxInstructor`, 768 dimensions.
- All three embedders derive from `OnnxEmbedderBase` and only override the model‑specific properties.

## Cross-References
- [[IEmbedder]] — the interface that all embedders must implement.
- [[EmbedderType]] — enum used to identify each embedder variant.
- [[ExternalEmbedder]] — alternative embedder that calls a remote HTTP API (contrast to local ONNX inference).

## Source Files
- `AGXIOME.Storage/Embedders/OnnxEmbedders.cs` (ingested 2026-05-14)