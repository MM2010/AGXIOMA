---
title: ProviderCostTracker
category: entity
tags: [cost tracking, token pricing, LLM providers, model costs]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# ProviderCostTracker

## Summary
The `ProviderCostTracker` class provides a centralized mechanism for estimating the monetary cost of LLM inference calls based on provider, model, and token counts. It maintains a static dictionary of known provider–model pairs with per-million-token input and output prices (in USD), supporting both exact model matches and wildcard (`*`) patterns. This enables the agent framework to calculate and track real-time usage costs across different LLM backends.

## Key Details
- **Sealed static class** in `AGXIOME.LLMWIKY` namespace with no instance state.
- Contains a private static `Dictionary<string, (double Input, double Output)>` mapping composite keys (`"{provider}-{model}"`) to per-million-token costs.
- Wildcard keys (`"Ollama-*"`, `"Local-*"`, `"Gemini-*"`) allow cost estimation for any model under a given provider.
- Returns `0` for unknown provider–model combinations.
- `Estimate(string provider, string model, int inputTokens, int outputTokens)` computes cost as:  
  `(inputTokens / 1,000,000) * inputPrice + (outputTokens / 1,000,000) * outputPrice`.
- Supports major providers: OpenAI, Anthropic, DeepSeek, Ollama, Local, Gemini.
- Prices are in USD and stored as doubles but returned as `decimal` for precise monetary calculations.

## Cross-References
- [[TokenTracker]] — records token counts used in cost estimation.

## Source Files
- `AGXIOME.LLMWIKY/ProviderCostTracker.cs` (ingested 2026-05-14)