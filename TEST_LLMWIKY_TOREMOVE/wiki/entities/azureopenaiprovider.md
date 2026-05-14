---
title: AzureOpenAIProvider
category: entity
tags: [provider, azure, openai, llm]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AzureOpenAIProvider

## Summary
The `AzureOpenAIProvider` is a sealed implementation of the `ILLMProvider` interface that provides access to Azure OpenAI services. It wraps the standard `OpenAIProvider` by dynamically constructing the Azure‑specific REST endpoint (using the configured resource, deployment, and API version), thereby reusing all OpenAI‑provider logic for completions, streaming, and embeddings.

## Key Details
- Class: `sealed class AzureOpenAIProvider : ILLMProvider`
- Property `Name` returns `"AzureOpenAI"`
- `Capabilities` declares support for chat, streaming, and embeddings with 1536 embedding dimensions, max context of 128,000 tokens, and models `gpt-4o`, `gpt-4o-mini`, `gpt-4`
- `EmbeddingDimensions` delegates to `_config.EmbeddingDimensions`
- Constructor takes `LLMWIKYConfig` and builds the Azure endpoint as:  
  `{config.Endpoint}/openai/deployments/{config.Model}/chat/completions?api-version={config.ApiVersion ?? "2024-08-01-preview"}`
- Internally instantiates `OpenAIProvider` with a modified config that uses the Azure endpoint
- Delegates all public methods (`CompleteAsync`, `CompleteStreamAsync`, `EmbedAsync`, `EmbedBatchAsync`) directly to the inner `OpenAIProvider`

## Cross-References
- [[ILLMProvider]] — interface that `AzureOpenAIProvider` implements
- [[OpenAIProvider]] — internal provider reused by this Azure wrapper
- [[LLMWIKYConfig]] — configuration object used to construct the Azure endpoint and embedding dimensions
- [[LLMCompletionRequest]] — request model for completions

## Source Files
- `AGXIOME.LLMWIKY/Providers/AzureOpenAIProvider.cs` (ingested 2026-05-14)