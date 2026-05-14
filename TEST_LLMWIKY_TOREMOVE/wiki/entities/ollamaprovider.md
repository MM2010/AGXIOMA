---
title: OllamaProvider
category: entity
tags: [provider, LLM, Ollama, local]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# OllamaProvider

## Summary
The `OllamaProvider` is a concrete implementation of the [[ILLMProvider]] interface that enables the AGXIOMA framework to communicate with locally running Ollama instances via its REST API (`/api/chat`, `/api/embeddings`). It handles both synchronous and streaming chat completions, tool-call conversations, and embedding generation using models such as Llama 3.1, DeepSeek Coder, Mistral, and others. This provider is ideal for on-premise or air-gapped deployments where data privacy and low latency are priorities.

## Key Details
- Implements `ILLMProvider` with full support for text generation, streaming, tool calling, and embeddings.
- Communicates via HTTP using a configurable base address (default `http://localhost:11434`).
- Provides tool-call parsing both in request history (assistant and tool roles) and in responses.
- Embedding method uses the `nomic-embed-text` model by default; dimension count is configurable via `LLMWIKYConfig.EmbeddingDimensions`.
- Supports streaming via `CompleteStreamAsync`, yielding `LLMWIKYChunk` objects with a final `IsFinal` flag.
- Batch embedding (`EmbedBatchAsync`) runs individual embedding tasks concurrently.
- Offers a predefined set of supported models: `llama3.1`, `llama3.2`, `deepseek-coder`, `mistral`, `mixtral`, `phi4`, `qwen2.5`.
- All async methods accept a `CancellationToken` for graceful cancellation.

## Cross-References
- [[ILLMProvider]] — interface that OllamaProvider implements
- [[LLMWIKYConfig]] — configuration object providing endpoint and embedding dimensions
- [[LLMCompletionRequest]] — request model for chat completions
- [[LLMWIKYResponse]] — response model containing content or tool-call info
- [[LLMWIKYChunk]] — streaming chunk model with content and final flag

## Source Files
- `AGXIOME.LLMWIKY/Providers/OllamaProvider.cs` (ingested 2026-05-14)