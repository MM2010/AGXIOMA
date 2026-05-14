---
title: LLMWIKYConfigFactory
category: entity
tags: [config, factory, LLM, provider, environment]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# LLMWIKYConfigFactory

## Summary
Static factory class that auto‑detects and constructs an [[LLMWIKYConfig]] from environment variables with a single line of code. Unlike frameworks like Swarms that require manual provider configuration, AGXIOMA scans a priority list of common LLM providers (DeepSeek, OpenAI, Anthropic, Gemini, etc.) – as well as Azure OpenAI and Ollama – and returns a fully populated configuration object. Supports optional provider hints, bulk retrieval of all detected configs, and a safe `Try` variant.

## Key Details
- **Static class**, no instantiation needed.
- **`FromEnv(string? providerHint = null)`** – auto‑detects the first available provider in a predefined priority order (DeepSeek → OpenAI → Anthropic → Gemini → Groq → Mistral → TogetherAI → Perplexity → OpenRouter). Azure OpenAI is checked first if all three env vars are set; Ollama is detected by `OLLAMA_BASE_URL`.
- **`AllAvailableFromEnv()`** – returns a read‑only list of configs for every provider whose API key is present in environment variables.
- **`TryFromEnv(string? providerHint = null)`** – returns `null` instead of throwing an exception if no provider is configured.
- Recognised environment variables: `DEEPSEEK_API_KEY`, `OPENAI_API_KEY`, `ANTHROPIC_API_KEY`, `GEMINI_API_KEY`, `GROQ_API_KEY`, `MISTRAL_API_KEY`, `TOGETHER_API_KEY`, `PERPLEXITY_API_KEY`, `OPENROUTER_API_KEY`, `AZURE_OPENAI_KEY` + `AZURE_OPENAI_ENDPOINT` + `AZURE_OPENAI_DEPLOYMENT`, `OLLAMA_BASE_URL`.
- For each provider, a default model is set (e.g., `gpt-4o` for OpenAI, `claude‑sonnet‑4‑20250514` for Anthropic) and can be overridden by an environment variable like `OPENAI_MODEL`.
- Endpoint is populated for providers that require a custom base URL (DeepSeek, TogetherAI, Perplexity, OpenRouter); others use the provider’s default.
- Throws `InvalidOperationException` if no matching provider API key is found and no hint matches.

## Cross-References
- [[LLMWIKYConfig]] – the configuration object this factory creates
- [[LLMProviderType]] – the enum of supported providers

## Source Files
- `AGXIOME.Shared/Config/LLMWIKYConfigFactory.cs` (ingested 2026-05-14)