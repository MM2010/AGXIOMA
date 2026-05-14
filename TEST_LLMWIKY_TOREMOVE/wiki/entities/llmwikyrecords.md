---
title: LLMWIKYRecords
category: entity
tags: [llm, records, models]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# LLMWIKYRecords

## Summary
The `LLMWIKYRecords` are a set of C# record types defined in the `AGXIOME.Shared.Models` namespace that standardize communication between agents and large language model (LLM) providers. They cover request/response payloads, streaming chunks, token usage statistics, and provider capability metadata, enabling a unified interface for LLM interactions within the AGXIOMA framework.

## Key Details
- `LLMWIKYRequest` – immutable request record with `SystemPrompt`, `UserPrompt`, optional `History` (list of [[AgentMessage]]), `Temperature`, `MaxTokens`, and `Tools` (list of JSON objects).
- `LLMWIKYResponse` – response record with `Content`, optional `ToolCallId`, `ToolName`, `ToolArguments`, and `Usage` ([[LLMWIKYUsage]]).
- `LLMWIKYChunk` – streaming chunk record with `Content`, `IsToolCall`, `ToolCallId`, and `IsFinal` flags.
- `LLMWIKYUsage` – usage statistics record with `PromptTokens`, `CompletionTokens`, and `TotalTokens`.
- `LLMCompletionRequest` – a separate request record that adds a `Model` field, used for direct completion calls.
- `LLMProviderCapabilities` – record describing provider abilities: `SupportsStreaming`, `SupportsFunctionCalling`, `SupportsVision`, `EmbeddingDimensions`, `MaxContextTokens`, and `SupportedModels`.
- All records use C# `record` syntax with immutable properties and optional parameters with sensible defaults (e.g., `Temperature = 0.7`, `MaxTokens = 4096`).
- Designed to support both standard text generation and tool/function calling patterns.

## Cross-References
- [[AgentMessage]] – used as the type for `History` in request records.
- [[LLMWikyAgent]] – likely consumer of these records for agentic LLM interactions.

## Source Files
- `AGXIOME.Shared/Models/LLMWIKYRecords.cs` (ingested 2026-05-14)