---
title: PersistPlanTool
category: entity
tags: [orchestration, master, tool]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# PersistPlanTool

## Summary
`PersistPlanTool` is the second step in the six-step master orchestration sequence. It persists a decomposed `PlanState` and all its associated `TaskSpec` objects into both the hot store (fast, TTL-backed cache) and the orchestration store (canonical persistence). The tool also initialises hot‑store keys for step counter, plan status, and per‑task status, enabling efficient read access during subsequent orchestration steps.

## Key Details
- **Implements**: `IAgxiomeExecutableTool`
- **Dependencies**: `IHotStore` (hot cache) and `IOrchestrationStore` (canonical database)
- **Required parameter**: `plan` of type `PlanState`
- **Operations performed**:
  - Saves the plan and all its tasks to the orchestration store via `SavePlanAsync` / `SaveTaskSpecAsync`
  - Writes plan metadata, status, and step counter to hot store with a 72‑hour TTL
  - Writes each task’s spec and status to hot store with a 24‑hour TTL
  - Adds each task ID to a hot‑store set keyed by plan ID (`PlanTasks`)
- **Returns**: `ToolResult` with `persisted=true`, `planId`, `taskCount`, and `hotStoreTtlHours=72`
- **TTL rationale**: 72 hours for plan‑level data (entire orchestration lifetime), 24 hours for per‑task data (shorter cache window)

## Cross-References
- [[entities/planstate]] — the `PlanState` object required as input
- [[entities/hotkeys]] — the key patterns used for hot‑store access
- [[entities/orchestrationstore]] — interface for canonical persistence
- [[entities/iaxiomeexecutabletool]] — the tool interface contract

## Source Files
- `AGXIOME.Orchestration/Tools/Master/PersistPlanTool.cs` (ingested 2026-05-14)