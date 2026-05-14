---
title: AgentCodeGenerator
category: entity
tags: [agent-generation, llm, code-generation, dynamic-runtime]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AgentCodeGenerator

## Summary
The `AgentCodeGenerator` class orchestrates the end-to-end pipeline that converts a natural-language agent description into a compiled, registered C# agent. It sends a prompt to an LLM, extracts the generated C# code from a ````csharp` block, validates it, compiles it with [[AgentCompiler]], saves the source file to a hot-folder, and registers the agent in the [[DynamicAgentRegistry]]. The process includes automatic retry (up to 3 attempts) with error feedback from Roslyn compilation failures.

## Key Details
- Sealed class located in `AGXIOME.DynamicRuntime.Generation` namespace.
- Constructor requires `ILLMProvider`, `AgentCompiler`, `DynamicAgentRegistry`, and optional `basePath` (defaults to `~/.agxioma`).
- `GenerateAsync` takes an `AgentGenerationRequest` and cancellation token, returns `AgentGenerationResult`.
- Uses regex ``````csharp\s*(.*?)\s*`````` to extract code blocks.
- Validates generated code with `AgentCodeValidator.Validate` before compilation.
- Retries up to `MaxRetries = 3` times, appending previous compiler/validation errors to the user prompt.
- LLM call uses `AgentGenerationSystemPrompt.Build` for system prompt and dynamically builds user prompt with description, optional example input/output.
- Temperature fixed at 0.2; max tokens 2000.
- Extracts agent ID from `[AgxiomeAgent("...")]` attribute using regex; falls back to a GUID-based ID.
- Saves `.cs` file to `{basePath}/agents/{userId}/src/{agentId}.cs`.
- Registers a `DiscoveredAgentEntry` with status `Compiled` in the registry.
- On success, returns the generated code and file path; on failure, aggregates errors.

## Cross-References
- [[AgentCompiler]] — Performs Roslyn compilation of the generated C# code.
- [[DynamicAgentRegistry]] — Stores the discovered agent entry after successful compilation.
- [[AgentCodeValidator]] — Performs static validation of the generated code before compilation.
- [[HotFolder]] — The file saving location (``src`` folder) that is monitored for new agents.
- [[LLMProvider]] — Interface for the LLM used to generate code.
- [[AgentGenerationRequest]] — Input model containing description, user ID, and allowed namespaces.
- [[AgentGenerationResult]] — Output model containing success status, generated code, errors, and timing.
- [[AgentGenerationSystemPrompt]] — Builds the system prompt for the LLM.

## Source Files
- `AGXIOME.DynamicRuntime/Generation/AgentCodeGenerator.cs` (ingested 2026-05-14)