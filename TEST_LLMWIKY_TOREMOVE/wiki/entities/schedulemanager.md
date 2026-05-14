---
title: ScheduleManager
category: entity
tags: [scheduling, workflow, in-memory]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# ScheduleManager

## Summary
An in-memory implementation of the `IWorkflowScheduler` interface that manages workflow schedules using concurrent dictionaries. It provides CRUD operations for schedules, immediate execution triggers, and run history tracking, but lacks persistence — ideal for testing, prototyping, or environments where schedule state does not need to survive restarts.

## Key Details
- Implements `IWorkflowScheduler` entirely in memory via `ConcurrentDictionary<string, Schedule>` and `ConcurrentDictionary<string, List<ScheduleRun>>`.
- `CreateScheduleAsync` generates a new GUID for the schedule ID and stores the `Schedule` record.
- `UpdateScheduleAsync` uses the `with` expression to update only the provided fields (cron expression and/or enabled status) on the existing immutable `Schedule`.
- `DeleteScheduleAsync` removes the schedule from the dictionary using `TryRemove`.
- `TriggerAsync` is a no‑op placeholder — actual execution logic is not implemented.
- `GetRunHistoryAsync` returns the last `limit` entries (default 20) using `TakeLast` on the run list.
- No persistence; all data is lost on process restart.
- Thread-safe due to `ConcurrentDictionary` usage, but not distributed‑safe.

## Cross-References
- [[WorkflowScheduler]] — interface contract this class fulfills
- [[Schedule]] — the data model for a schedule definition
- [[ScheduleRun]] — the data model for an individual run record
- [[Concepts/WorkflowScheduling]] — overview of scheduling concepts in AGXIOME

## Source Files
- `AGXIOME.Core/Scheduling/ScheduleManager.cs` (ingested 2026-05-14)