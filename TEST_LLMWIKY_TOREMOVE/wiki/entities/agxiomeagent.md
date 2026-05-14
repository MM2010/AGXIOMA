---
title: AgxiomeAgent
category: entity
tags: [agent, core, LLM, runtime, hooks, guardrails, memory]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AgxiomeAgent

## Summary
The `AgxiomeAgent` is the central runtime entity in the AGXIOMA framework. Each agent instance encapsulates one LLM engine, a tool registry, configurable execution hooks, guardrails, persistent memory, and optional learning. It orchestrated the full lifecycle of a conversational or tool‑calling agent, including input/output guardrail checks, streaming responses, human‑in‑the‑loop continuation, and background learning. The agent also integrates with `IAgxiomeLogger` for per‑module debug‑level control and supports multiple logging providers (Seq, NLog, Graylog, Serilog, Console).

## Key Details
- Implements `IAgxiomeAgent` and `IAsyncDisposable`.
- Internal fields: `ILLMWIKYEngine`, `IToolRegistry`, `ILogger<AgxiomeAgent>`, `IAgxiomeLogger`, `AgentConfig`, lists of hooks, tool hooks, guardrails, middleware, and a `ConcurrentDictionary` for paused run state.
- State machine: Idle → Thinking → Completed / Error / Streaming.
- Core method `RunAsync(AgentRequest, CancellationToken)`: runs guardrails, pre‑hooks, the LLM loop (with configurable max iterations and auto‑stop), post‑hooks, and background hooks. On failure, returns an `AgxiomeException` wrapped in an error response.
- Generic overload `RunAsync<T>(...)` attempts to deserialize the response content as JSON into type `T`.
- Streaming variants: `RunStreamAsync` yields `AgentChunk` (content, tool call, final flag); `RunEventStreamAsync` yields `RunOutputEvent` with lifecycle events.
- `ContinueRunAsync` resumes a paused run using `HITLRequirement` approvals and re‑executes the LLM loop with updated history.
- Registration methods: `RegisterFunction<T>`, `RegisterTool`, `UseMiddleware`, `AddHook`, `AddToolHook`, `AddGuardrail`.
- Learning is enabled via `EnableLearning(LearningConfig?)` which sets an `ILearningMachine` (default `LearningMachine`).
- Underlying LLM loop respects `MaxIterationsMode` (Auto, Infinite, or custom) and uses `AutoStopCriteria` evaluator.
- Logs run duration, chunk count, and failure details per run.

## Cross-References
- [[IAgxiomeAgent]] — contract implemented by this agent
- [[AgentConfig]] — configuration used to create the agent (max iterations, persistence, stop criteria)
- [[ILLMWIKYEngine]] — the LLM inference engine consumed by the agent
- [[IToolRegistry]] — registry of tools and functions the agent can invoke
- [[IAgentHook]] and [[IToolHook]] — extension points for custom hooks
- [[IGuardrail]] — input/output validation guards
- [[IAgentMemory]] / [[AgentMemoryManager]] — memory and persistence layer
- [[LearningMachine]] — optional learning processor triggered after each run
- [[AgxiomeException]] / [[AgxiomeErrorCode]] — error handling pattern
- [[AgentRequest]], [[AgentResponse]], [[AgentChunk]], [[RunOutputEvent]] — core data models

## Source Files
- `AGXIOME.Core/Agent/AgxiomeAgent.cs` (ingested 2026-05-14)