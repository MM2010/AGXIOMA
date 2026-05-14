---
title: HuggingFace and NIM Embedders
category: entity
tags: [embedder, huggingface, nvidia, nim, embeddings]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# HuggingFace and NIM Embedders

## Summary
Two concrete implementations of the `IEmbedder` interface for generating vector embeddings of text: the **HuggingFace Embedder** uses the Hugging Face Inference API (feature-extraction pipeline) and performs mean pooling of token-level embeddings to obtain fixed-size vectors; the **NVIDIA NIM Embedder** communicates with NVIDIA’s OpenAI-compatible embedding endpoint supporting models like NV-Embed. Both embedders handle single and batch requests, manage authentication via API keys, and are designed for use within the AGXIOME framework's storage and retrieval pipeline.

## Key Details
- **HuggingFaceEmbedder** (`AGXIOME.Storage.Embedders`):
  - Configured via `HuggingFaceEmbedderOptions` with `ApiKey`, `BaseUrl` (default `https://api-inference.huggingface.co/`), `Model` (default `sentence-transformers/all-MiniLM-L6-v2`), and `Dimensions` (default 384).
  - Sends POST to `/pipeline/feature-extraction/{model}` with `wait_for_model` option.
  - Accepts single text or batch; returns `float[]` or `float[][]`.
  - For token-level responses (2D array), performs mean pooling across all tokens.
  - Returns zero vector for empty/null input.
- **NvidiaNimEmbedder** (`AGXIOME.Storage.Embedders`):
  - Configured via `NvidiaNimEmbedderOptions` with `ApiKey`, `BaseUrl` (default `https://integrate.api.nvidia.com/v1/`), `Model` (default `nvidia/nv-embed-v1`), `InputType`, and `TruncateMode`.
  - Sends POST to `/embeddings` with OpenAI-compatible payload including `input`, `model`, `input_type`, `truncate`, `encoding_format`.
  - Returns 1024-dimensional embeddings (fixed).
  - Parses JSON response of shape `{ data: [{ index, embedding }, …] }`, ordering results by index.
- Both classes implement `IDisposable` and dispose their internal `HttpClient`.
- Both embedders throw on HTTP errors via `EnsureSuccessStatusCode()`.
- Embedder type is exposed via `EmbedderType` property (enum values `HuggingFaceApi` and `NvidiaNIM`).

## Cross-References
- [[IEmbedder]] — Interface implemented by both embedders.
- [[EmbedderType]] — Enum containing `HuggingFaceApi` and `NvidiaNIM`.
- [[StorageEmbedders]] — Overview of all embedder implementations in the framework.

## Source Files
- `AGXIOME.Storage/Embedders/HuggingFaceAndNimEmbedders.cs` (ingested 2026-05-14)