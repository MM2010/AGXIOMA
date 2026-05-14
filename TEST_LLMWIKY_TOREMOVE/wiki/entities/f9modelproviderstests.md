---
title: F9ModelProvidersTests
category: entity
tags: [tests, structural-tests, llm-providers, model-providers]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# F9ModelProvidersTests

## Summary
The `F9ModelProvidersTests` class is a structural test suite covering all LLM providers in the AGXIOME framework (tests M-01 through M-50). It verifies that each provider returns the correct name, exposes non-null capabilities, lists at least one supported model, can be created via `LLMProviderFactory`, and that the embedding dimensions property matches `Capabilities.EmbeddingDimensions`. All tests are purely structural—no network calls—ensuring providers meet their contract without external dependencies.

## Key Details
- Contains 50 tests: 5 per provider across 10 providers (Groq, MistralAI, xAIGrok, Cohere, Perplexity, TogetherAI, FireworksAI, Cerebras, and others from the full file).
- Each provider test class follows an identical pattern: instantiate the provider with a test configuration, then assert name, capabilities, supported models, factory creation, and embedding dimensions consistency.
- Uses a `ProviderTestHelpers.Cfg()` helper to create a minimal `LLMWIKYConfig` with a test API key and model.
- Tests are designed to run in parallel (Xunit) and require no live API connectivity.
- Ensures that provider implementations never return null capabilities, empty model lists, or inconsistent embedding dimensions.

## Cross-References
- [[entities/LLMProviderFactory]] — factory used to instantiate each provider in the tests
- [[entities/LLMProviderType]] — enum values tested (e.g., Groq, MistralAI, etc.)
- [[entities/Capabilities]] — object verified for non-nullness and supported models

## Source Files
- `AGXIOME.Tests/F9ModelProvidersTests.cs` (ingested 2026-05-14)