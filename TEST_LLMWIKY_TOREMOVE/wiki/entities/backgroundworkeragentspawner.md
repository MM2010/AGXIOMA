---
title: BackgroundWorkerAgentSpawner
category: entity
tags: [agent-spawner, background-worker, orchestration, dispatcher]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# BackgroundWorkerAgentSpawner

## Summary
Implements the [[IAgentSpawner]] interface by delegating agent execution to the core `BackgroundRunWorker` infrastructure. Translates a [[TaskSpec]] into a [[RunJob]] and enqueues it for background processing, bridging the orchestration layer with the execution engine without direct coupling. Returns the job identifier assigned by the worker, enabling subsequent cancellation and status checks.

## Key Details
- Implements `IAgentSpawner` using `IBackgroundRunWorker`.
- `SpawnAsync` creates a `RunJob` populated with task metadata (task ID, plan ID, step index, temperature, max tokens, etc.) and a prompt composed of the task description and optional prior context.
- The job ID returned is a new GUID string (N format), matching the identifier used by the background worker.
- `CancelAsync` calls `_worker.Cancel(jobId)` to terminate the running job.
- `IsActiveAsync` returns `true` only when the job’s status is not `Completed`, `Failed`, or `Cancelled`.
- Uses `BackgroundJobType.RunExecution` as the job type and generates an agent ID of the form `sub-agent-{taskId:N}`.

## Cross-References
- [[IAgentSpawner]] — contract this class fulfills.
- [[IBackgroundRunWorker]] — the execution backbone that runs the spawned jobs.
- [[TaskSpec]] — the input specification consumed by `SpawnAsync`.
- [[RunJob]] — the job object constructed and enqueued.
- [[BackgroundJobType]] — job type constant `RunExecution` used for spawning.

## Source Files
- `AGXIOME.Orchestration/Dispatcher/BackgroundWorkerAgentSpawner.cs` (ingested 2026-05-14)