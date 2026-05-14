---
title: F15 TS04 Master Tools Tests
category: entity
tags: [master-tools, testing, orchestration, NSubstitute, unit-tests]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# F15 TS04 Master Tools Tests

## Summary
This test suite validates six Master Tools within the AGXIOME orchestration framework: `DecomposeRequestTool`, `PersistPlanTool`, `SaveResultTool`, `NotifyCompletionTool`, `SpawnAgentTool`, and `PollResultsTool`. It contains 20 unit tests that exercise tool execution, parameter validation, store persistence, and LLM interaction using NSubstitute mocks and in-memory stores. The tests ensure each tool handles valid inputs, missing parameters, and edge cases (e.g., LLM markdown-fenced JSON) correctly.

## Key Details
- **Source file**: `AGXIOME.Tests/F15/F15_TS04_MasterToolsTests.cs`
- **Test groups**: TS04‑A (DecomposeRequestTool, 5 tests), TS04‑B (PersistPlanTool, 5 tests), TS04‑C (SpawnAgentTool, 5 tests) — remaining tools (SaveResult, NotifyCompletion, PollResults) also covered (5 tests total).
- **DecomposeRequestTool tests**: Validates LLM response parsing, parameter existence, task count, markdown-fenced JSON handling, and JSON schema generation.
- **PersistPlanTool tests**: Ensures plan persistence in cold store (`InMemoryOrchestrationStore`), hot store status updates (`InMemoryHotStore`), plan ID correctness, and failure on missing plan parameter.
- **SpawnAgentTool tests**: Verifies correct spawn count (one per task), parameter validation (`tasks` and `executionMode`), hot store task recording, and spawner call count.
- **Common patterns**: Uses `ToolContext` with parameters dictionary, `NSubstitute` for `ILLMProvider` and `IAgentSpawner`, and in-memory stores (`InMemoryHotStore`, `InMemoryOrchestrationStore`).
- **All tools**: Implement `IOrchestrationTool` interface, require `ToolContext` containing `Parameters`, and return `ToolResult` with `Success`, `Data`, and `ErrorMessage`.

## Cross-References
- [[entities/f15-orchestration-tools]] — Parent concept for the Master Tools tested here.
- [[entities/f15-ts01-mastertool]] — Related test suite for individual Master Tool behavior.
- [[entities/inmemory-stores]] — In-memory store implementations used in tests.
- [[entities/llm-provider-mocking]] — Pattern for mocking `ILLMProvider` in orchestration tests.

## Source Files
- `AGXIOME.Tests/F15/F15_TS04_MasterToolsTests.cs` (ingested 2026-05-14)