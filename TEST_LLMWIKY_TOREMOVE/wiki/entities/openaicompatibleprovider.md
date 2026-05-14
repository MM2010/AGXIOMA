---
title: OpenAICompatibleProvider
category: entity
tags: [provider, OpenAI, adapter, resilience, streaming]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# OpenAICompatibleProvider

## Summary
The `OpenAICompatibleProvider` is a universal adapter for any OpenAI-compatible API endpoint, supporting a wide range of third-party providers (Together AI, Fireworks AI, Perplexity, DeepInfra, LM Studio, vLLM, etc.). It implements the `ILLMProvider` interface with full type safety, integrated circuit breaker and retry resilience, intelligent routing via `ProviderMetricsCollector`, and supports both streaming and non‑streaming completions as well as embeddings.

## Key Details
- Implements `ILLMProvider` with `Name`, `Capabilities`, `EmbeddingDimensions`.
- Configurable via `LLMWIKYConfig` (API key, endpoint, model, timeout, max retries).
- Built‑in resilience: `ResilientExecutor` (retry policy) and `CircuitBreaker` (threshold of 5 failures).
- **`CompleteAsync`**: sends a non‑streaming chat completion request, parses `tool_calls` and usage.
- **`CompleteStreamAsync`**: reads SSE stream (`data: ... [DONE]`), yields `LLMWIKYChunk` items.
- **`EmbedAsync`**: posts to `/embeddings`, returns `float[]` array.
- Supports function/tool calling (via `tool_calls` in response).
- Automatically derives display name from endpoint URL if not provided.
- Handles rate limiting (HTTP 429) as a retriable exception.

## Cross-References
- [[ILLMProvider]] — the interface this provider implements.
- [[LLMWIKYConfig]] — configuration object used to instantiate the provider.
- [[LLMWIKYResponse]] — response model for non‑streaming completions.
- [[LLMWIKYChunk]] — chunk model for streaming completions.
- [[ResilientExecutor]] — retry and circuit breaker execution engine.
- [[CircuitBreaker]] — failure threshold protection.
- [[ProviderMetricsCollector]] — collects latency, cost, and error metrics.

## Source Files
- `AGXIOME.LLMWIKY/Providers/OpenAICompatibleProvider.cs` (ingested 2026-05-14)