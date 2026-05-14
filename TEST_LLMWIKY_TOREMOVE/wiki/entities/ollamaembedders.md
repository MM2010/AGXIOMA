---
title: OllamaEmbedders
category: entity
tags: [embedder, ollama, embedding, local, nomic, mxbai]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# OllamaEmbedders

## Summary
The `OllamaEmbedders` are two concrete embedder implementations (`OllamaNomicEmbedder` and `OllamaMxbaiEmbedder`) that wrap the Ollama `/api/embed` endpoint to produce dense vector embeddings locally. They support any model served by Ollama, defaulting to `nomic-embed-text` (768 dimensions) and `mxbai-embed-large` (1024 dimensions). Both share a lightweight HTTP client and support single-text and batch embedding, making them ideal for privacy‑sensitive or offline scenarios.

## Key Details
- **Two embedders** expose the [[IEmbedder]] interface:<br>
  `OllamaNomicEmbedder` → `EmbedderType.OllamaNomic` (768 dims)<br>
  `OllamaMxbaiEmbedder` → `EmbedderType.OllamaMxbai` (1024 dims)
- Both use `OllamaEmbedClient`, an internal disposable class that posts JSON to `{BaseUrl}/api/embed` with `{ model, input }` payload.
- Options configured via `OllamaEmbedderOptions` record: `BaseUrl` (default `http://localhost:11434/`) and `Model` (default `"nomic-embed-text"` or `"mxbai-embed-large"`).
- `EmbedAsync(string)` returns a zero‑vector of `Dimensions` length for null/empty input; otherwise delegates to one‑element batch.
- `EmbedBatchAsync` returns `float[][]` with one vector per input string. Empty input returns empty array.
- `HttpClient` is injected and disposed with the embedder; base address is set if not already.
- Response parsing expects `{ "embeddings": [ [ ... ], ... ] }` per Ollama v0.3+ API.

## Cross-References
- [[IEmbedder]] — the interface these classes implement
- [[EmbedderType]] — enum used to identify each embedder variant
- [[Ollama API]] — platform that serves the `/api/embed` endpoint
- [[AGXIOME.Storage]] — namespace containing these embedders

## Source Files
- `AGXIOME.Storage/Embedders/OllamaEmbedders.cs` (ingested 2026-05-14)