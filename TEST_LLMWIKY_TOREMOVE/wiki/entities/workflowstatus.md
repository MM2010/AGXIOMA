---
title: WorkflowStatus
category: entity
tags: [workflow, status, enum, lifecycle]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# WorkflowStatus

## Summary
The `WorkflowStatus` enum defines the lifecycle states of a workflow within the AGXIOME framework. It captures the progression from creation through execution, possible interruption, and terminal outcomes, enabling consistent state management and transitions across agents and pipelines.

## Key Details
- Enum values: `Created`, `Running`, `Paused`, `Completed`, `Failed`, `Cancelled`.
- `Created` — initial state after workflow instantiation.
- `Running` — actively executing steps or sub-workflows.
- `Paused` — execution suspended, awaiting external input or condition.
- `Completed` — all steps finished successfully.
- `Failed` — encountered an unrecoverable error.
- `Cancelled` — manually or programmatically terminated before completion.
- Used across the framework for status tracking, UI display, and conditional logic.
- Belongs to namespace `AGXIOME.Shared.Models`.

## Cross-References
- [[entities/workflow]] — a workflow object is the primary consumer of this status.
- [[concepts/workflow-lifecycle]] — describes the state machine and transitions.
- [[entities/agent]] — agents may query or update workflow status during execution.

## Source Files
- `AGXIOME.Shared/Models/WorkflowStatus.cs` (ingested 2026-05-14)