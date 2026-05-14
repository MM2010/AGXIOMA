---
title: Asian Providers
category: entity
tags: [llm, provider, asia, openai-compatible]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Asian Providers

## Summary
A group of Asian LLM providers integrated into the AGXIOME framework via OpenAI-compatible API wrappers. These include Moonshot (Kimi), Zhipu AI (GLM), Baichuan, ByteDance (Doubao), and Alibaba (Qwen). Each provider delegates completion and streaming calls to an `OpenAIProvider` instance configured with the provider’s specific endpoint, base URL, and capabilities. Most support streaming and function calling; some offer vision (Zhipu GLM-4V, Alibaba Qwen-VL) or long-context windows up to 128K tokens. None provide embeddings through the chat endpoint.

## Key Details
- All five providers implement `ILLMProvider` and reuse the `OpenAIProvider` internally.
- Endpoints differ: Moonshot (`api.moonshot.cn/v1`), Zhipu (`open.bigmodel.cn/api/paas/v4`), Baichuan (`api.baichuan-ai.com/v1`), ByteDance (`ark.cn-beijing.volces.com/api/v3`), Alibaba (`dashscope.aliyuncs.com/compatible-mode/v1`).
- Vision support: Zhipu (models `glm-4v`) and Alibaba (models `qwen-vl-plus`).
- Embeddings are explicitly unsupported; each provider throws `NotSupportedException` for embedding methods.
- Context windows range from 32K (Baichuan) to 131K (Alibaba Qwen).
- All support streaming and function calling.
- Models include Moonshot variants (8K/32K/128K), GLM-4 series, Baichuan4, Doubao-Pro, Qwen2.5, and QwQ reasoning.
- Configuration is passed via `LLMWIKYConfig` with the endpoint overridden per provider.

## Cross-References
- [[entities/openai-provider]] — The underlying wrapper used for all Asian providers.
- [[entities/llm-provider]] — The `ILLMProvider` interface that all implement.

## Source Files
- `AGXIOME.LLMWIKY/Providers/AsianProviders.cs` (ingested 2026-05-14)