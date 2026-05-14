---
title: ModelCompatibilityMap
category: entity
tags: [model, compatibility, registry, LLM]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# ModelCompatibilityMap

## Summary
The `ModelCompatibilityMap` is a centralized, enum-driven registry that maps LLM model identifiers (e.g., `"gpt-4o"`, `"claude-3-5-sonnet-20241022"`) to their canonical provider and lifecycle status. It allows downstream code to query deprecation warnings and supported providers without hard-coding model names, using `LLMProviderType` and `ModelCompatibilityStatus` enums exclusively. The map is initialized with well-known models from OpenAI, Anthropic, Google, DeepSeek, Mistral, Groq, and Ollama, and can be extended at startup via `RegisterModel()`.

## Key Details
- Sealed class with a private dictionary using ordinal case-insensitive key comparison.
- Contains predefined entries for major providers: OpenAI, Anthropic, Gemini, DeepSeek, Mistral, Groq, Ollama.
- `RegisterModel()` allows adding or updating entries at startup for custom or private models.
- `Check(modelId)` returns a `CompatibilityCheckResult` with a warning message for deprecated, removed, or preview statuses; unknown models produce a warning but are treated as supported with `Custom` provider.
- `GetProvider(modelId)` returns the `LLMProviderType?` for a known model, or `null` if unknown.
- All status values use the `ModelCompatibilityStatus` enum: `Supported`, `Deprecated`, `Removed`, `Preview`.
- `ModelEntry` record holds `Provider`, `Status`, optional `ReplacedBy`, `DeprecationNote`, and `DeprecatedSince`.
- The map is designed to surface deprecation warnings before sending requests, avoiding hard-coded model names in application logic.

## Cross-References
- [[entities/llmprovidertype]] — Enum used for provider identification in all entries.
- [[entities/modelcompatibilitystatus]] — Enum defining lifecycle statuses (`Supported`, `Deprecated`, etc.).
- [[entities/modelentry]] — Record type representing a single model’s metadata in the map.
- [[entities/compatibilitycheckresult]] — Record returned by `Check()` containing model ID, provider, status, and optional warning.

## Source Files
- `AGXIOME.Core/LLM/ModelCompatibilityMap.cs` (ingested 2026-05-14)