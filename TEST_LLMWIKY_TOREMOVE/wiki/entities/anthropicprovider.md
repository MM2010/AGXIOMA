---
title: AnthropicProvider
category: entity
tags: [provider, anthropic, llm, claude]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AnthropicProvider

## Summary
An implementation of `ILLMProvider` that connects to the Anthropic Claude API, supporting the full Messages API with tool calling (`tool_use`) and server-sent event (SSE) streaming. It handles both synchronous and streaming completions, builds request bodies conforming to Anthropic's schema, and parses responses including usage statistics. Embedding operations are not supported natively and throw `NotSupportedException`.

## Key Details
- Implements `ILLMProvider` with streaming (`CompleteStreamAsync`) and non-streaming (`CompleteAsync`) completion methods.
- Supports multiple Claude models: `claude-sonnet-4-20250514`, `claude-3-5-sonnet-20241022`, `claude-opus-4-20250514`, `claude-3-5-haiku-20241022`.
- Uses the Anthropic Messages API endpoint (`/v1/messages`) with API version `2023-06-01`.
- Handles tool calls in both streaming (via `content_block_start` and `input_json_delta` events) and non-streaming responses.
- Does **not** support embeddings; throws `NotSupportedException` for `EmbedAsync` and `EmbedBatchAsync`.
- Constructor requires an `LLMWIKYConfig` with a non-null `ApiKey`.
- Internal `BuildBody` constructs the JSON payload including system prompt, messages, streaming flag, and tools.
- Internal `ParseResponse` extracts text content, tool call ID/name/arguments, and usage tokens.
- Implements `IDisposable` to release the underlying `HttpClient`.

## Cross-References
- [[ILLMProvider]] — the contract this provider fulfills
- [[LLMWIKYConfig]] — configuration object required by the constructor
- [[LLMCompletionRequest]] — request model used for completions
- [[LLMWIKYChunk]] — streaming chunk model
- [[LLMWIKYResponse]] — non-streaming response model
- [[LLMWIKYUsage]] — token usage model
- [[LLMProviderCapabilities]] — capabilities descriptor returned by `Capabilities`

## Source Files
- `AGXIOME.LLMWIKY/Providers/AnthropicProvider.cs` (ingested 2026-05-14)