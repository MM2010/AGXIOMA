---
title: Voyage and Jina Embedders
category: entity
tags: [embedder, voyage, jina, AI, embeddings]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Voyage and Jina Embedders

## Summary
Two embedder implementations for the AGXIOME framework that produce 1024‑dimensional vector embeddings via third‑party APIs. The `VoyageEmbedder` uses Voyage AI’s proprietary schema (`voyage-3-large`), while the `JinaEmbedder` uses the OpenAI‑compatible schema (`jina-embeddings-v3`). Both implement the `IEmbedder` interface and support single‑text and batch embedding operations.

## Key Details
- **VoyageEmbedder** – `EmbedderType.VoyageAI`, dimensions 1024, model `voyage-3-large`, `input_type` defaults to `"document"`, POSTs to `v1/embeddings` with Voyage AI’s non‑OpenAI payload.
- **JinaEmbedder** – `EmbedderType.JinaAI`, dimensions 1024, model `jina-embeddings-v3`, `task` defaults to `"retrieval.passage"`, uses OpenAI‑compatible request schema.
- Both require an API key injected via options (`VoyageEmbedderOptions.ApiKey`, `JinaEmbedderOptions.ApiKey`).
- Base URLs are configurable; default Voyage: `https://api.voyageai.com/`, Jina: `https://api.jina.ai/`.
- Authentication: Bearer token set on the `HttpClient` via `DefaultRequestHeaders.Authorization`.
- Single‑text `EmbedAsync` delegates to `EmbedBatchAsync` and returns the first result.
- Batch method orders response data by `Index` to preserve input order.
- `Dispose` cleans up the injected `HttpClient`.
- Both deserialize response JSON into private nested records (`VoyageEmbedResponse`, `JinaEmbedResponse`) with `Data` lists of `VoyageEmbedData`/`JinaEmbedData` (each containing `Index` and `Embedding` float array).

## Cross-References
- [[IEmbedder]] – interface each embedder implements
- [[EmbedderType]] – enum used by `EmbedderType` property
- [[OpenAIEmbedder]] – another embedder with similar pattern, OpenAI‑compatible schema

## Source Files
- `AGXIOME.Storage/Embedders/VoyageAndJinaEmbedders.cs` (ingested 2026-05-14)