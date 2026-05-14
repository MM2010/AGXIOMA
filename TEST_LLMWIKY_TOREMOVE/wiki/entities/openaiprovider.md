---
title: OpenAIProvider
category: entity
tags: [provider, llm, openai, deepseek, resilience, streaming]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# OpenAIProvider

## Summary
`OpenAIProvider` is the primary LLM provider implementation within AGXIOMA, supporting OpenAI, DeepSeek, and any OpenAI-compatible endpoint. It integrates robust resilience (retry, exponential backoff, circuit breaker, fallback) via `ResilientExecutor` and `CircuitBreaker`. The provider handles standard chat completions, streaming, embeddings, and has special handling for DeepSeek's legacy XML-based function call format.

## Key Details
- **Capabilities**: Supports chat, streaming, function/tool calls; embedding dimension 1536; context up to 128k tokens; models include `gpt-4o`, `gpt-4o-mini`, `deepseek-chat`, and others.
- **Resilience**: Uses `ResilientExecutor` with `RetryPolicy` (max retries from config, 500ms base delay) and `CircuitBreaker` (failure threshold 5). Retries on HTTP 429/500/502/503.
- **Non‑success handling**: API error body is read and included in `HttpRequestException` for diagnostics.
- **Streaming**: Sends `stream: true` in the request body, parses SSE `data:` lines, yields `LLMWIKYChunk` and terminates with `[DONE]`.
- **DeepSeek legacy function calls**: Detects `<functioncall>` XML in content and parses `<invoke name>` and `<parameter>` tags, converting them into standard tool call properties.
- **Embeddings**: `EmbedAsync` sends a request to `/embeddings` and returns a `float[]`.
- **Configuration**: Reads `LLMWIKYConfig` (endpoint, API key, embedding dimensions, max retries). Default endpoint is `https://api.openai.com/v1`.
- **JSON serialization**: Uses `SnakeCaseLower` naming policy; requests built as `JsonObject` to avoid policy interference.

## Cross-References
- [[ILLMProvider]] — interface implemented by this provider
- [[LLMWIKYConfig]] — configuration class consumed by the provider
- [[ResilientExecutor]] — used for resilient execution with retries and circuit breaker
- [[LLMWIKYResponse]] — response model for non‑streaming completions
- [[LLMWIKYChunk]] — chunk model for streaming completions
- [[LLMWIKYUsage]] — token usage model

## Source Files
- `AGXIOME.LLMWIKY/Providers/OpenAIProvider.cs` (ingested 2026-05-14)