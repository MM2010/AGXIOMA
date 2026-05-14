---
title: IAgxiomeAgent
category: entity
tags: [interface, agent, core]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# IAgxiomeAgent

## Summary
The central contract for all agents in the AGXIOME framework. It defines agent lifecycle (run, stream, continue), composition (tools, middleware, hooks, guardrails), and advanced capabilities (learning, typed output schemas, human-in-the-loop). Any agent implementation must implement this interface to participate in the runtime.

## Key Details
- Inherits `IDisposable` — agents must clean up resources.
- `Id` and `Status` provide identity and lifecycle state.
- `Memory` ([[IAgentMemory]]) and optional `Learning` ([[ILearningMachine]]) enable state persistence and adaptation.
- `SessionId` and `SessionState` support conversational sessions.
- `RunAsync` has a generic overload for typed responses.
- `RunStreamAsync` yields `AgentChunk` for incremental output; `RunEventStreamAsync` yields `RunOutputEvent` for richer event streams.
- `ContinueRunAsync` handles human-in-the-loop (HITL) by resuming a run with user-provided requirements.
- Tool registration: `RegisterFunction<T>()` for typed functions inheriting `AgxiomeFunction`, and `RegisterTool(IAgxiomeTool)` for arbitrary tools.
- Middleware, hooks, tool hooks, and guardrails are added via `UseMiddleware`, `AddHook`, `AddToolHook`, `AddGuardrail`.
- `SetOutputSchema<T>()` and `SetInputSchema<T>()` enforce typed schemas on agent inputs/outputs.
- `RequireConfirmationFor<T>()` marks specific functions as requiring user confirmation.
- `EnableLearning()` activates adaptive learning with optional `LearningConfig`.

## Cross-References
- [[IAgxiomeTool]] — tools registrable via `RegisterTool`
- [[AgxiomeFunction]] — base class for typed functions
- [[IAgentMemory]] — memory interface used by agents
- [[ILearningMachine]] — learning subsystem
- [[IAgentMiddleware]] — middleware for pipeline interception
- [[IAgentHook]] / [[IToolHook]] — lifecycle hooks
- [[IGuardrail]] — guardrail interface
- [[AgentRequest]] / [[AgentResponse]] — core request/response types
- [[AgentChunk]] — streaming output unit
- [[RunOutputEvent]] — event stream output
- [[HITLRequirement]] — human-in-the-loop requirement type

## Source Files
- `AGXIOME.Shared/Interfaces/IAgxiomeAgent.cs` (ingested 2026-05-14)