---
title: GeminiEmbedder
category: entity
tags: [embedder, gemini, google]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# GeminiEmbedder

## Summary
A built-in embedder implementation for the AGXIOMA framework that uses Google Gemini's `text-embedding-004` model to generate 768-dimensional embeddings. It supports both single and batch embedding operations via the Gemini Embed Content API, and is configured through `GeminiEmbedderOptions`.

## Key Details
- Implements [[IEmbedder]] interface with `EmbedderType.GoogleGemini` and `Dimensions = 768`.
- Uses `POST /v1beta/models/{model}:embedContent` for single calls and `/v1beta/models/{model}:batchEmbedContents` for batch calls.
- Default model is `text-embedding-004`; default task type is `RETRIEVAL_DOCUMENT` (optimal for passage embeddings).
- Requires `ApiKey` in `GeminiEmbedderOptions`; API key is passed as a query parameter (`?key=`).
- `EmbedAsync` returns an array of 768 floats; empty input returns a zero-filled array.
- `EmbedBatchAsync` processes up to the batch limit of the Gemini API; returns an array of float arrays.
- Class is `sealed` and implements `IDisposable` to dispose the underlying `HttpClient`.
- JSON response models (`GeminiEmbedResponse`, `GeminiBatchResponse`, `GeminiEmbedding`) are private nested classes with `System.Text.Json` serialization.

## Cross-References
- [[IEmbedder]] — interface contract that this embedder fulfills.

## Source Files
- `AGXIOME.Storage/Embedders/GeminiEmbedder.cs` (ingested 2026-05-14)