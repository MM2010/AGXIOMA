---
title: Bedrock and Azure Embedders
category: entity
tags: [embedders, aws, bedrock, azure, openai, titan]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Bedrock and Azure Embedders

## Summary
Two concrete implementations of the `IEmbedder` interface: `BedrockTitanEmbedder` (AWS Bedrock Titan Embeddings, 1024 dimensions) and `AzureOpenAIEmbedder` (Azure OpenAI text-embedding-3-large, 3072 dimensions). Both handle authentication (SigV4 for AWS, API key for Azure), offer single and batch embedding methods, and follow the same disposal pattern.

## Key Details
- Both classes implement `IEmbedder` and `IDisposable`.
- `BedrockTitanEmbedder`: 1024 dimensions, uses `titan-embed-text-v2:0` model. `EmbedAsync` posts to `/model/{modelId}/invoke`. Batch is implemented sequentially (no native batch support).
- `AzureOpenAIEmbedder`: 3072 dimensions, uses configurable deployment (default `text-embedding-3-large`). Batch is native via Azure API; single call uses batch internally.
- Options records (`BedrockEmbedderOptions`, `AzureOpenAIEmbedderOptions`) require endpoint/URL and authentication parameters.
- `AzureOpenAIEmbedder` sets `api-key` header; `BedrockTitanEmbedder` expects the injected `HttpClient` to carry AWS SigV4 signing.
- Both return empty arrays (single: zero-filled, batch: empty) for empty/null input.
- Source file: `AGXIOME.Storage/Embedders/BedrockAndAzureEmbedders.cs`.

## Cross-References
- [[IEmbedder]] — shared interface implemented by both classes.
- [[EmbedderType]] — enum values `AWSBedrockTitan` and `AzureOpenAI`.
- [[EmbeddersOverview]] — aggregate page for all embedder implementations.

## Source Files
- `AGXIOME.Storage/Embedders/BedrockAndAzureEmbedders.cs` (ingested 2026-05-14)