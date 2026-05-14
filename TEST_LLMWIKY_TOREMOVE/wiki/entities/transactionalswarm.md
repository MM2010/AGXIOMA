---
title: TransactionalSwarm
category: entity
tags: [transactional-swarm, outbox-pattern, agent-pipeline, crash-recovery]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# TransactionalSwarm

## Summary
TransactionalSwarm implements an outbox pattern for AI agent pipelines, ensuring atomic journaling of every agent run and tool call before execution. This provides deterministic crash recovery and replay capability — analogous to database transaction logs. It is a unique capability not found in other agent frameworks (Swarms, LangChain, AutoGen, CrewAI).

## Key Details
- Every agent execution is preceded by writing an `OutboxEntry` to a pluggable `ICheckpointStore` (default: in-memory `InMemoryCheckpointStore`).
- Supports `Strict` (commit before execute) and `Optimistic` (commit on success) commit policies.
- On startup, the swarm can detect an existing checkpoint and resume from the last committed agent index (`ReplayMode`).
- `OutboxEntry` records full serialised state, including task, agent ID, status, output, error, and timestamps.
- Immutable audit log: entries are write-once; the full log can be retrieved per run.
- Provides `OnCheckpoint` and `OnReplay` callbacks for monitoring or custom side-effects.
- If `AbortOnAgentFailure` is true, the run halts immediately on agent failure; otherwise continues.
- Run completion is explicitly marked via `MarkRunCompleteAsync`.

## Cross-References
- [[IAgxiomeAgent]] — agents orchestrated by the swarm
- [[TransactionalResult]] — result object produced by `RunAsync`
- [[OutboxEntry]] — core journal record type
- [[ICheckpointStore]] — interface for persistent checkpoint storage

## Source Files
- `AGXIOME.Core/Patterns/TransactionalSwarm.cs` (ingested 2026-05-14)