---
title: F3 Embedder Tests
category: entity
tags: [embedder, tests, unit-test]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# F3 Embedder Tests

## Summary
This file contains unit tests for the embedder implementations in the AGXIOME framework. It covers five embedder types: OpenAI Small, OpenAI Large, Anthropic, Cohere, and Mistral. Each embedder test class validates correct type identification, dimensions, vector parsing from HTTP responses, batch processing, and null‑option guard clauses. A shared HTTP helper provides canned JSON responses in OpenAI or provider‑specific formats.

## Key Details
- Uses `TestHttpMessageHandler` (static helper class `EmbedHttpHelper`) to return controlled HTTP responses.
- Five test classes: `OpenAISmallEmbedderTests`, `OpenAILargeEmbedderTests`, `AnthropicEmbedderTests`, `CohereEmbedderTests`, `MistralEmbedderTests`.
- Each embedder test class contains exactly five test methods (some truncated in the source excerpt).
- Tests verify `EmbedderType`, `Dimensions`, single `EmbedAsync`, batch `EmbedBatchAsync`, and `ArgumentNullException` on null options.
- Helper methods `F` and `FVec` use invariant culture to guarantee dot‑separated floats.
- Batch tests confirm order preservation and correct handling of empty input.

## Cross-References
- [[OpenAISmallEmbedder]] — the embedder tested in `OpenAISmallEmbedderTests`
- [[OpenAILargeEmbedder]] — the embedder tested in `OpenAILargeEmbedderTests`
- [[AnthropicEmbedder]] — the embedder tested in `AnthropicEmbedderTests`
- [[CohereEmbedder]] — the embedder tested in `CohereEmbedderTests`
- [[MistralEmbedder]] — the embedder tested in `MistralEmbedderTests`
- [[EmbedderType]] — the enum verified in each test

## Source Files
- `AGXIOME.Tests/F3EmbedderTests.cs` (ingested 2026-05-14)