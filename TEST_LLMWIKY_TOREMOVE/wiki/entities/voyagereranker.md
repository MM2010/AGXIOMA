---
title: VoyageReranker
category: entity
tags: [reranker, voyage, IReranker, api]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# VoyageReranker

## Summary
The `VoyageReranker` provides an implementation of the [[IReranker]] interface that calls the Voyage AI `rerank-2` API. It reorders a list of candidate documents by relevance to a given query, returning scored results with original and new ranks. The class handles API authentication, request construction, and response deserialization using `System.Text.Json`.

## Key Details
- Implements `IReranker` and `IDisposable`.
- Uses `VoyageRerankerOptions` for configuration: `ApiKey` (required), `BaseUrl` (default `https://api.voyageai.com`), `Model` (default `rerank-2`).
- Constructor accepts either just options (creates its own `HttpClient`) or an existing `HttpClient` plus options.
- `RerankerType` property returns `RerankerType.VoyageRerank2`.
- `RerankAsync` method posts JSON to `/v1/rerank` with fields: `model`, `query`, `documents` (array of candidate texts), `top_k`.
- Throws on non-success HTTP responses via `EnsureSuccessStatusCode`.
- Returns empty list if API response contains no data.
- Each `RerankResult` includes candidate `Id`, `Text`, `Score`, `OriginalRank` (from API), `NewRank` (sequential index), and `Metadata`.
- Internal types `VoyageRerankResponse` and `VoyageResult` map API fields using `JsonPropertyName`.

## Cross-References
- [[IReranker]] — interface implemented by this class.
- [[RerankerType]] — enumeration identifying implementations.
- [[RerankResult]] — data transfer object for rerank output.
- [[RerankCandidate]] — input document type for reranking.

## Source Files
- `AGXIOME.Storage/Rerankers/VoyageReranker.cs` (ingested 2026-05-14)