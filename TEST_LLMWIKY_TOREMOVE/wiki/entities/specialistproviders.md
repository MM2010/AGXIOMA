---
title: Specialist Providers
category: entity
tags: [provider, specialist, openai-compatible, wrapper]
source_count: 5
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Specialist Providers

## Summary
The specialist providers are a family of `ILLMProvider` implementations that wrap the [[OpenAIProvider]] with a distinct API endpoint and a tailored set of supported models. Each represents a third‑party AI service (e.g., AI21 Labs, Writer, NVIDIA NIM, Anyscale, OctoAI) that exposes an OpenAI‑compatible chat interface. They all inherit streaming and function‑calling capabilities from the underlying provider but explicitly do **not** support embeddings. This pattern allows the AGXIOME framework to transparently route requests to a wide range of specialized language model services while reusing the same client‑side invocation logic.

## Key Details
- **AI21LabsProvider** – Endpoint `https://api.ai21.com/studio/v1`. Models: `jamba-1.5-large`, `jamba-1.5-mini`, `j2-ultra`, `j2-mid`. Max context: 256k tokens. No embeddings.
- **WriterProvider** – Endpoint `https://api.writer.com/v1`. Models: `palmyra-x-004`, `palmyra-x-003-instruct`, `palmyra-med`, `palmyra-fin`. Max context: 128k tokens. No embeddings.
- **NvidiaNIMProvider** – Default endpoint `https://integrate.api.nvidia.com/v1` (configurable). Models: `nvidia/llama-3.1-nemotron-70b-instruct`, `meta/llama-3.1-70b-instruct`, `mistralai/mistral-7b-instruct-v0.3`. Max context: 131k tokens. Embedding must use separate NIM containers.
- **AnyscaleProvider** – Endpoint `https://api.endpoints.anyscale.com/v1`. Models: `meta-llama/Meta-Llama-3.1-70B-Instruct`, `mistralai/Mixtral-8x7B-Instruct-v0.1`. Max context: 131k tokens. No embeddings.
- **OctoAIProvider** – Endpoint `https://text.octoai.run/v1`. Models: `meta-llama-3.1-70b-instruct`, `mixtral-8x7b-instruct-fp16`, `hermes-2-pro-llama-3-8b`. Max context: 131k tokens. No embeddings.
- All providers set `SupportsVision` to `false` and `EmbeddingDimensions` to `0`.
- The constructor pattern always creates an inner `OpenAIProvider` with a modified endpoint; all `CompleteAsync` and `CompleteStreamAsync` calls are delegated to that inner instance.

## Cross-References
- [[LLMProviderCapabilities]] – Describes the capability structure used by each provider.
- [[OpenAIProvider]] – The underlying implementation that handles actual API communication.
- [[ILLMProvider]] – The interface all specialist providers implement.
- [[Entities/LLMWIKYConfig]] – Configuration class used to pass the endpoint and API key.

## Source Files
- `AGXIOME.LLMWIKY/Providers/SpecialistProviders.cs` (ingested 2026-05-14)