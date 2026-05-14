---
title: WorkflowHistoryStore
category: entity
tags: [workflow, history, storage, persistence, in-memory, ICommandStore]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# WorkflowHistoryStore

## Summary
`WorkflowHistoryStore` is the default implementation of `IWorkflowHistoryStore` in the AGXIOME framework, responsible for persisting completed workflow execution runs, including per-step results and metadata. It combines an `ICommandStore` for durable storage with an in-memory index to enable fast lookups by run ID or workflow ID, making it suitable for single-process deployments. All step results and metadata are stored as JSON blobs to support schema evolution, and run IDs are expected to be server-generated (Guid.CreateVersion7) for security and ordering.

## Key Details
- **DTO**: `WorkflowHistoryEntry` — flat record containing `RunId`, `WorkflowId`, `Status` (uses `WorkflowStatus` enum), `StepResultsJson`, `MetadataJson`, timestamps, optional `SessionId`, `UserId`, and `SchemaVersion`.
- **Interface**: `IWorkflowHistoryStore` — exposes `AppendAsync`, `GetAsync`, `ListByWorkflowAsync` (reverse-chronological, with limit), `GetStepAsync` (extracts a single step result from a stored run), and `DeleteAsync`.
- **Implementation**: Backed by an injected `ICommandStore` for durable writes; maintains a `Dictionary<string, WorkflowHistoryEntry>` by `RunId` and a `Dictionary<string, List<string>>` by `WorkflowId` (newest-first) for in-memory lookups.
- **AppendAsync**: Persists entry via `ICommandStore.SaveAsync`, then updates both in-memory indexes (new run IDs inserted at position 0 for latest-first ordering).
- **GetStepAsync**: Deserializes `StepResultsJson` on demand using `System.Text.Json` (with `JsonStringEnumConverter`) and returns the first `StepResult` matching the given `stepName`. Returns `null` if run or step not found.
- **Security note**: Run IDs must be server-generated (e.g., `Guid.CreateVersion7`) to prevent tampering and ensure chronological ordering.
- **Helper**: Static method `FromResult` builds a `WorkflowHistoryEntry` from a `WorkflowResult`, timestamps, and optional session/user info.
- **Limitations**: In-memory indexes are not persisted; all history is lost on process restart unless the underlying `ICommandStore` provides its own recovery mechanism.

## Cross-References
- [[ICommandStore]] — backing store interface for durable persistence of history entries.
- [[WorkflowResult]] — source data used to construct `WorkflowHistoryEntry` via the `FromResult` helper.
- [[StepResult]] — per-step result type; serialized as JSON within `StepResultsJson`.
- [[WorkflowStatus]] — enum used for execution status (no magic strings).

## Source Files
- `AGXIOME.Core/Storage/WorkflowHistoryStore.cs` (ingested 2026-05-14)