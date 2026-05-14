---
title: F14 TS06 Embedder Tests
category: entity
tags: [embedder-tests, f14, ts06, extended-coverage]
source_count: 103
created_at: 2026-05-14
updated_at: 2026-05-14
---

# F14 TS06 Embedder Tests

## Summary
This test suite (TS-06) provides extended coverage for all embedder implementations in the AGXIOME framework, totaling 103 tests. It goes beyond the core tests in F3EmbedderTests by validating edge cases such as empty input safety, error propagation, batch behavior, null argument guards, dimension contract conformance, and disposal safety. The suite covers embedders for Anthropic, Cohere, Google Gemini, HuggingFace, Jina, Meta Llama, OpenAI, Amazon Titan, Ollama, and VoyageAI, ensuring robust and consistent behavior across providers.

## Key Details
- **103 tests** covering all major embedder types in the framework.
- Validates **empty-input safety**: returns zero-vector of correct dimensions for empty strings.
- Verifies **batch behavior**: empty batch returns empty result; non-empty batch returns correct embeddings.
- Tests **null guards**: constructor throws `ArgumentNullException` when `HttpClient` or options are null.
- Confirms **dimension contract**: `Dimensions` property matches returned vector length.
- Ensures **dispose safety**: no tests explicitly shown in snippet, but part of extended coverage.
- Each embedder's `EmbedderType` is asserted correctly.
- Includes custom JSON response helpers (`F14EmbedHelper`) to simulate provider-specific response formats (e.g., Gemini's nested `embedding.values`, Titan's direct `embedding` array).
- Uses a file-scoped `F14HttpHandler` to mock HTTP responses with desired status code and JSON body.
- Extends coverage from [[F3-Embedder-Tests]] with additional edge-case scenarios.

## Cross-References
- [[F3-Embedder-Tests]] — Base test suite that TS-06 extends.
- [[Embedder Abstractions]] — Core interfaces and base classes tested here.
- [[Embedder Type]] — Enum values validated by these tests.
- [[Embedder Options]] — Options classes validated for null guards.

## Source Files
- `AGXIOME.Tests/F14/F14_TS06_EmbedderTests.cs` (ingested 2026-05-14)