---
title: CancellationManager
category: entity
tags: [cancellation, token, thread-safe, memory]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# CancellationManager

## Summary
Thread-safe, in-memory registry of cancellation tokens keyed by run session IDs. Provides centralised cancel, remove, and active-state queries. Designed for single‑process use; can be extended for distributed scenarios (e.g., via Redis Pub/Sub) by propagating cancellation signals after local cancellation.

## Key Details
- Implements `ICancellationManager` and `IDisposable`.
- Registration (`Register(runId)`) replaces any existing token for the same run ID, disposing the previous `CancellationTokenSource`.
- `Cancel(runId)` returns `false` if the run ID is unknown or already cancelled.
- `CancelAll()` cancels all registered tokens, skipping any already cancelled.
- `Remove(runId)` removes the registration and disposes the token source.
- `IsActive(runId)` checks both existence and non‑cancelled state.
- `ActiveRunIds` returns a snapshot of run IDs whose tokens are still active.
- All operations are backed by a `ConcurrentDictionary<string, CancellationTokenSource>`.

## Cross-References
- [[RunSession]] — cancellation tokens are typically registered per run session in the execution pipeline.
- [[DistributedExecution]] — the comment suggests extending with Redis Pub/Sub for distributed cancellation signals.

## Source Files
- `AGXIOME.Core/Execution/CancellationManager.cs` (ingested 2026-05-14)