---
title: TeamStorageBridge
category: entity
tags: [storage, team, bridge, persistence, snapshot]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# TeamStorageBridge

## Summary
The `TeamStorageBridge` is a typed persistence bridge for team snapshots, mirroring the Agent Storage Bridge pattern (SS-02). It encapsulates serialization, upsert, load, delete, list, and existence check operations over any `ICommandStore` backend. Two implementations are provided: an in-memory–indexed variant for single-process deployments and a query-capable variant (`QueryableTeamStorageBridge`) for multi-process or stateless environments. Team IDs are server-generated using `Guid.CreateVersion7` to prevent client-controlled key injection.

## Key Details
- **Snapshot DTO**: `TeamSnapshot` records team identity, topology, mode, member `AgentConfig`s (including default tools), snapshot timestamp, schema version, and optional extra properties.
- **Storage DTO**: `TeamStorageEntry` is a flat record containing the serialized snapshot JSON plus metadata (name, user ID, timestamps, tags).
- **Interface `ITeamStorageBridge`** defines six methods: `SaveAsync` (upsert), `LoadAsync`, `DeleteAsync`, `ListAsync`, `ExistsAsync`.
- **`TeamStorageBridge`** uses an in-memory `Dictionary<string, TeamStorageEntry>` index for O(1) list/exists; relies on `ICommandStore` for actual persistence.
- **`QueryableTeamStorageBridge`** (truncated in source) extends the pattern with `IQueryStore<TeamStorageEntry>` for persistent queries across processes.
- Security: all team IDs must be generated server-side (`Guid.CreateVersion7`); raw client-controlled IDs are never used as canonical store keys.
- Tags are stored as comma-separated strings for lightweight filtering.

## Cross-References
- [[AgentStorageBridge]] — source pattern (SS-02) that this bridge mirrors; shares `ICommandStore` contract.
- [[ICommandStore]] — underlying storage interface used for CRUD operations.
- [[TeamSnapshot]] — the serializable DTO persisted by this bridge.
- [[TeamStorageEntry]] — flat storage wrapper used by both bridge implementations.
- [[AgentConfig]] — configuration object stored per team member within a snapshot.

## Source Files
- `AGXIOME.Core/Storage/TeamStorageBridge.cs` (ingested 2026-05-14)