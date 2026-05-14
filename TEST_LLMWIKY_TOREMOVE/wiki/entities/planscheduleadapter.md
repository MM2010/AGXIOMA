---
title: PlanScheduleAdapter
category: entity
tags: [scheduling, bridge, adapter, orchestration]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# PlanScheduleAdapter

## Summary
`PlanScheduleAdapter` bridges the scheduling layer (`ScheduleManager`) with the orchestration layer (`IPlanOrchestrator`). When a schedule fires and its payload contains a `PlanRequest`, this adapter invokes the orchestrator’s `StartAsync` method to launch an orchestrated plan. It uses a delegate for `IPlanOrchestrator.StartAsync` to keep `AGXIOME.Core` free of a direct assembly reference to the orchestration project; the host project wires the delegate during DI setup.

## Key Details
- Implements `IScheduledPlanBridge`, defined in `AGXIOME.Core` for testability.
- Accepts a delegate `Func<string, string, CancellationToken, Task<Guid>>` that matches the minimal surface of `IPlanOrchestrator.StartAsync`.
- Called by [[SchedulePoller]] when a schedule fires.
- Only launches a plan if `SchedulePayload.PlanRequest` is non-empty; otherwise returns `null`.
- Uses `schedule.Payload.SessionId` or falls back to `"schedule:{schedule.Id}"` as the session identifier.
- Logs details at `Debug` level when skipping, `Information` on successful plan launch, and `Error` on failure.
- Returns `Guid?` — the ID of the started plan, or `null` if no plan request or if an exception occurs.

## Cross-References
- [[IScheduledPlanBridge]] — the abstraction that allows unit testing without `IPlanOrchestrator`.
- [[SchedulePoller]] — the component that triggers this adapter when a schedule fires.
- [[IPlanOrchestrator]] — the orchestration interface whose `StartAsync` method is invoked via the delegate.
- [[SchedulePayload]] — the data structure containing `PlanRequest` and `SessionId`.

## Source Files
- `AGXIOME.Core/Scheduling/PlanScheduleAdapter.cs` (ingested 2026-05-14)