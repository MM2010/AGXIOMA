---
title: BackgroundRunWorker
category: entity
tags: [background worker, execution, agent, thread-safe, state streaming, in-process]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# BackgroundRunWorker

## Summary

`BackgroundRunWorker` is the in-process implementation of `IBackgroundRunWorker` that executes agent runs on a persistent background loop. It supports polling, checkpoints, and SSE-style reconnect semantics. Thread-safe and stateless toward the caller, it manages job state via immutable `RunJobState` records and streams updates through per-job unbounded channels. Designed for single-node deployments; for multi-node production, a distributed worker backed by Redis streams is recommended.

## Key Details

- Implements `IBackgroundRunWorker` contract, which defines `Enqueue`, `GetState`, `WatchAsync`, `Cancel`, and `ListJobs`.
- Uses `ConcurrentDictionary<string, RunJobState>` for state storage and `ConcurrentDictionary<string, Channel<RunJobState>>` for streaming.
- Each enqueued job starts in `Queued` status, transitions through `Running`, then to a terminal state (`Completed`, `Failed`, `Cancelled`).
- `WatchAsync` returns an `IAsyncEnumerable<RunJobState>` that yields states until terminal or cancellation — reconnect-safe by allowing caller to supply last seen status.
- `Cancel` triggers a `CancellationTokenSource` associated with the job; `ExecuteAsync` catches `OperationCanceledException` to mark `Cancelled`.
- Jobs are executed via `IAgxiomeAgent.RunAsync(AgentRequest, CancellationToken)`.
- `RunJobState` is immutable; new instances emitted on every status change.
- `IDisposable` implementation disposes all `CancellationTokenSource` instances.
- For production with multiple nodes, replace with a distributed worker (e.g., backed by Redis streams).

## Cross-References

- [[AgxiomeAgent]] — the agent interface used to execute runs
- [[AgentRequest]] — the request model passed to the agent
- [[BackgroundJobStatus]] — status enum values used in state transitions
- [[RunJob]] — the job descriptor record
- [[RunJobState]] — the immutable state snapshot record
- [[IBackgroundRunWorker]] — the contract interface

## Source Files

- `AGXIOME.Core/Execution/BackgroundRunWorker.cs` (ingested 2026-05-14)