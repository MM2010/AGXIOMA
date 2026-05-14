---
title: F15-TS01: Domain Model Tests
category: entity
tags: [test, domain-model, enumeration, record, orchestration]
source_count: 15
created_at: 2026-05-14
updated_at: 2026-05-14
---

# F15-TS01: Domain Model Tests

## Summary
Fifteen unit tests that validate the core domain models of the AGXIOME orchestration framework. The tests cover enumeration completeness and uniqueness, default values and immutability of the `TaskSpec` and `TaskResult` records, and the correctness of computed properties on `PlanState`. These tests ensure that the building blocks (status enums, step execution modes, advance decisions, task specification, task results, and plan state) behave as expected before higher-level orchestration logic is exercised.

## Key Details
- **Enumeration tests (5 tests):** All values defined for `OrchestratorTaskStatus` (Pending, Running, Done, Failed, Skipped, Cancelled), `OrchestratorPlanStatus` (Decomposing, Running, Paused, Completed, Failed, Cancelled), `StepExecutionMode` (Parallel, Sequential), and `AdvanceDecision` (Advance, Complete, Fail, Retry). Enum values are verified to have unique underlying integer values.
- **TaskSpec record tests (4 tests):** Default `Status` is `Pending`, default `Temperature` is `0.7f`, default `MaxTokens` is `4096`. Record `with` expressions produce a new instance without mutating the original.
- **TaskResult record tests (3 tests):** `Success=true` leaves `ErrorMessage` as `null`; `Success=false` can carry an error message. Default `RetryCount` is `0`.
- **PlanState computed properties tests (3 tests):** `CurrentStepTasks` returns only tasks matching `CurrentStep`; `MaxStepIndex` equals `Tasks.Count - 1`; `IsLastStep` is `true` when `CurrentStep == MaxStepIndex`.
- All tests reside in `AGXIOME.Tests/F15/F15_TS01_DomainModelTests.cs` and belong to the F15 test suite (domain model layer).

## Cross-References
- [[OrchestratorTaskStatus]] — enum validated for all values and uniqueness
- [[OrchestratorPlanStatus]] — enum validated for all values and uniqueness
- [[StepExecutionMode]] — enum includes Parallel and Sequential
- [[AdvanceDecision]] — enum with four distinct values
- [[TaskSpec]] — record defaults and `with` expression semantics
- [[TaskResult]] — record defaults and null vs. error message behavior
- [[PlanState]] — computed properties `CurrentStepTasks`, `MaxStepIndex`, `IsLastStep`

## Source Files
- `AGXIOME.Tests/F15/F15_TS01_DomainModelTests.cs` (ingested 2026-05-14)