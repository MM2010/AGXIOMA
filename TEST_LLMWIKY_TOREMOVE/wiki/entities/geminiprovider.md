---
title: GeminiProvider
category: entity
tags: [gemini, llm, provider, streaming, embedding, google]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# GeminiProvider

## Summary
`GeminiProvider` is the Google Gemini implementation of the [[ILLMProvider]] interface in the AGXIOMA framework. It provides non-streaming and streaming text completion, function calling, and single/batch embedding generation by communicating with the Google Gemini API. The provider is configured via [[LLMWIKYConfig]] and uses an injected `HttpClient` to call Gemini endpoints.

## Key Details
- Implements `ILLMProvider` with `Name = "Gemini"` and supports streaming, chat, and function calling (`LLMProviderCapabilities` with all three booleans `true`, context window 1,048,576 tokens, embedding dimension 768).
- Supported models: `gemini-2.0-flash`, `gemini-1.5-pro`, `gemini-2.0-flash-thinking`, `gemini-2.5-pro`.
- Constructor initializes `HttpClient` with base address `https://generativelanguage.googleapis.com/v1beta`.
- `CompleteAsync` sends a POST to `/models/{model}:generateContent` with the API key as a query parameter and returns a parsed `LLMWIKYResponse`.
- `CompleteStreamAsync` uses SSE streaming via the `streamGenerateContent` endpoint, yielding `LLMWIKYChunk` objects until `[DONE]` or cancellation.
- `EmbedAsync` posts to `/models/{model}:embedContent` and returns a `float[]` of the embedding vector.
- `EmbedBatchAsync` runs multiple `EmbedAsync` calls in parallel using `Task.WhenAll`.
- `BuildBody` constructs the JSON request payload including `contents`, `systemInstruction`, and optional `tools` (function declarations).
- `ParseResponse` extracts the text from the first candidate's content parts.
- API key is read from `_config.ApiKey`; model chosen per request (not fixed from config).

## Cross-References
- [[ILLMProvider]] — interface that GeminiProvider implements
- [[LLMWIKYConfig]] — configuration object used for API key and embedding dimensions
- [[LLMCompletionRequest]] — request model used by completion methods
- [[LLMWIKYResponse]] — response model returned by `CompleteAsync`
- [[LLMWIKYChunk]] — streaming chunk model yielded by `CompleteStreamAsync`

## Source Files
- `AGXIOME.LLMWIKY/Providers/GeminiProvider.cs` (ingested 2026-05-14)