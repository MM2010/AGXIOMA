---
title: F15 TS06 OrchestratorTests
category: entity
tags: [orchestrator, tests, events, cancellation, resume]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# F15 TS06 OrchestratorTests

## Summary
This test suite validates the core state management, event hierarchy, and lifecycle logic of the `PlanOrchestrator` and its supporting `PlanResumeService`. It covers default configuration, start/cancel/watch operations, event types, and resumption of active plans using in-memory stores and NSubstitute mocks. The suite contains 15 tests across four test classes.

## Key Details
- **`OrchestratorOptions` defaults** (3 tests): `MaxParallelTasks` is 4, `DefaultTaskTimeout` is 5 minutes, `EnableAutoResume` is `true`.
- **`PlanOrchestrator.StartAsync`** (5 tests): Returns non-empty GUIDs, distinct IDs for separate calls, `GetStateAsync` returns null for unknown plans, `CancelAsync` does not throw for unknown plans, `WatchAsync` yields no events when cancelled immediately.
- **`OrchestratorEvent` hierarchy** (4 tests): `PlanStartedEvent`, `PlanCompletedEvent` (carries summary), `TaskSpawnedEvent` (carries step index), `StepAdvancedEvent` (carries old and new step).
- **`PlanResumeService`** (3 tests): `StartAsync` with no active plans does not throw, `StopAsync` does not throw, `StartAsync` with an active plan calls `ResumeAsync` once.
- Uses `InMemoryOrchestrationStore`, `InMemoryHotStore`, and `InMemoryCompletionNotifier` as test doubles.

## Cross-References
- [[PlanOrchestrator]] — primary orchestrator tested here
- [[PlanResumeService]] — service for resuming interrupted plans
- [[OrchestratorOptions]] — configuration object validated in TS06-A

## Source Files
- `AGXIOME.Tests/F15/F15_TS06_OrchestratorTests.cs` (ingested 2026-05-14)