---
title: LocalProvider
category: entity
tags: [provider, local, openai-compatible, vllm, llama.cpp, tgi]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# LocalProvider

## Summary
The `LocalProvider` adapts the OpenAI-compatible API to work with local model serving engines such as vLLM, TGI, and llama.cpp. It wraps [[OpenAIProvider]] with a default endpoint of `http://localhost:8080/v1` and a placeholder API key, delegating all completion, streaming, and embedding operations to the underlying provider. This enables the AGXIOME framework to leverage self-hosted models without changing the external interface.

## Key Details
- Implements `ILLMProvider` and is marked `sealed`.
- Uses composition: internally holds an `OpenAIProvider` instance with a modified `LLMWIKYConfig`.
- Default endpoint: `http://localhost:8080/v1` (configurable via `LLMWIKYConfig.Endpoint`).
- API key is set to `"not-needed"` as local servers typically do not require authentication.
- Capabilities: supports completions and streaming; no function calling; embedding dimensions from configuration; context length of 8,192 tokens.
- Exposed model list: `["llama-3.1-8b", "mistral-7b", "mixtral-8x22b"]`.
- All public methods (`CompleteAsync`, `CompleteStreamAsync`, `EmbedAsync`, `EmbedBatchAsync`) are forwarded to the inner `OpenAIProvider`.

## Cross-References
- [[OpenAIProvider]] — The actual provider that handles the HTTP requests to the local endpoint.
- [[ILLMProvider]] — The interface this class implements.
- [[LLMWIKYConfig]] — Configuration object used to set endpoint, API key, and embedding dimensions.
- [[LLMProviderCapabilities]] — Data class describing provider capabilities.
- [[LLMWIKYResponse]] — Response model for completions.
- [[LLMCompletionRequest]] — Request model for completions.
- [[LLMWIKYChunk]] — Streaming chunk model.

## Source Files
- `AGXIOME.LLMWIKY/Providers/LocalProvider.cs` (ingested 2026-05-14)