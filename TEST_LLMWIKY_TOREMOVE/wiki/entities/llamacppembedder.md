---
title: LlamaCppEmbedder
category: entity
tags: [embedder, llama.cpp, embeddings, AGXIOME]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# LlamaCppEmbedder

## Summary
`LlamaCppEmbedder` is a sealed class that implements the [[IEmbedder]] interface to generate text embeddings by calling the llama.cpp server REST API (`POST /embedding`). It requires the server to be started with the `--embedding` flag and supports both single and batched embedding requests with concurrency control to avoid overloading the server.

## Key Details
- **Namespace:** `AGXIOME.Storage.Embedders`
- **Implements:** `IEmbedder` and `IDisposable`
- **EmbedderType:** `EmbedderType.LlamaCpp`
- **Dimensions:** Defaults to 768, but varies by loaded GGUF model; configurable via `LlamaCppEmbedderOptions.Dimensions`
- **Options record:** `LlamaCppEmbedderOptions` — `BaseUrl` (default `http://localhost:8080/`) and `Dimensions` (default 768)
- **Single embedding:** `EmbedAsync(string text, CancellationToken ct)` — posts JSON `{ "content": text }` to `/embedding` and returns the embedding array
- **Batch embedding:** `EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct)` — fans out up to 4 concurrent requests using a `SemaphoreSlim(4,4)`
- **Error handling:** Throws on non‑success HTTP status; returns zero vector for null/empty input
- **Dispose:** Disposes the injected `HttpClient`

## Cross-References
- [[IEmbedder]] — the interface this class implements
- [[EmbedderType]] — enum value used for classification
- [[LlamaCppEmbedderOptions]] — configuration record

## Source Files
- `AGXIOME.Storage/Embedders/LlamaCppEmbedder.cs` (ingested 2026-05-14)