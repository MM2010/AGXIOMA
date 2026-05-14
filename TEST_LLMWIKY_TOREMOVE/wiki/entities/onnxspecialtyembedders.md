---
title: ONNX Specialty Embedders
category: entity
tags: [ONNX, embedder, embedding, NLP]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# ONNX Specialty Embedders

## Summary
A set of seven pre-configured embedder classes (E-19 through E-25) that inherit from `OnnxEmbedderBase`. Each wraps a specific ONNX model (e.g., `multilingual-e5-large`, `bge-large-en-v1.5`) with standard mean‑pooling and L2 normalisation. They provide a consistent interface for producing vector embeddings via the `IEmbedder` abstraction, with model path overridable through `OnnxEmbedderOptions`.

## Key Details
- All classes are `sealed` and reside in `AGXIOME.Storage.Embedders`.
- They inherit from `OnnxEmbedderBase`, which handles ONNX session shimming, mean‑pooling, and L2 normalisation.
- Each subclass only declares `EmbedderType`, `ModelDimensions`, and `DefaultModelPath`.
- Default model path is empty (`string.Empty`) when no `OnnxEmbedderOptions` is provided; you must supply a valid `.onnx` file in production.
| Embedder class | EmbedderType | Dimensions | Source model |
|----------------|--------------|------------|--------------|
| `MultilingualE5Embedder` | `MultilingualE5` | 1024 | `multilingual-e5-large` |
| `BgeEmbedder` | `BGE` | 1024 | `bge-large-en-v1.5` |
| `GteEmbedder` | `GTE` | 1024 | `gte-large-en-v1.5` |
| `SolarEmbedder` | `SOLAR` | 4096 | `solar-embedding` |
| `M3EEmbedder` | `M3E` | 1024 | `m3e-large` |
| `BgeM3Embedder` | `BAGEM3` | 1024 | `BAAI/bge-m3` |
| `NomicV15Embedder` | `NomicV15` | 768 | `nomic-embed-text-v1.5` |
- Constructor accepts `OnnxEmbedderOptions?` (optional) to set the model path.

## Cross-References
- [[OnnxEmbedderBase]] — base class providing pooling and normalisation logic
- [[EmbedderType]] — enum values used by these embedders
- [[OnnxEmbedderOptions]] — options class that overrides the default model path

## Source Files
- `AGXIOME.Storage/Embedders/OnnxSpecialtyEmbedders.cs` (ingested 2026-05-14)