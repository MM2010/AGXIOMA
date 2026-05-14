---
title: IOrchestrationStore
category: entity
tags: [orchestration, storage, interface, persistence, abstraction]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# IOrchestrationStore

## Summary
The `IOrchestrationStore` interface defines the canonical persistent storage contract for plans, task specifications, and results within the AGXIOME orchestration framework. Implementations are expected for Postgres, SQLite, MongoDB, CosmosDB, DynamoDB, and other backends. A `null` return value is used for not-found entities; no exceptions are thrown for missing records. The interface includes atomic operations for step advancement and crash recovery querying.

## Key Details
- Defines methods for CRUD operations on three entity types: **Plans**, **TaskSpecs**, and **TaskResults**.
- `SavePlanAsync`, `GetPlanAsync`, `UpdatePlanStatusAsync` manage plan lifecycle.
- `AdvancePlanStepAsync` atomically increments `CurrentStep` and merges provided results in a single write, preserving audit trail.
- `GetActivePlansAsync` returns all plans with `Running` or `Paused` status, intended for crash recovery.
- `SaveTaskSpecAsync`, `GetTaskSpecAsync`, `UpdateTaskStatusAsync` for individual task specifications.
- `SaveResultAsync`, `GetResultAsync`, `GetPlanResultsAsync` for storing and retrieving execution results.
- Null returns are the contract for missing entities — no exceptions, simplifying callers.
- Methods accept an optional `CancellationToken` for cooperative cancellation.

## Cross-References
- [[PlanState]] — the data model for orchestration plans.
- [[TaskSpec]] — the data model for task specifications.
- [[TaskResult]] — the data model for task execution results.
- [[OrchestratorPlanStatus]] — enumeration for plan statuses.
- [[OrchestratorTaskStatus]] — enumeration for task statuses.

## Source Files
- `AGXIOME.Orchestration/Abstractions/IOrchestrationStore.cs` (ingested 2026-05-14)