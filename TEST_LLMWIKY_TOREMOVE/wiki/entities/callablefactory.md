---
title: CallableFactory
category: entity
tags: [tool, LLM, serialization, factory]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# CallableFactory

## Summary
`CallableFactory` is the central component that converts `IAgxiomeExecutableTool` instances into the provider-specific JSON callable schemas required by major LLM APIs (OpenAI, Anthropic, Gemini). It also parses raw tool-call responses from those models into a normalized `ParsedToolCall` record. This abstraction decouples tool definitions from serialization formats, allowing the same tool to be used across different LLM back‑end providers without modification.

## Key Details
- Defines the `LlmCallableFormat` enum (`OpenAiFunction`, `AnthropicTool`, `GeminiTool`) to select the target schema.
- Produces a `CallableDefinition` record containing the tool name, description, parameters schema, format, and the complete provider payload.
- Implements `ICallableFactory` with four main operations:
  - `CreateCallable` – build a single definition from one tool.
  - `CreateCallableList` – build definitions from a collection, silently skipping tools that fail serialization.
  - `BuildToolsArray` – return a `JsonArray` of raw payloads ready for embedding in an LLM request.
  - `ParseToolCall` – extract tool name, call ID, and arguments JSON from a provider-specific response node.
- Payload builders for each format:
  - **OpenAI**: `{ "type": "function", "function": { "name", "description", "parameters" } }`
  - **Anthropic**: `{ "name", "description", "input_schema" }`
  - **Gemini**: `{ "name", "description", "parameters" }`
- Response parsers mirror the same three formats, returning `null` if the input does not match the expected structure.
- The underlying tool’s JSON schema is obtained via `IAgxiomeExecutableTool.ToJsonSchema()`, which is expected to return a `{ name, description, parameters }` object.
- Error handling in `CreateCallableList` catches exceptions (e.g., from malformed tools) and logs a warning — execution never throws.

## Cross-References
- [[IAgxiomeExecutableTool]] — the interface that every tool must implement; `CallableFactory` depends on it for schema generation.
- [[LlmCallableFormat]] — the enum that controls which provider schema is generated (defined in the same source file).
- [[ToolExecution]] — likely consumer of `ParsedToolCall` results for invoking tools during agent runs.

## Source Files
- `AGXIOME.Core/Tools/CallableFactory.cs` (ingested 2026-05-14)