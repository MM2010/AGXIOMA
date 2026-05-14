---
title: ILLMWIKYEngine
category: entity
tags: [interface, inference, streaming, embeddings, tool-registration]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# ILLMWIKYEngine

## Summary
The `ILLMWIKYEngine` interface defines the core contract for interacting with a large language model in the AGXIOMA framework. It provides methods for single and streaming inference, embedding generation (single and batch), and dynamic registration of tool schemas, enabling agents to seamlessly integrate LLM capabilities.

## Key Details
- Located in `AGXIOME.Shared.Interfaces` namespace.
- All inference methods accept a cancellation token (`CancellationToken ct = default`).
- `InferAsync` returns a single `LLMWIKYResponse` asynchronously.
- `InferStreamAsync` returns an `IAsyncEnumerable<LLMWIKYChunk>` for real-time token streaming.
- `EmbedAsync` generates a single float array embedding for a given text.
- `EmbedBatchAsync` generates a 2D float array embedding for a list of texts.
- `RegisterTools` accepts a read-only list of JSON objects representing tool schemas (e.g., for function calling).
- Designed for extensibility; implementers can plug into any LLM backend.

## Cross-References
- [[llmwikychunk]] — streamed response units returned by `InferStreamAsync`
- [[llmwikyrresponse]] – complete response type for `InferAsync`
- [[llmwikytrequest]] – input request type used by inference methods

## Source Files
- `AGXIOME.Shared/Interfaces/ILLMWIKYEngine.cs` (ingested 2026-05-14)