---
title: ModelStringParser
category: entity
tags: [provider, parsing, routing, LLM]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# ModelStringParser

## Summary
The `ModelStringParser` static class provides a centralised, alias‑driven mechanism for converting "provider:model" shorthand strings (e.g., `"openai:gpt-4o"`) into typed `(LLMProviderType, string)` pairs via `ParsedModelString`. It eliminates the need to instantiate provider classes solely for routing, normalises over 50 provider aliases, and offers both throwing and non‑throwing parse methods.

## Key Details
- Parses `"provider:model"` format, throwing `ArgumentException` on malformed input or unknown provider.
- The `_aliases` dictionary uses case‑insensitive keys to map prefixes like `"claude"`, `"azure"`, `"vertex"` to their canonical `LLMProviderType` enum values.
- `Parse(string)` – throws on error; `TryParse(string?)` – returns `null` on failure.
- `Format(LLMProviderType, string)` – reverses the parse, outputting lower‑cased provider name and model ID.
- `KnownPrefixes` – exposes all registered alias keys sorted alphabetically.
- No raw provider strings escape this class; all provider identification is normalised through `LLMProviderType`.
- Supports aliases for OpenAI, Anthropic, Gemini, DeepSeek, Ollama, Azure, Local, Custom, Groq, Mistral, Cohere, TogetherAI, FireworksAI, Perplexity, XAIGrok, SambaNova, Cerebras, NvidiaNIM, AWSBedrock, AzureAIFoundry, HuggingFaceInference, LlamaCpp, LMStudio, GPT4All, TextGenWebUI, KoboldCPP, LocalAI, OpenRouter, NovitaAI, and Featherless.

## Cross-References
- [[LLMProviderType]] – the enum that `ParsedModelString.Provider` maps to.
- [[LLMRouter]] – likely consumer that uses parsed strings to dispatch requests.

## Source Files
- `AGXIOME.Core/LLM/ModelStringParser.cs` (ingested 2026-05-14)