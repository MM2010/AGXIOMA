---
title: PlanState
category: entity
tags: [orchestration, plan-state, state-management]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# PlanState

## Summary
`PlanState` is the canonical, immutable record that represents the full state of an orchestration plan. It serves as the single source of truth—allowing complete reconstruction of plan context from scratch. It tracks plan identity, tasks, current execution step, completed results, and status, with computed helpers for step management.

## Key Details
- Sealed record type; all properties are `init`-only (immutable after construction).
- Required fields: `PlanId` (guid), `OriginalRequest` (string), `SessionId` (string).
- `Tasks` is an ordered list of `TaskSpec`, where tasks sharing the same `StepIndex` may execute in parallel.
- `CurrentStep` indicates the active step index; `Status` (e.g., `Decomposing`) reflects the orchestration lifecycle phase.
- `CompletedResults` maps task IDs to their results, written atomically by `AdvancePlanTool`.
- Computed helpers (no extra state):
  - `CurrentStepTasks` — retrieves tasks for the current step.
  - `IsCurrentStepComplete` — true when all current step tasks have results.
  - `MaxStepIndex` — highest step index among all tasks.
  - `IsLastStep` — true when current step is the final one.
- Timestamps (`CreatedAt`, `UpdatedAt`, `CompletedAt`) track lifetime; `FinalSummary` holds the ultimate output.

## Cross-References
- [[TaskSpec]] — defines each individual work item within the plan.
- [[OrchestratorPlanStatus]] — enumeration of possible plan statuses.
- [[AdvancePlanTool]] — tool responsible for advancing steps and recording completed results.

## Source Files
- `AGXIOME.Orchestration/Models/PlanState.cs` (ingested 2026-05-14)