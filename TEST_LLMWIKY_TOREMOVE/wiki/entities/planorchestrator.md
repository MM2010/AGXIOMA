---
title: PlanOrchestrator
category: entity
tags: [orchestrator, plan, execution, async, framework]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# PlanOrchestrator

## Summary
The `PlanOrchestrator` is the master orchestrator within the AGXIOMA agent framework, implementing the Plan-and-Execute loop. It decomposes a user request into a plan, persists it, and executes each step by generating agents, spawning them, polling results, and advancing the plan until completion or failure. All plan execution is fire-and-forget from the caller's perspective, with real-time event streaming available via `WatchAsync`. It supports pause, resume, and cancellation operations, and integrates with hot/cold storage, completion notifiers, agent spawners, LLM providers, and optional cryptographic and audit logging.

## Key Details
- Implements `IPlanOrchestrator` and is sealed (not designed for inheritance).
- Constructor dependencies: `IOrchestrationStore`, `IHotStore`, `ICompletionNotifier`, `IAgentSpawner`, `ILLMProvider`, `ILogger<PlanOrchestrator>`, and optional `IAgxiomeCrypto` and `IAuditLogger`.
- Primary methods: `StartAsync`, `ResumeAsync`, `PauseAsync`, `CancelAsync`, `GetStateAsync`, `WatchAsync`.
- `StartAsync` returns a `Guid` planId immediately; execution runs asynchronously in the background.
- Internal plan loop (`RunPlanLoopAsync`) handles decomposition via `DecomposeRequestTool`, persistence via `PersistPlanTool`, and iterative step execution.
- Per-plan event channels (`ConcurrentDictionary<Guid, Channel<OrchestratorEvent>>`) enable real-time observability through `WatchAsync` (returns `IAsyncEnumerable<OrchestratorEvent>`).
- Per-plan pause signals (`ConcurrentDictionary<Guid, TaskCompletionSource>`) and cancellation token sources (`ConcurrentDictionary<Guid, CancellationTokenSource>`) manage lifecycle.
- Tracks plan metrics: `OrchestratorMetrics.PlansStarted`, `OrchestratorMetrics.ActivePlans`.
- Optional audit logging logs plan start events with pseudonymised identifiers.
- Supports pause/resume by updating plan status in cold store and hot store, and emitting `PlanPausedEvent`.
- Cancellation cancels the linked token source, updates status, and completes the event channel.

## Cross-References
- [[IPlanOrchestrator]] — the interface that `PlanOrchestrator` implements.
- [[PlanState]] — the data model for plan state persisted by `PlanOrchestrator`.
- [[OrchestratorEvent]] — base type for events streamed by `WatchAsync`.
- [[IOrchestrationStore]] — cold storage dependency for plan persistence.
- [[IHotStore]] — hot storage dependency for fast status lookups.
- [[IAgentSpawner]] — used to spawn agents during plan execution.
- [[ILLMProvider]] — LLM provider used for plan decomposition and agent generation.

## Source Files
- `AGXIOME.Orchestration/Orchestrator/PlanOrchestrator.cs` (ingested 2026-05-14)