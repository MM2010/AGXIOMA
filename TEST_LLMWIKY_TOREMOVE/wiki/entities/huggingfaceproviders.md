---
title: HuggingFace Providers
category: entity
tags: [HuggingFace, LLMProvider, Inference API, TGI, OpenAI-compatible]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# HuggingFace Providers

## Summary
The `HuggingFaceProviders` source defines two LLM providers that wrap the [[OpenAIProvider]] to offer OpenAI-compatible endpoints for HuggingFace models. The **HuggingFaceInferenceProvider** connects to HuggingFace’s hosted Inference API (api-inference.huggingface.co/v1), giving access to over 200k community models. The **HuggingFaceTGIProvider** targets self‑hosted Text Generation Inference (TGI) containers (e.g., localhost:8080/v1), adding support for function calling. Both providers delegate text generation to the underlying OpenAI provider and raise `NotSupportedException` for embedding operations.

## Key Details
- **HuggingFaceInferenceProvider** (`Name = "HuggingFaceInference"`)
  - Uses HuggingFace’s public Inference API, configured via `LLMWIKYConfig.Model` for model repo ID.
  - Capabilities: streaming = true, function calling = false, vision = false, embedding dimensions = 0.
  - Default supported models: `meta-llama/Meta-Llama-3.1-70B-Instruct`, `mistralai/Mistral-7B-Instruct-v0.3`, `Qwen/Qwen2.5-72B-Instruct`.
  - Max context tokens: 131,072.
- **HuggingFaceTGIProvider** (`Name = "HuggingFaceTGI"`)
  - Designed for self‑hosted TGI endpoints (requires TGI v1.4+). Endpoint defaults to `http://localhost:8080/v1`.
  - Capabilities: streaming = true, function calling = true, vision = false, embedding dimensions = 0.
  - Default supported models: `meta-llama/Meta-Llama-3.1-70B-Instruct`, `mistralai/Mistral-7B-v0.3`.
  - Max context tokens: 131,072.
- Both providers inherit the `CompleteAsync` and `CompleteStreamAsync` implementations from [[OpenAIProvider]] by delegating to an internally instantiated `OpenAIProvider` with the appropriate endpoint.
- Embedding methods (`EmbedAsync`, `EmbedBatchAsync`) throw `NotSupportedException`, directing users to dedicated HuggingFace embedding services (e.g., Inference API for feature extraction, TEI containers for TGI).

## Cross-References
- [[OpenAIProvider]] — used as the internal implementation layer for both providers.
- [[ILLMProvider]] — interface implemented by both providers.
- [[LLMWIKYConfig]] — configuration object consumed in constructor for endpoint and model settings.
- [[LLMProviderCapabilities]] — data structure describing provider capabilities.

## Source Files
- `AGXIOME.LLMWIKY/Providers/HuggingFaceProviders.cs` (ingested 2026-05-14)