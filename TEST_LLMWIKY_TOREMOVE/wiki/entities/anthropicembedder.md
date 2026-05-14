---
title: AnthropicEmbedder
category: entity
tags: [embedder, anthropic, embeddings, adapter]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AnthropicEmbedder

## Summary
A ready-to-activate adapter for generating text embeddings via Anthropic’s (as-yet-unreleased) embedding endpoint. Implements the `IEmbedder` interface using `HttpClient` and Bearer token authentication, with a placeholder model name and configurable dimensions. The adapter is structured to mirror the OpenAI embedding pattern and will become functional once Anthropic ships `/v1/embeddings`.

## Key Details
- **Sealed class** implementing `IEmbedder` and `IDisposable`.
- Uses `HttpClient` with base URL defaulting to `https://api.anthropic.com/`.
- Sends `x-api-key` and `anthropic-version: 2023-06-01` headers.
- Default model: `claude-embed-v1` (placeholder).
- Default embedding dimensions: **1536**.
- Single-embedding method: `EmbedAsync(string text, CancellationToken)` – returns zero vector for empty input.
- Batch method: `EmbedBatchAsync(IReadOnlyList<string>, CancellationToken)` – runs parallel `EmbedAsync` calls.
- Deserializes JSON response with `data[0].embedding` array.
- **Not yet functional** – Anthropic has not released a public embeddings endpoint.
- Options configured via `AnthropicEmbedderOptions` record with required `ApiKey`.

## Cross-References
- [[IEmbedder]] — interface implemented by this adapter
- [[OpenAiEmbedder]] — parallel adapter for OpenAI embeddings
- [[EmbedderType]] — enum used to identify embedder type
- [[AGXIOME.Storage]] — namespace containing this and other storage components

## Source Files
- `AGXIOME.Storage/Embedders/AnthropicEmbedder.cs` (ingested 2026-05-14)