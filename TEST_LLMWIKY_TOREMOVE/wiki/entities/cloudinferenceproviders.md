---
title: Cloud Inference Providers
category: entity
tags: [cloud inference providers, LLM, OpenAI-compatible, AGXIOME]
source_count: 6
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Cloud Inference Providers

## Summary
The `CloudInferenceProviders` file defines a collection of first-party LLM provider implementations within the AGXIOME framework. Each provider wraps the internal `OpenAIProvider` with a tailored endpoint and capability set, enabling seamless interchangeability of cloud inference backends such as Groq, Mistral AI, xAI Grok, Cohere, Perplexity, and Together AI. All providers implement the `ILLMProvider` interface and expose streaming, function calling (where supported), and embedding dimensions.

## Key Details
- **GroqProvider** — Uses LPU hardware for high-throughput, low-latency inference. Supports vision and function calling. No embeddings.
- **MistralAIProvider** — Native function calling. Supports embeddings (1024 dimensions). No vision.
- **XAIGrokProvider** — Powers Grok-2 with vision. No embeddings. 131k context.
- **CohereProvider** — Specialises in RAG and tool use (Command R+). Supports embeddings (1024 dimensions). No vision.
- **PerplexityProvider** — Search-augmented Sonar models; no function calling or embeddings. Each call may perform live web retrieval.
- **TogetherAIProvider** (truncated in source) — Over 200 open models, batch inference, and fine-tuned hosting.
- All providers delegate to `OpenAIProvider` after overriding the `Endpoint` in `LLMWIKYConfig`.
- Embedding support varies: Mistral and Cohere support embeddings; Grok and Perplexity throw `NotSupportedException`.
- Each provider exposes a `Capabilities` property of type `LLMProviderCapabilities`.

## Cross-References
- [[LLMProviderCapabilities]] — Defines the capability structure used by all providers.
- [[OpenAIProvider]] — Internal base wrapper reused by every cloud provider.
- [[ILLMProvider]] — Interface all providers implement.
- [[LLMWIKYConfig]] — Configuration object whose `Endpoint` is overridden per provider.

## Source Files
- `AGXIOME.LLMWIKY/Providers/CloudInferenceProviders.cs` (ingested 2026-05-14)