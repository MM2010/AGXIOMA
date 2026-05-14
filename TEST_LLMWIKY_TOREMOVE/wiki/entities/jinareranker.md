---
title: JinaReranker
category: entity
tags: [reranker, jina, storage]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# JinaReranker

## Summary
`JinaReranker` is a concrete implementation of [[IReranker]] that communicates with the Jina AI rerank HTTP API (`jina-reranker-v2-base-multilingual`). It submits a query and a list of candidate texts, receives relevance scores, and returns a re-ranked list of `[[RerankResult]]` items. This class handles authentication via bearer token, model selection, result trimming, and proper disposal of the underlying `HttpClient`.

## Key Details
- Implements `IReranker` and `IDisposable`.
- Requires `JinaRerankerOptions` with an `ApiKey` (mandatory) and optional `BaseUrl` (default `https://api.jina.ai`) and `Model` (default `jina-reranker-v2-base-multilingual`).
- Two constructors: one accepts `HttpClient` and options; another creates its own `HttpClient`.
- Sends a POST request to `v1/rerank` with JSON body containing `model`, `query`, `documents` (array of candidate texts), and `top_n`.
- Parses response using `System.Text.Json` with `[JsonPropertyName]` attributes for `results` and `index`/`relevance_score`.
- Converts each raw result to a `RerankResult`, mapping `Index` to `OriginalRank` and computing `NewRank` from the sequence order.
- Defensively trims results to `topK` client-side if the API returns more than requested.
- Property `RerankerType` returns `RerankerType.JinaV2`.

## Cross-References
- [[IReranker]] — interface that `JinaReranker` implements
- [[RerankerType]] — enum value `JinaV2` used as identifier
- [[RerankResult]] — output record produced by `RerankAsync`
- [[RerankCandidate]] — input record passed as candidates

## Source Files
- `AGXIOME.Storage/Rerankers/JinaReranker.cs` (ingested 2026-05-14)