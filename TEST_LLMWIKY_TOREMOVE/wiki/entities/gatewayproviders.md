---
title: GatewayProviders
category: entity
tags: [gateway, provider, LLM, OpenRouter, LiteLLM, AI/ML API, Novita AI, DeepInfra]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# GatewayProviders

## Summary
Gateway providers are LLM provider implementations that aggregate access to multiple underlying model endpoints through a unified interface. Each gateway—OpenRouter, LiteLLM, AI/ML API, Novita AI, and DeepInfra—delegates to the [[OpenAIProvider]] with a custom endpoint and exposes only the capabilities supported by the upstream service. They allow the framework to route requests to a wide variety of models (200+) while maintaining a consistent API.

## Key Details
- All gateway providers implement `ILLMProvider` and wrap an internal `OpenAIProvider` instance.
- Each sets a specific base endpoint: OpenRouter (`https://openrouter.ai/api/v1`), LiteLLM (configurable), AI/ML API (`https://api.aimlapi.com/v1`), Novita AI (`https://api.novita.ai/v3/openai`), DeepInfra (`https://api.deepinfra.com/v1/openai`).
- Capabilities vary: OpenRouter and LiteLLM support streaming, function calling, vision, and high context (200K). AI/ML API and Novita AI lack vision and embeddings. DeepInfra supports embeddings (1024 dimensions) but not vision.
- LiteLLM and DeepInfra forward embedding requests to the inner provider; all others throw `NotSupportedException` for embeddings.
- Supported model lists are illustrative; actual availability depends on the gateway’s model catalog.
- OpenRouter requires HTTP headers (`HTTP-Referer`, `X-Title`) for policy compliance.

## Cross-References
- [[OpenAIProvider]] — Underlying delegating provider used by all gateways.
- [[LLMProviderCapabilities]] — Structure that defines each gateway’s supported features.
- [[ILLMProvider]] — Interface contract implemented by all gateways.

## Source Files
- `AGXIOME.LLMWIKY/Providers/GatewayProviders.cs` (ingested 2026-05-14)