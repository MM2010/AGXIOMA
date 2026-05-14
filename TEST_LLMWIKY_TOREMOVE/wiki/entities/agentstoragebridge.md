---
title: AgentStorageBridge
category: entity
tags: [storage, bridge, agent, snapshot, persistence]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AgentStorageBridge

## Summary
AgentStorageBridge is a typed persistence bridge for `AgentSnapshot` instances, exposing save, load, list, delete, and existence checks through any `ICommandStore` backend. It serializes snapshots to JSON blobs stored inside a flat `AgentStorageEntry` DTO, decoupling the agent model from storage schema evolution. Two implementations exist: a lightweight in-memory-indexed version for single‑process use, and a `QueryableAgentStorageBridge` backed by `IQueryStore<AgentStorageEntry>` for stateless/multi‑process deployments.

## Key Details
- Interface: `IAgentStorageBridge` — `SaveAsync`, `LoadAsync`, `DeleteAsync`, `ListAsync`, `ExistsAsync`
- DTO: `AgentStorageEntry` — stores `Id`, `Name`, `UserId`, `SnapshotJson`, `SchemaVersion`, `CreatedAt`, `UpdatedAt`, `Tags`
- Serialization: Uses `AgentSerializer` and `System.Text.Json` with `JsonStringEnumConverter` and null‑suppression
- In‑memory index maintained in `AgentStorageBridge` for O(1) `ListAsync` and `ExistsAsync`
- `QueryableAgentStorageBridge` delegates list/exists to a persistent `IQueryStore<AgentStorageEntry>`
- `SaveAsync` performs upsert (creates or replaces); `DeleteAsync` is no‑op if not found
- Agent IDs must be server‑generated (e.g., `Guid.CreateVersion7`); raw client IDs are never accepted as canonical keys
- Tags are stored as a comma‑separated string for lightweight filtering

## Cross-References
- [[AgentSnapshot]] — the domain model being persisted
- [[ICommandStore]] — the write‑side storage contract used by both bridge variants
- [[IQueryStore]] — the query‑side storage contract (used by `QueryableAgentStorageBridge`)
- [[AgentSerializer]] — responsible for deterministic snapshot ↔ JSON conversion
- [[StorageEntry]] — other storage DTO patterns in AGXIOME

## Source Files
- `AGXIOME.Core/Storage/AgentStorageBridge.cs` (ingested 2026-05-14)