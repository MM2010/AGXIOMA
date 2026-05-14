---
title: OpenAI Embedders
category: entity
tags: [openai, embedders, embedding]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# OpenAI Embedders

## Summary
Provides two concrete implementations of the [[IEmbedder]] interface for OpenAI's text-embedding-3-small (1536 dimensions) and text-embedding-3-large (3072 dimensions) models. Uses a shared internal HTTP client (`OpenAICompatEmbedderClient`) that communicates with the `/v1/embeddings` OpenAI-compatible endpoint with Bearer token authentication. Both embedders support single and batch embedding operations and define their `EmbedderType` and `Dimensions` accordingly.

## Key Details
- `OpenAISmallEmbedder` uses model `text-embedding-3-small` (1536 dimensions, `EmbedderType.OpenAISmall`).
- `OpenAILargeEmbedder` uses model `text-embedding-3-large` (3072 dimensions, `EmbedderType.OpenAILarge`).
- Both require an `OpenAIEmbedderOptions` record with a mandatory `ApiKey`, optional `BaseUrl` (default `https://api.openai.com/`), and `MaxRetries` (default 3).
- The internal `OpenAICompatEmbedderClient` handles HTTP calls, including JSON serialization and deserialization, and re-sorts batch results by index for safety.
- Empty input returns an empty array (`float[]` or `float[][]` as appropriate).
- Both classes implement `IDisposable` and delegate disposal to the internal client.
- Each constructor configures the `HttpClient` base address and adds the `Authorization: Bearer <ApiKey>` header.

## Cross-References
- [[IEmbedder]] — Interface that both embedders implement.
- [[EmbedderType]] — Enum values used to identify the embedder variant.
- [[OpenAIEmbedderOptions]] — Configuration record consumed by both embedders.

## Source Files
- `AGXIOME.Storage/Embedders/OpenAIEmbedders.cs` (ingested 2026-05-14)