---
title: F15 TS07 Dispatcher Tests
category: entity
tags: [tests, dispatcher, BackgroundWorkerAgentSpawner, orchestration]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# F15 TS07 Dispatcher Tests

## Summary
This test suite (10 tests) validates the `BackgroundWorkerAgentSpawner` class, ensuring that `SpawnAsync` correctly enqueues jobs, returns job IDs, and includes spec details; that `CancelAsync` delegates cancellation; and that `IsActiveAsync` accurately reflects job state based on `BackgroundJobStatus` (running, completed, failed, cancelled) or unknown job IDs.

## Key Details
- `SpawnAsync` calls `worker.Enqueue` with a `RunJob` once.
- `SpawnAsync` returns the job ID returned by the worker.
- The spawned job’s prompt includes the `TaskSpec.Description`.
- The spawned job’s context contains the `taskId` key matching `TaskSpec.TaskId`.
- `CancelAsync` delegates to `worker.Cancel` with the given job ID.
- `IsActiveAsync` returns `true` when the job status is `BackgroundJobStatus.Running`.
- `IsActiveAsync` returns `false` for completed, failed, cancelled, or unknown jobs.
- All tests use `NSubstitute` mocks for `IBackgroundRunWorker`.
- Test fixture is `F15_TS07_DispatcherTests` in namespace `AGXIOME.Tests.F15`.
- Uses helper `MockWorker` to create pre-configured worker substitutes.

## Cross-References
- [[background-worker-agent-spawner]] — class under test
- [[ibackground-run-worker]] — interface being mocked
- [[run-job]] — model enqueued by spawner
- [[background-job-status]] — enum active/inactive states
- [[f15-agent-orchestration-tests]] — parent test group

## Source Files
- `AGXIOME.Tests/F15/F15_TS07_DispatcherTests.cs` (ingested 2026-05-14)