---
title: IPlanOrchestrator
category: entity
tags: [interface, orchestrator, planning, events, execution]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# IPlanOrchestrator

## Summary
Master orchestrator contract that receives a user request, decomposes it into a plan, persists the plan, and coordinates background execution across sub-agents. It exposes lifecycle methods for managing plan execution (start, resume, pause, cancel), state queries, and a real-time event stream. The orchestrator also supports human-in-the-loop pauses via `TaskHitlPausedEvent` and relies on [`OrchestratorOptions`](#orchestratoroptions) for global execution tuning.

## Key Details
- Designed for asynchronous, long-running plan execution; `StartAsync` returns a `Guid` immediately.
- Supports lifecycle control: `StartAsync`, `ResumeAsync`, `PauseAsync`, `CancelAsync`.
- State retrieval via `GetStateAsync` returns `PlanState?`.
- Real-time monitoring via `WatchAsync` — streams `OrchestratorEvent` records until cancellation.
- Orchestrator events include: `PlanStarted`, `PlanDecomposed`, `TaskSpawned`, `TaskCompleted`, `StepAdvanced`, `PlanPaused`, `TaskHitlPaused`, `PlanCompleted`, `PlanFailed`.
- `OrchestratorOptions` governs max parallelism, per-task timeout, auto-resume, preferred LLM, global tool allowlist, and retry policy.
- `TaskHitlPausedEvent` indicates a task waiting for human approval; orchestrator remains in `WaitingForHITL` state until resolved by an external `IHitlCompletionSource`.
- Implementation is expected to handle plan persistence, agent coordination, and error recovery.

## Cross-References
- [[PlanState]] — the state object returned by `GetStateAsync` and used internally for resumption.
- [[OrchestratorEvent]] — base record for all streaming events; concrete subtypes listed above.
- [[IHitlCompletionSource]] — external resolver for human-in-the-loop pauses triggered by `TaskHitlPausedEvent`.
- [[OrchestratorOptions]] — configurable runtime parameters for plan execution.

## Source Files
- `AGXIOME.Orchestration/Abstractions/IPlanOrchestrator.cs` (ingested 2026-05-14)