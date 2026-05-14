---
title: ILLMProvider
category: entity
tags: [interface, llm, provider, factory]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# ILLMProvider

## Summary
The `ILLMProvider` interface and its companion factory interface (`ILLMProviderFactory`) define the core abstraction for integrating any large language model (LLM) into the AGXIOME framework. `ILLMProvider` exposes methods for single and streaming completions, embedding generation, and capabilities metadata, while the factory enables dynamic registration and creation of providers from configuration. This decoupling allows the framework to support multiple LLM backends interchangeably.

## Key Details
- `ILLMProvider` exposes `Name`, `Capabilities` (`LLMProviderCapabilities`), and `EmbeddingDimensions`.
- Completion methods: `CompleteAsync` (returns `LLMWIKYResponse`) and `CompleteStreamAsync` (returns `IAsyncEnumerable<LLMWIKYChunk>`).
- Embedding methods: `EmbedAsync` (single string → `float[]`) and `EmbedBatchAsync` (batch → `float[][]`).
- `ILLMProviderFactory` manages provider creation via `CreateProvider(LLMWIKYConfig)` and registration via `RegisterProvider(name, factoryFunc)`.
- `ListAvailableProviders` returns a read-only list of `ProviderDescriptor`.
- Both interfaces are defined in namespace `AGXIOME.Shared.Interfaces`.
- The factory pattern supports runtime extensibility for different LLM implementations.
- All async methods accept an optional `CancellationToken` parameter.

## Cross-References
- [[LLMWIKYConfig]] — configuration passed to `CreateProvider` to instantiate a provider.
- [[LLMCompletionRequest]] — input model for completion requests.
- [[LLMWIKYResponse]] and [[LLMWIKYChunk]] — output models for completions and streaming.
- [[LLMProviderCapabilities]] — metadata about a provider’s supported features.
- [[ProviderDescriptor]] — descriptor returned when listing available providers.

## Source Files
- `AGXIOME.Shared/Interfaces/ILLMProvider.cs` (ingested 2026-05-14)