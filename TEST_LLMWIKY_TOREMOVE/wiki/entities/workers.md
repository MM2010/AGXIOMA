---
title: Workers
category: entity
tags: [worker, background, IHostedService, channel, session, token]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Workers

## Summary
The Workers module implements three background services using the `IHostedService` pattern: `BackgroundRunWorker`, `SessionCleanupJob`, and `TokenRotationJob`. These handle asynchronous agent run processing with backpressure, periodic expired session cleanup, and scheduled JWT/ECDSA key rotation with audit logging.

## Key Details
- `BackgroundRunWorker` uses a bounded `Channel<RunCommand>` (capacity 256) with `DropWrite` full mode to provide backpressure under burst load; `TryEnqueue` returns false when channel is full, allowing upstream 503 responses.
- Processing loop reads from channel with `ReadAllAsync` and executes runs in `ProcessRunAsync` (currently a stub: 100 ms delay).
- `SessionCleanupJob` runs every 5 minutes (`CleanupInterval`) and calls `CleanupExpiredSessionsAsync` (stub) to purge expired sessions from in-memory and Redis stores.
- `TokenRotationJob` runs every 24 hours (`RotationInterval`), triggers `IKeyRotationService.ForceRotate()`, writes an audit entry via `AuditLogger`, and logs the new key ID.
- All workers handle `OperationCanceledException` for graceful shutdown and log non-PII errors/warnings.
- Production notes: Replace channel with Redis Streams for distributed fan-out; implement actual `IAgentRunner`, `ISessionRepository`, and `IKeyRotationService` dependencies.

## Cross-References
- [[entities/sessions]] — SessionCleanupJob interacts with session repository for expired session removal.
- [[entities/keys]] — TokenRotationJob rotates signing keys via `IKeyRotationService`.
- [[entities/audit]] — TokenRotationJob writes audit entries via `AuditLogger`.
- [[entities/runs]] — BackgroundRunWorker enqueues and processes agent run commands.

## Source Files
- `AGXIOME.Runtime/Workers/Workers.cs` (ingested 2026-05-14)