---
title: Orchestration Enums
category: entity
tags: [orchestration, enums, status, tasks, plans, step-execution, advance-decision]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Orchestration Enums

## Summary
Defines the core enumerations used by the AGXIOME orchestration engine to represent task and plan lifecycle states, step parallelism modes, and workflow advance decisions. These enums provide a strongly-typed contract for status tracking, execution flow control, and human-in-the-loop (HITL) interactions.

## Key Details
- **OrchestratorTaskStatus**: Tracks individual task state — values: `Pending`, `Running`, `Done`, `Failed`, `Skipped`, `Cancelled`.
- **OrchestratorPlanStatus**: Tracks overall plan lifecycle — values: `Decomposing`, `Running`, `Paused`, `WaitingForHITL`, `Completed`, `Failed`, `Cancelled`.
- **StepExecutionMode**: Controls how tasks in the same step index are executed — `Sequential` (one at a time), `Parallel` (all via `Task.WhenAll`), `RaceFirst` (first completion wins), `Quorum` (requires N/2+1 completions).
- **AdvanceDecision**: Guides plan continuation after evaluating step results — `Advance` (next step), `Complete` (emit summary), `Fail` (abort), `Retry` (re-execute failed tasks in the same step).
- `WaitingForHITL` is the only HITL-related value; it pauses the plan until external approval.
- `Quorum` mode is used for consensus-sensitive workflows where majority completion is required.

## Cross-References
- [[entities/orchestrator]] — Core orchestrator that consumes these enums for state management.
- [[entities/plan]] — Plan entity that uses `OrchestratorPlanStatus` and `StepExecutionMode`.
- [[entities/task]] — Task entity that uses `OrchestratorTaskStatus`.
- [[entities/advanceplantool]] — Tool that produces `AdvanceDecision` values.
- [[concepts/human-in-the-loop]] — Expands on HITL integration and `WaitingForHITL` behavior.

## Source Files
- `AGXIOME.Orchestration/Models/OrchestrationEnums.cs` (ingested 2026-05-14)