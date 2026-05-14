---
title: DynamicAgentAdapter
category: entity
tags: [adapter, bridge, dynamic-runtime, agent]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# DynamicAgentAdapter

## Summary
The `DynamicAgentAdapter` bridges the minimal user-facing `ISimpleDynamicAgent` interface with the full `IAgxiomeAgent` contract required by the AGXIOMA framework. It wraps a simple agent and provides default no‑op or stub implementations for all advanced features (HITL, tools, middleware, hooks, guardrails, learning, memory, session state), allowing simple agents to be used seamlessly in places where a full `IAgxiomeAgent` is expected. The adapter also includes two internal helper classes: `NullAgentMemory` and `NullSessionState`, which offer empty implementations of the memory and session interfaces.

## Key Details
- Implements `IAgxiomeAgent` by delegating to an inner `ISimpleDynamicAgent`.
- Constructor: `DynamicAgentAdapter(ISimpleDynamicAgent inner)`.
- `Id` is taken from the inner agent’s `AgentId`.
- Default `Status` is `AgentStatus.Idle`; transitions to `Thinking` during `RunAsync`.
- `Memory` is a `NullAgentMemory` (saves/loads nothing), `Learning` is `null`.
- `RunAsync(AgentRequest, CancellationToken)` invokes `_inner.RunAsync(request.Prompt, ct)` and returns a completed `AgentResponse`.
- `RunAsync<T>(AgentRequest, CancellationToken)` deserializes the output string to the requested type using `System.Text.Json`.
- `RunStreamAsync` yields a single final `AgentChunk` with the full output.
- `RunEventStreamAsync` yields `RunStart` and `RunCompleted` events.
- `ContinueRunAsync` throws `NotSupportedException` (HITL not supported).
- All registration and configuration methods (`RegisterFunction`, `RegisterTool`, `UseMiddleware`, `AddHook`, `AddToolHook`, `AddGuardrail`, `SetOutputSchema`, `SetInputSchema`, `RequireConfirmationFor`, `EnableLearning`, `Dispose`) are no‑ops.
- File‑scoped classes `NullAgentMemory` and `NullSessionState` provide empty implementations for `IAgentMemory` and `ISessionState` respectively.

## Cross-References
- [[IAgxiomeAgent]] — the interface being adapted to
- [[ISimpleDynamicAgent]] — the minimal interface being adapted from
- [[AgentStatus]] — state management during execution
- [[AgentRequest]] / [[AgentResponse]] — request/response types
- [[AgentChunk]] / [[RunOutputEvent]] — streaming types
- [[NullAgentMemory]] — empty memory implementation
- [[NullSessionState]] — empty session state implementation

## Source Files
- `AGXIOME.DynamicRuntime/Bridge/DynamicAgentAdapter.cs` (ingested 2026-05-14)