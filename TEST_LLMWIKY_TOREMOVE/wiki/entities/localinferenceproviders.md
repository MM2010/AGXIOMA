---
title: Local Inference Providers
category: entity
tags: [providers, inference, local, llamacpp, lmstudio, ollama, localai]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Local Inference Providers

## Summary
The `AGXIOME.LLMWIKY` namespace includes a set of local inference providers that enable running LLMs on the user's own hardware by wrapping local server processes. These providers abstract away the underlying server APIs (llama.cpp, LM Studio, Ollama, LocalAI) behind the common `ILLMProvider` interface, allowing the framework to use quantised models, embeddings, and tool calling without cloud dependencies.

## Key Details
- **LlamaCppProvider** — wraps a llama.cpp server (default endpoint `http://localhost:8080/v1`); supports streaming, function calling, and embeddings (dimension depends on loaded GGUF model).
- **LMStudioProvider** — wraps LM Studio's OpenAI‑compatible API (`http://localhost:1234/v1`); same capabilities as LlamaCpp, useful for local testing.
- **OllamaResponsesProvider** — uses the Ollama Responses API (`POST /api/responses`) distinct from the chat API; single-turn structured completions with tool calling support (Ollama 0.5+); does **not** support embeddings (use `OllamaProvider` instead).
- **LocalAI** — (truncated in source) adds another local inference option, typically OpenAI‑compatible.
- All providers inherit a shared `Name`, `Capabilities`, and `EmbeddingDimensions` property, and delegate to `OpenAIProvider` or custom HTTP clients.
- Common use case: running GGUF or other local models with low‑latency, offline inference.

## Cross-References
- [[entities/llmproviderinterface]] — the `ILLMProvider` contract that all local providers implement
- [[entities/openaiinferenceprovider]] — the underlying `OpenAIProvider` used by many local providers
- [[entities/ollamaprovider]] — the other Ollama provider supporting embeddings and chat

## Source Files
- `AGXIOME.LLMWIKY/Providers/LocalInferenceProviders.cs` (ingested 2026-05-14)