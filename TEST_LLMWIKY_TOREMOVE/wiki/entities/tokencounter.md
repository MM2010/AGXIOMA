---
title: TokenCounter
category: entity
tags: [token counting, LLM, provider, approximate]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# TokenCounter

## Summary
TokenCounter provides per-provider token counting for LLM requests using a character‑based approximation (BPE‑approximate) keyed by `TokenizerAlgorithm`. It eliminates magic strings by mapping `LLMProviderType` to a canonical algorithm, returns a `TokenCountResult` with metadata, and is designed as a lightweight fallback until an exact BPE implementation (e.g. ML.Tokenizers for Cl100k/O200k) is integrated.

## Key Details
- Enum‑driven provider‑to‑algorithm mapping via static `_providerMap` (e.g. OpenAI → O200kBase, Anthropic → Cl100kBase).
- Character‑per‑token constants derived from public benchmarks (e.g. O200kBase: 3.9, Llama3: 3.8).
- `Count(string, LLMProviderType)` – core method, returns approximate input token count.
- `CountWithAlgorithm(string, TokenizerAlgorithm)` – explicit algorithm override for custom or local providers.
- `CountWithCompletion(string, LLMProviderType, double ratio=0.5)` – estimates output tokens by multiplying input count by completion ratio.
- `AlgorithmFor(LLMProviderType)` – inspect which algorithm is used for a given provider.
- `TokenCountResult` record includes `InputTokens`, `EstimatedOutputTokens`, `Algorithm`, and `IsApproximate` (always `true` in this tier).
- Handles empty/null input gracefully (zero tokens, `Generic` algorithm).
- All methods are instance‑based; `TokenCounter` is a sealed class.

## Cross-References
- [[LLMProviderType]] — provider enum used as key in the mapping.
- [[TokenizerAlgorithm]] — algorithm enum driving the approximation constants.
- [[TokenCountResult]] — output record containing token counts and metadata.

## Source Files
- `AGXIOME.Core/LLM/TokenCounter.cs` (ingested 2026-05-14)