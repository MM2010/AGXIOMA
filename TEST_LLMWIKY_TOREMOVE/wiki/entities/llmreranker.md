---
title: LlmReranker
category: entity
tags: [llmreranker, reranker, llm, relevance scoring]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# LlmReranker

## Summary
`LlmReranker` is an [[IReranker]] implementation that uses an [[ILLMProvider]] to score each candidate document's relevance to a query on a 0–1 scale. It sends one LLM call per candidate (configurable batch parallelism) with a strict prompt requesting only a numeric score. Content is escaped to reduce prompt‑injection risk, and parsing degrades gracefully — unscores return 0.0.

## Key Details
- Implements `IReranker` with `RerankerType` = `LLMReranker`.
- Constructor requires an `ILLMProvider` instance.
- `BatchSize` (default `4`) controls how many candidates are scored concurrently via a `SemaphoreSlim`.
- Each candidate is scored with a prompt that sandboxes document text between `<<<BEGIN DOCUMENT>>>` / `<<<END DOCUMENT>>>` markers.
- `EscapeContent()` replaces `<<<` and `>>>` sequences in user content to prevent delimiter injection.
- The LLM is instructed to output only a single float; `ParseScore()` extracts the first numeric token and clamps it to `[0, 1]`.
- If the LLM call throws, the candidate receives a score of `0.0` (graceful degradation).
- Output is sorted descending by score; `topK` limits the final list (default all).
- Each result is a `[[RerankResult]]` containing id, text, score, original rank, new rank, and metadata.
- **Cost note:** one LLM call per candidate — prefer cheaper cross‑encoder rerankers for large lists.

## Cross-References
- [[IReranker]] — interface contract that `LlmReranker` fulfills.
- [[ILLMProvider]] — the language model service used for scoring.
- [[RerankResult]] — result type returned after reranking.
- [[RerankerType]] — enum value `LLMReranker`.

## Source Files
- `AGXIOME.Storage/Rerankers/LlmReranker.cs` (ingested 2026-05-14)