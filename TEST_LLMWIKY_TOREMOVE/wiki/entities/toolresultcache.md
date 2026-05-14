---
title: ToolResultCache
category: entity
tags: [caching, tools, performance]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# ToolResultCache

## Summary
`ToolResultCache` is the default in-memory, thread-safe implementation of `IToolResultCache`. It caches `ToolResult` instances using a deterministic SHA256 key derived from the tool name and a sorted JSON representation of parameters. Entries have a configurable TTL (default 5 minutes) and the cache enforces a maximum entry count (default 1000) with pluggable eviction policies. Background sweeps remove expired entries periodically. This entity is suitable for single-node deployments; multi-node scenarios should replace it with a distributed implementation (e.g., Redis).

## Key Details
- Implements `IToolResultCache` interface and `IDisposable`.
- Cache key: `SHA256("{toolName}|{sortedParamJson}")` – deterministic across identical inputs.
- Constructor accepts optional `defaultTtl`, `maxEntries`, `evictionPolicy`, and `sweepInterval`.
- Supports two eviction policies: `Lru` (oldest insertion first) and `TtlFirst` (closest to expiry first).
- Provides atomic statistics via `ToolCacheStats` record (hits, misses, entry count, hit rate).
- Statistics can be reset without clearing cached entries via `ResetStatsAsync`.
- Lazy eviction on read (expired entries removed when accessed); active sweep runs on a timer (default 60 seconds).
- Tool‑level invalidation maintains a `ConcurrentDictionary<string, ConcurrentBag<string>>` for O(n_tool) removal.

## Cross-References
- [[ToolResult]] — the object being cached, returned by tool executions.
- [[IToolResultCache]] — interface contract this entity fulfills.
- [[CacheEvictionPolicy]] — enum defining Lru and TtlFirst strategies.

## Source Files
- `AGXIOME.Core/Tools/ToolResultCache.cs` (ingested 2026-05-14)