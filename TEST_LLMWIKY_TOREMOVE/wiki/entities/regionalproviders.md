---
title: Regional Providers
category: entity
tags: [regional, providers, LLM, OpenAI-compatible]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Regional Providers

## Summary
The **Regional Providers** are a collection of LLM backend implementations in AGXIOME that wrap the OpenAI API-compatible endpoints of third‑party inference services. Each provider delegates to the internal [[OpenAIProvider]] after overriding the base endpoint and capabilities. They enable the framework to access models from AI21 Jamba, Snowflake Arctic, Phind, You.com, Hyperbolic, Featherless, and similar services without duplicating transport logic.

## Key Details
- All providers implement `ILLMProvider` and reuse `OpenAIProvider` internally with a custom `Endpoint`.
- **AI21JambaProvider** – endpoint `api.ai21.com/studio/v1`, supports streaming & function calling, 256K context, models `jamba-1.5-large` and `jamba-1.5-mini`. No embeddings.
- **SnowflakeArcticProvider** – uses default endpoint, no function calling, 4K context, models include `snowflake-arctic-instruct`, `arctic-embed-m`, `arctic-embed-l`. No embeddings.
- **PhindProvider** – endpoint `api.phind.com/agent/`, no function calling, 16K context, models like `Phind-70B`. No embeddings.
- **YouComProvider** – endpoint `chat-api.you.com/v1`, supports streaming & function calling, 128K context, models include `you-claude-3-5-sonnet`, `you-gpt-4o`. No embeddings.
- **HyperbolicProvider** – endpoint `api.hyperbolic.xyz/v1`, supports streaming & function calling, 131K context, models like `meta-llama/Meta-Llama-3.1-70B-Instruct`. No embeddings.
- **FeatherlessProvider** – (truncated in source) – serverless open‑model endpoint.
- All providers throw `NotSupportedException` for embedding methods, as these endpoints only serve chat completions.
- Configuration is handled via `LLMWIKYConfig` passed into each constructor, which is merged with the provider‑specific endpoint.

## Cross-References
- [[OpenAIProvider]] — base implementation used by all regional providers.
- [[LLM Providers]] — overview of all provider types in the framework.

## Source Files
- `AGXIOME.LLMWIKY/Providers/RegionalProviders.cs` (ingested 2026-05-14)