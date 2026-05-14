---
title: InMemoryOrchestrationStore
category: entity
tags: [orchestration, store, in-memory, testing]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# InMemoryOrchestrationStore

## Summary
An in-memory implementation of [[IOrchestrationStore]] backed by three `ConcurrentDictionary` collections. Designed exclusively for testing and local development, it provides thread-safe storage for plans, task specs, and results without any persistence. The store exposes the full `IOrchestrationStore` contract and includes a `Clear()` method for easy test isolation.

## Key Details
- Uses `ConcurrentDictionary<Guid, PlanState>`, `ConcurrentDictionary<Guid, TaskSpec>`, and `ConcurrentDictionary<Guid, TaskResult>` for data storage.
- All operations are non-blocking and return `Task.CompletedTask` — no actual async I/O.
- `SavePlanAsync` / `GetPlanAsync` / `UpdatePlanStatusAsync` / `AdvancePlanStepAsync` manage plan lifecycle.
- `GetActivePlansAsync` returns plans with status `Running` or `Paused`.
- Task spec storage via `SaveTaskSpecAsync` / `GetTaskSpecAsync` / `UpdateTaskStatusAsync`.
- Result storage via `SaveResultAsync` / `GetResultAsync` / `GetPlanResultsAsync`.
- `UpdatePlanStatusAsync` automatically sets `CompletedAt` when final status is reached.
- `Clear()` resets all three dictionaries — intended for test setup/teardown.
- Not suitable for production due to lack of durability.

## Cross-References
- [[IOrchestrationStore]] — the interface this class implements.
- [[PlanState]] — data model for stored plans.
- [[TaskSpec]] — data model for stored task specifications.
- [[TaskResult]] — data model for stored task results.
- [[IOrchestrationStore]] — other potential implementations (e.g., database-backed).

## Source Files
- `AGXIOME.Orchestration/Infrastructure/InMemoryOrchestrationStore.cs` (ingested 2026-05-14)