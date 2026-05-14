---
title: Agent Edge Tests
category: entity
tags: [tests, edge-cases, agent]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Agent Edge Tests

## Summary
The `AgentEdgeTests` class validates the robustness and correct behavior of the [[Agent]] framework under boundary conditions and uncommon execution paths. It covers scenarios such as empty prompts, zero max iterations, cancellation, tool-induced loops, hook execution (pre-run and background), multiple guardrails, blocking guardrails, session state persistence across runs, concurrent execution guarding, and error propagation from tools. These tests ensure the agent remains stable and predictable when faced with edge-case inputs, configuration, or runtime events.

## Key Details
- **Namespace**: `AGXIOME.Tests`
- **Base class**: `AgentEdgeTests` (plain class, no base)
- **Test methods (11)**:
  - `EmptyPrompt_ReturnsResponse` – verifies agent completes when given an empty prompt.
  - `MaxIterationsZero_Completes` – ensures agent handles zero iteration limit gracefully.
  - `CancelledToken_Throws` – confirms agent returns error status when cancellation is requested before execution.
  - `ToolLoop_MaxIterations_ReturnsResponse` – uses a `LoopTool` that returns a continuation command to test iteration limit enforcement.
  - `PreHook_ModifiesContext` – validates that a pre-run hook can modify context and that the modification is observable.
  - `BackgroundHook_DoesNotBlock` – ensures background hooks are truly asynchronous and do not delay agent completion.
  - `MultipleGuardrails_AllCheck` – confirms multiple guardrails are all invoked (even when none block).
  - `Guardrail_BlocksWithMessage` – tests that a guardrail with `Block` severity prevents execution and includes the error code `ERR-0101`.
  - `SessionState_PersistsAcrossRuns` – demonstrates session state is preserved between agent runs.
  - `ConcurrentRun_OnlyOneExecutes` – checks that simultaneous `RunAsync` calls do not interfere or duplicate execution.
  - `ToolWithError_DoesNotBlockLoop` – ensures a tool that throws an exception does not cause the agent loop to hang.
- **Helper types** (defined in same file):
  - `LoopTool` – a [[Tool]] that returns a command to call itself again.
  - `ErrorTool` – a [[Tool]] that throws `InvalidOperationException`.
  - `TestGuardrail` – a configurable [[Guardrail]] for testing different severities and logic.
- **Dependencies**: `AgentFactory`, `AgentRequest`, `AgentStatus`, `IGuardrail`, `AgxiomeFunction`, `HookPoint`, `DelegateHook`.
- **Test framework**: xUnit (async test methods).

## Cross-References
- [[Agent]] – the core entity under test.
- [[Tool]] – tools are registered and executed during agent runs; `LoopTool` and `ErrorTool` test tool loop and error handling.
- [[Guardrail]] – guardrails validate input/output; `TestGuardrail` demonstrates blocking and logging behavior.
- [[Hook]] – hooks are attached via `AddHook`; tests cover pre-run and background hooks.
- [[SessionState]] – state persists across runs; tested in `SessionState_PersistsAcrossRuns`.
- [[AgentFactory]] – used to create test agents with optional configuration names.

## Source Files
- `AGXIOME.Tests/AgentEdgeTests.cs` (ingested 2026-05-14)