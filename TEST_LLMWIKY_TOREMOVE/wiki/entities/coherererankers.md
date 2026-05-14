---
title: CohereRerankers
category: entity
tags: [reranker, cohere, nlp, embedding, http-client]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# CohereRerankers

## Summary
The `CohereRerankEnglish` and `CohereRerankMultilingual` classes are concrete implementations of the [[IReranker]] interface that connect to Cohere's Rerank v2 API. They reuse a shared HTTP client (`CohereRerankerClient`) and configuration record (`CohereRerankerOptions`) to avoid code duplication, and expose the rerank functionality for English (`rerank-english-v3.0`) and multilingual (`rerank-multilingual-v3.0`) models respectively.

## Key Details
- Both classes implement `IReranker` and `IDisposable`, providing a clean `RerankAsync` method.
- `CohereRerankerOptions` holds the API key (required, never hardcoded), base URL (defaults to `https://api.cohere.ai`), and max retries (default 2).
- `CohereRerankerClient` is an internal sealed class that manages the `HttpClient` and Bearer-token authentication.
- The client sends a POST request to `v2/rerank` with model name, query, documents (text from candidates), and top_n.
- Response deserialization maps Cohere's results (index + relevance_score) into `RerankResult` objects with original rank and new rank.
- `CohereRerankEnglish` uses model name `rerank-english-v3.0` and `RerankerType.CohereEnglish`.
- `CohereRerankMultilingual` uses model name `rerank-multilingual-v3.0` and `RerankerType.CohereMultilingual`.
- Both classes offer two constructors: one that accepts only options (creates a new `HttpClient`), and one that also accepts a pre‑existing `HttpClient` for DI/testing.
- The `Dispose()` method on each reranker disposes the underlying `CohereRerankerClient`.

## Cross-References
- [[IReranker]] — interface contract implemented by both classes.
- [[RerankerType]] — enum values `CohereEnglish` and `CohereMultilingual`.
- [[RerankResult]], [[RerankCandidate]] — DTOs used in the process.

## Source Files
- `AGXIOME.Storage/Rerankers/CohereRerankers.cs` (ingested 2026-05-14)