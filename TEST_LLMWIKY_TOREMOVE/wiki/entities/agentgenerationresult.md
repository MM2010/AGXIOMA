---
title: AgentGenerationResult
category: entity
tags: [agent, generation, result, entity]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AgentGenerationResult

## Summary
An immutable record that encapsulates the outcome of a single LLM-driven agent generation operation. It reports whether the generation succeeded, the generated source code, the assigned agent identifier, any errors encountered, the total elapsed time, and an optional file path where the output was persisted.

## Key Details
- **Immutable record type** – instances are created once and cannot be modified.
- **Success flag** (`bool Success`) – indicates if generation completed without errors.
- **GeneratedCode** – the full source code produced by the LLM.
- **Errors** – a read-only list of error strings (empty on success).
- **TotalTime** – total duration of the generation call.
- **SavedFilePath** – optional path to the file where the generated code was written (null if not saved).
- **AgentId** – the unique identifier assigned to the generated agent.
- Typically created at the end of the `GenerateAgentAsync` pipeline in the runtime.

## Cross-References
- [[AgentGenerationRequest]] — the input that produces this result.
- [[DynamicAgentEngine]] — the orchestrator that manages the generation flow.
- [[AgentRuntime]] — consumes generated agents for execution.

## Source Files
- `AGXIOME.DynamicRuntime/Generation/AgentGenerationResult.cs` (ingested 2026-05-14)