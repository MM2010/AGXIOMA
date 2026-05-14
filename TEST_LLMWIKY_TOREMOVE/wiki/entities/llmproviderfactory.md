---
title: LLMProviderFactory
category: entity
tags: [provider factory, LLM provider, dependency injection, factory pattern]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# LLMProviderFactory

## Summary
The `LLMProviderFactory` is a sealed class implementing `ILLMProviderFactory` that serves as the central registry and factory for all LLM providers in the AGXIOMA system. It maps `LLMProviderType` enum values to concrete `ILLMProvider` factory functions, eliminating magic strings from provider routing logic. The factory comes pre-populated with a comprehensive set of built-in providers (over 50) and supports dynamic registration of custom providers at runtime.

## Key Details
- Implements `ILLMProviderFactory` interface.
- Uses a private static dictionary `_builtIn` holding default factory functions for all supported provider types (e.g., OpenAI, Anthropic, Ollama, local providers, etc.).
- Uses a mutable instance dictionary `_registry` initialized from `_builtIn`, allowing overrides and additions.
- Two `RegisterProvider` overloads: one typed (`LLMProviderType`) and one string-based (parses `LLMProviderType` via `Enum.TryParse`).
- `CreateProvider(LLMWIKYConfig config)` resolves the factory by `config.Provider` and returns an `ILLMProvider` instance.
- `ListAvailableProviders()` returns a read-only list of `ProviderDescriptor` objects, including provider name, supported models, and flags indicating cloud/local/active status.
- A static convenience method `Create(LLMWIKYConfig config)` creates a temporary factory instance and calls `CreateProvider`.
- Supports a vast array of providers including cloud APIs (OpenAI, Anthropic, Gemini, Cohere, etc.) and local models (Ollama, LMStudio, GPT4All, etc.).
- Exception handling: throws `ArgumentException` if a provider type is not registered or if a string name cannot be parsed.

## Cross-References
- [[ILLMProviderFactory]] — interface implemented by this factory.
- [[LLMProviderType]] — enum used as keys for provider routing.
- [[LLMWIKYConfig]] — configuration object consumed by factory methods.
- [[ProviderDescriptor]] — data class returned by `ListAvailableProviders`.
- [[Providers]] — namespace containing concrete provider implementations (e.g., `OpenAIProvider`, `AnthropicProvider`).

## Source Files
- `AGXIOME.LLMWIKY/LLMProviderFactory.cs` (ingested 2026-05-14)