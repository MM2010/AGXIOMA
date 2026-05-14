---
title: PlanResumeService
category: entity
tags: [hosted service, resilience, plan orchestration, crash recovery]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# PlanResumeService

## Summary
`PlanResumeService` is a background hosted service that runs on startup to detect and resume any plans left in a Running or Paused state from a previous process crash or normal shutdown. By querying the canonical orchestration store and delegating resume logic to the plan orchestrator, it provides crash recovery resilience, ensuring no active plan is silently abandoned.

## Key Details
- Implements `IHostedService`; `StartAsync` performs all resume work, `StopAsync` is a no-op.
- Injected dependencies: `IPlanOrchestrator` (to resume plans) and `IOrchestrationStore` (to fetch active plans).
- Fetches active plans via `_cold.GetActivePlansAsync(cancellationToken)`; if none, logs a debug message and exits.
- Iterates over each active plan, calling `_orchestrator.ResumeAsync(plan.PlanId, cancellationToken)`.
- Logs resume success with plan ID, current step, and max step index on each plan.
- Catches and logs individual exceptions per plan without halting the resumption of others.
- Designed to be registered as a hosted service in the application's service collection (e.g., via `AddHostedService<PlanResumeService>()`).

## Cross-References
- [[IPlanOrchestrator]] — central orchestrator used to resume individual plans.
- [[IOrchestrationStore]] — canonical persistence store queried for active plan state.
- [[PlanOrchestrator]] — likely implementation of `IPlanOrchestrator` that contains the resume logic.

## Source Files
- `AGXIOME.Orchestration/Orchestrator/PlanResumeService.cs` (ingested 2026-05-14)