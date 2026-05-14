---
title: WorkflowHitlStore
category: entity
tags: [storage, workflow, hitl, pause, resume]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# WorkflowHitlStore

## Summary
The `WorkflowHitlStore` provides a typed storage contract (`IWorkflowHitlStore`) and a default implementation for persisting and managing Human-In-The-Loop (HITL) pause/resume checkpoints in workflow executions. It enables workflows to await human approval or input by recording pause checkpoints, tracking their resolution, and supporting expiry. The implementation is backed by `ICommandStore` and an in-memory index, suitable for single-process deployments.

## Key Details
- **Record `WorkflowPauseRecord`**: Serializable checkpoint capturing run ID, workflow ID, step name, requirement type, status (`Pending`, `Approved`, `Rejected`, `Expired`), payload JSON, resolution JSON, resolver ID, timestamps, and schema version.
- **Record `HitlResolution`**: Input for resolving a checkpoint, containing approval flag, notes, optional input value, and pseudonymized resolver ID.
- **Interface `IWorkflowHitlStore`**:
  - `PauseAsync`: Persists a new checkpoint with `Pending` status, optional expiry, and serializes the payload.
  - `ResumeAsync`: Records resolution (approve/reject) and updates the checkpoint status. Throws `KeyNotFoundException` if checkpoint missing or `InvalidOperationException` if not pending.
  - `GetAsync`: Retrieves a checkpoint by ID (returns null if not found).
  - `GetPendingAsync`: Lists all pending checkpoints for a given run ID.
  - `ExpireAsync`: Marks all checkpoints past their `ExpiresAt` as expired.
- **Concurrency**: In-memory dictionaries (`_byId` and `_byRun`) are used for fast indexing; no thread-safe guards shown (intended for single-process scenarios).
- **Security**: Checkpoint IDs are server-generated via `Guid.CreateVersion7`. Resolution payloads store resolver IDs in pseudonymized form (upstream handling required). Authorization is assumed to be handled before calling `ResumeAsync`.
- **Design notes**: Uses `ICommandStore` for persistence. JSON serialization is configured with `JsonStringEnumConverter` and null-value omission.

## Cross-References
- [[ICommandStore]] — Backing store for persisting checkpoint records.
- [[HITLStatus]] — Status enum used by pause records.
- [[HITLRequirementType]] — Enum for the type of human interaction required.

## Source Files
- `AGXIOME.Core/Storage/WorkflowHitlStore.cs` (ingested 2026-05-14)