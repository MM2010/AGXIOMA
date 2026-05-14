---
title: CohereEmbedder
category: entity
tags: [embedder, cohere, embeddings, vector]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# CohereEmbedder

## Summary
The `CohereEmbedder` is an implementation of the [[IEmbedder]] interface that generates text embeddings using Cohere's `embed-english-v3.0` model (1024 dimensions). It authenticates via an API key and sends requests to the `v2/embed` endpoint with `input_type="search_document"` for document‑oriented embedding. This embedder is designed for use within the AGXIOMA storage layer and supports both single and batch embedding operations.

## Key Details
- **Dimensions**: 1024 (fixed)
- **Model**: `embed-english-v3.0` (configurable via `CohereEmbedderOptions.Model`)
- **Input Type**: defaults to `search_document` (configurable) – suited for document retrieval
- **API**: POST to `v2/embed`, expects JSON response with `embeddings.float`
- **Authentication**: Bearer token using the provided `ApiKey` in request headers
- **Batch support**: `EmbedBatchAsync` accepts a list of texts and returns an array of float arrays
- **Options**: `CohereEmbedderOptions` record requires `ApiKey`, optional `BaseUrl` and `Model`
- **Disposable**: implements `IDisposable` to dispose the internal `HttpClient`
- **Error handling**: Throws on `null` options/HTTP client, ensures HTTP success via `EnsureSuccessStatusCode`

## Cross-References
- [[IEmbedder]] — the interface this class implements
- [[EmbedderType]] — enum used to identify embedder type (Cohere)
- [[AGXIOME.Storage]] — namespace containing storage‐related embedders

## Source Files
- `AGXIOME.Storage/Embedders/CohereEmbedder.cs` (ingested 2026-05-14)