---
title: IAgentSpawner
category: entity
tags: [interface, orchestration, sub-agent, lifecycle, cancellation]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# IAgentSpawner

## Summary
`IAgentSpawner` defines an agnostic contract for spawning, tracking, and cancelling sub-agent jobs. It decouples the orchestration layer from any specific execution environment (e.g., background workers, HTTP remotes, SignalR hubs, WASM sandboxes) by requiring implementors to provide opaque job IDs and lifecycle methods. This enables a flexible, pluggable sub-agent architecture within the AGXIOMA framework.

## Key Details
- **Namespace**: `AGXIOME.Orchestration.Abstractions`
- **Purpose**: Sub-agent factory and lifecycle management
- **Method `SpawnAsync(TaskSpec spec, CancellationToken ct)`**: Starts a sub-agent from a given task specification. Returns an opaque, implementation-specific job ID.
- **Method `CancelAsync(string jobId, CancellationToken ct)`**: Requests cancellation of a running or queued sub-agent job identified by the opaque job ID.
- **Method `IsActiveAsync(string jobId, CancellationToken ct)`**: Returns `true` if the job is still in a non-terminal state (e.g., queued, running, retrying).
- **Opaque Job ID**: Callers must not assume any format or structure of the returned job ID.
- **Implementations**: Can be built using `BackgroundRunWorker`, HTTP remoting, SignalR, WASM sandbox, or any other execution model.
- **Cancellation Support**: The `CancelAsync` method is a best-effort cancellation; implementors should handle graceful or forced termination as appropriate.

## Cross-References
- [[TaskSpec]] — The specification type passed to `SpawnAsync` that defines the sub‑agent’s work.
- [[entities/iagentworker]] — Another interface that may be used in conjunction with spawning lifecycle management.

## Source Files
- `AGXIOME.Orchestration/Abstractions/IAgentSpawner.cs` (ingested 2026-05-14)