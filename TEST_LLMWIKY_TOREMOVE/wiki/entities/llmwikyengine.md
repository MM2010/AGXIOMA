---
title: LLMWIKYEngine
category: entity
tags: [LLMWIKY, engine, inference, provider, fallback]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# LLMWIKYEngine

## Summary
The `LLMWIKYEngine` is the core inference orchestrator within the AGXIOME LLMWIKY subsystem. It routes chat/text completion and embedding requests to a configured primary LLM provider, automatically fails over to a fallback provider on transient network errors (`TimeoutException` or `HttpRequestException`), supports streaming responses, batch embedding, and global tool registration.

## Key Details
- Sealed class implementing `ILLMWIKYEngine`.
- Requires a `LLMWIKYConfig` at construction; the primary provider is created via `LLMProviderFactory.Create`. An optional fallback provider is instantiated from the first entry in `config.FallbackProviders`, if any.
- `InferAsync`: Performs a non‑streaming completion. Builds a `LLMCompletionRequest` from the request and config, then delegates to the primary provider. On failure with a transient exception and when a fallback exists, retries on the fallback provider.
- `InferStreamAsync`: Returns an `IAsyncEnumerable<LLMWIKYChunk>` for streaming completions. Always uses the primary provider (no fallback for streaming).
- `EmbedAsync`: Embeds a single text string via the primary provider.
- `EmbedBatchAsync`: Embeds a list of texts via the primary provider.
- `RegisterTools`: Stores a list of tool schemas (as `JsonObject` arrays) that will be injected into all subsequent completion requests unless overridden by the request’s own tools.
- The configured `Model` from `LLMWIKYConfig` is used as the default model; temperature and max tokens fall back to config values if not provided in the request.

## Cross-References
- [[ILLMWIKYEngine]] — interface implemented by this engine.
- [[LLMWIKYConfig]] — configuration object consumed at construction.
- [[LLMProviderFactory]] — factory used to create the primary and fallback providers.
- [[LLMCompletionRequest]] — internal request object built from the public API parameters.

## Source Files
- `AGXIOME.LLMWIKY/LLMWIKYEngine.cs` (ingested 2026-05-14)