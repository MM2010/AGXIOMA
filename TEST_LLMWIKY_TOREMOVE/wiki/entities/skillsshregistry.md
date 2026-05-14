---
title: SkillsShRegistry
category: entity
tags: [skills, registry, persistence, user]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# SkillsShRegistry

## Summary
The `SkillsShRegistry` is a persistent, per-user catalog of imported skill definitions. It stores each user's skills in a JSON file (`~/.agxioma/skills/{userId}.json`), caches them in memory, and provides thread-safe operations for importing, updating status, and retrieving skill entries. This enables the framework to maintain an offline, durable record of skills that have been imported by each user, along with their current lifecycle status.

## Key Details
- Thread‑safe using `SemaphoreSlim(1,1)` for exclusive access to each user's skill list.
- Persistence path defaults to `~/.agxioma/skills/`; configurable via constructor.
- `ImportAsync` adds or updates a [[UserSkillEntry]] with initial status `Pending`.
- `SetStatusAsync` updates the status of an existing entry by skill ID.
- `GetAllAsync` returns a read‑only list of entries, caching after first load.
- Cache is updated immediately on write operations; reads from cache if available.
- JSON serialization uses `WriteIndented` for readability.
- Handles missing or corrupt files gracefully by returning an empty list.
- Supports `CancellationToken` in all public async methods.

## Cross-References
- [[SkillDefinition]] — the definition object stored per entry
- [[UserSkillEntry]] — the record type persisted for each skill
- [[SkillStatus]] — enumeration of possible statuses (e.g., Pending)
- [[SkillsRepository]] — higher‑level abstraction that may use this registry

## Source Files
- `AGXIOME.DynamicRuntime/Skills/SkillsShRegistry.cs` (ingested 2026-05-14)