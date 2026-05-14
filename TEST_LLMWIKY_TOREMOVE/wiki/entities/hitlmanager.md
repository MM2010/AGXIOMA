---
title: HITLManager
category: entity
tags: [HITL, Human-in-the-Loop, Manager, Orchestration]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# HITLManager

## Summary
`HITLManager` is a centralized, thread-safe manager for Human-in-the-Loop (HITL) requirements within the AGXIOME framework. It stores pending requirements in a concurrent dictionary, provides methods to create, retrieve, and resolve them, and optionally integrates with an orchestration layer via an injected `IHitlCompletionSource` bridge to resume paused tasks without polling.

## Key Details
- Sealed class; uses a `ConcurrentDictionary<string, HITLRequirement>` for thread-safe storage.
- Constructor accepts an optional `IHitlCompletionSource` for orchestration bridge integration.
- `CreateRequirement()` generates a new `HITLRequirement` with a unique ID, stores it, and returns it.
- `GetRequirement(id)` retrieves a requirement by ID; returns `null` if not found.
- `Resolve(id, approved, notes?)` updates the requirement status to `Approved` or `Rejected`. If an orchestration bridge is present and the requirement is associated with an orchestrated task/plan, it fire-and-forgets a notification to resume the paused task.
- `GetPendingForRun(runId)` returns all pending requirements for a specific run.
- HITL resolution is immutable: creates a new record via `with { Status = ... }` and uses `TryUpdate` for atomic replacement.
- Orchestration notification is asynchronous and non-blocking; failure does not affect HITL resolution.

## Cross-References
- [[HITLRequirement]] — the record type managed by this class
- [[IHitlCompletionSource]] — orchestration bridge interface for task resumption

## Source Files
- `AGXIOME.Core/HITL/HITLManager.cs` (ingested 2026-05-14)