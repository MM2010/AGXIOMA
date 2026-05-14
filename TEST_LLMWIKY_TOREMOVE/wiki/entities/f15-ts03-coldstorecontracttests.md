---
title: F15 TS03 Coldstorecontracttests
category: entity
tags: [cold-store, contract-tests, inmemory-orchestration-store, f15]
source_count: 10
created_at: 2026-05-14
updated_at: 2026-05-14
---

# F15 TS03 Coldstorecontracttests

## Summary
This test suite validates the contract of `InMemoryOrchestrationStore`, the in-memory implementation of the orchestration storage interface. It covers all required operations: plan CRUD, task status updates, result persistence, and active plan filtering. These 10 tests serve as a specification for any store implementation, ensuring consistent behavior across persistence backends.

## Key Details
- **Source file**: `AGXIOME.Tests/F15/F15_TS03_ColdStoreContractTests.cs`
- **Test framework**: xUnit (10 `[Fact]` methods)
- **Store under test**: `InMemoryOrchestrationStore` (fresh instance created via `Fresh()` helper)
- **Tests cover**:
  - `SaveAndGetPlan_Roundtrip`: save a plan and retrieve it by ID.
  - `GetPlan_Missing_ReturnsNull`: querying a non‑existent plan returns null.
  - `UpdatePlanStatus_ChangesStatus`: status updates persist (e.g., Running → Completed).
  - `AdvancePlanStep_IncrementsStep`: step index increments and is persisted.
  - `SaveAndGetTaskSpec_Roundtrip`: task spec CRUD roundtrip.
  - `UpdateTaskStatus_ChangesStatus`: task status updates persist.
  - `SaveAndGetResult_Roundtrip`: result persistence by task ID.
  - `GetResult_Missing_ReturnsNull`: missing result returns null.
  - `GetPlanResults_ReturnsAllSaved`: all results for a plan are returned.
  - `GetActivePlans_OnlyReturnsRunningAndPaused`: active plan filter excludes completed plans.
- **Helper methods**: `MakePlan`, `MakeSpec`, `MakeResult` create consistent test data.

## Cross-References
- [[entities/inmemory-orchestration-store]] — the implementation under test
- [[concepts/orchestration-store-contracts]] — the interface contract these tests verify
- [[entities/f15-ts01-orchestratorengine-tests]] — related F15 test suite

## Source Files
- `AGXIOME.Tests/F15/F15_TS03_ColdStoreContractTests.cs` (ingested 2026-05-14)