---
title: MistralEmbedder
category: entity
tags: [embedder, mistral, embeddings, IEmbedder]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# MistralEmbedder

## Summary
`MistralEmbedder` is an implementation of the [[IEmbedder]] interface that generates embeddings using the Mistral AI `mistral-embed` model via an OpenAI-compatible `/v1/embeddings` endpoint. It wraps an internal `OpenAICompatEmbedderClient` to handle HTTP communication and returns 1024-dimensional vectors. Configuration is provided through `MistralEmbedderOptions`, which requires an API key and allows customization of the base URL and model name.

## Key Details
- Implements `IEmbedder` and `IDisposable`.
- Embedder type is `EmbedderType.Mistral`.
- Fixed embedding dimensionality of 1024.
- Uses the `mistral-embed` model by default (configurable via options).
- API base URL defaults to `https://api.mistral.ai/`.
- Authentication is done via a Bearer token set in the `Authorization` header.
- Delegates actual embedding calls to an internal `OpenAICompatEmbedderClient`, making it compatible with many OpenAI-style embedding APIs.
- Provides `EmbedAsync(string, CancellationToken)` for single texts and `EmbedBatchAsync(IReadOnlyList<string>, CancellationToken)` for batch processing.
- Constructor takes an `HttpClient` and `MistralEmbedderOptions`; the options object is validated for null.
- The `HttpClient.BaseAddress` is set only if not already assigned.

## Cross-References
- [[IEmbedder]] — Interface implemented by MistralEmbedder.
- [[EmbedderType]] — Enum value `Mistral` used by this embedder.
- [[MistralEmbedderOptions]] — Configuration record required to instantiate the embedder.

## Source Files
- `AGXIOME.Storage/Embedders/MistralEmbedder.cs` (ingested 2026-05-14)