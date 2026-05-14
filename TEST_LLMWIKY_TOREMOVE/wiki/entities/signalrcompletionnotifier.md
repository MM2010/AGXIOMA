---
title: SignalRCompletionNotifier
category: entity
tags: [signalr, completion, notifier, orchestrator, hitl]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# SignalRCompletionNotifier

## Summary
The `SignalRCompletionNotifier` is a singleton that implements both `ICompletionNotifier` and `IHitlCompletionSource`. It provides a dual-channel mechanism: in‑process `Channel<TaskResult>` queues per plan for the Master orchestrator’s polling loop, and real‑time SignalR fan‑out to front‑end clients subscribed to plan‑specific groups. It also bridges HITL (Human‑In‑The‑Loop) resolution events, pushing synthetic `TaskResult` entries into the orchestration channel and notifying clients of lifecycle transitions (`PlanStarted`, `TaskSpawned`, `PlanCompleted`, etc.). This eliminates the need for polling while keeping the agent runtime decoupled from the web layer.

## Key Details
- Registered as `AddSingleton<SignalRCompletionNotifier>()` with overrides for `ICompletionNotifier` and `IHitlCompletionSource` in DI.
- Maintains a `ConcurrentDictionary<Guid, Channel<TaskResult>>` — one unbounded channel per plan.
- `PublishAsync(TaskResult, CancellationToken)`: writes the result into the plan’s channel and pushes HITL‑waiting events to the SignalR group `plan:{planId}`.
- `SubscribeAsync(Guid planId, CancellationToken)`: returns an `IAsyncEnumerable<TaskResult>` reading from the plan’s channel.
- `NotifyResolvedAsync(...)` (IHitlCompletionSource): creates a synthetic `TaskResult` with `Success` set to `approved`, resets `IsHitlPaused`, and calls `PublishAsync` to resume the orchestrator.
- `BroadcastPlanEventAsync(OrchestratorEvent)`: maps each concrete event type (e.g., `PlanStartedEvent`, `TaskHitlPausedEvent`) to the corresponding SignalR client method (`OnPlanStarted`, `OnPlanWaitingForHITL`, etc.). On `PlanCompletedEvent` or `PlanFailedEvent`, the channel writer is completed to signal no more results.
- Does **not** push duplicate task‑spawned or task‑completed events to avoid double‑counting — those are surfaced by the orchestrator via `WatchAsync`.

## Cross-References
- [[icompletionnotifier]] — the in‑process completion notification interface it implements.
- [[ihitlcompletionsource]] — the HITL resolution interface it implements.
- [[agenthub]] — the SignalR hub whose `IHubContext` is injected.
- [[taskresult]] — the model class written to channels and broadcasted.
- [[orchestratorevent]] — the discriminated union of lifecycle events handled by `BroadcastPlanEventAsync`.

## Source Files
- `AGXIOME.Realtime/Hubs/SignalRCompletionNotifier.cs` (ingested 2026-05-14)