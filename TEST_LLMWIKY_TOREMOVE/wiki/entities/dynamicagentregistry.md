---
title: DynamicAgentRegistry
category: entity
tags: [thread-safe, in-memory, registry, agent-catalog]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# DynamicAgentRegistry

## Summary
The `DynamicAgentRegistry` is a thread‑safe, in‑memory catalog of discovered dynamic agents, keyed by user ID and agent ID. It provides register, query, update, and remove operations without any persistence (disk storage is handled separately by `AgentCacheStore`). The primary data structure is a `ConcurrentDictionary` keyed as `"{userId}::{agentId}"`.

## Key Details
- **Thread‑safe**: Uses `ConcurrentDictionary<string, DiscoveredAgentEntry>` internally.
- **Key format**: `"{userId}::{agentId}"` – a compound string ensures uniqueness per user.
- **Methods**:
  - `Register(userId, entry)` – add or update an agent entry.
  - `GetAll(userId)` – returns all agent entries for a user as a read‑only list.
  - `Get(userId, agentId)` – retrieves a single entry or `null`.
  - `GetStatus(userId, agentId)` – returns the `AgentEntryStatus` or `null`.
  - `UpdateStatus(userId, agentId, newStatus, error?)` – updates status in place; no‑op if agent not found.
  - `Remove(userId, agentId)` – deletes an entry; returns `true` if existed.
- **TotalCount** – property returning the total number of entries across all users.
- **No persistence**: State is volatile; disk operations are delegated to `AgentCacheStore`.
- **Sealed class** – not intended for inheritance.

## Cross-References
- [[DiscoveredAgentEntry]] – the entry type stored in the registry.
- [[AgentCacheStore]] – handles persistent storage of agent data.
- [[AgentEntryStatus]] – status enum used with `UpdateStatus`.

## Source Files
- `AGXIOME.DynamicRuntime/Registry/DynamicAgentRegistry.cs` (ingested 2026-05-14)