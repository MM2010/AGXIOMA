---
title: RedisCache
category: entity
tags: [cache, Redis, storage]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# RedisCache

## Summary
`RedisCache` is a thread-safe, sealed cache store backed by Redis, providing read-through and write-through semantics with configurable TTLs, pattern-based invalidation, and atomic increment operations. It uses `StackExchange.Redis` and JSON serialization to store arbitrary types.

## Key Details
- Connection is configured with `AbortOnConnectFail=false`, 5-second connect timeout, and 3-second sync timeout.
- Default TTL for `SetAsync` is 5 minutes if none specified.
- `GetAsync` deserializes stored JSON strings; returns `default` if key missing.
- `InvalidateAsync` deletes all keys matching a given Redis pattern (via `Keys` command on the server).
- `IncrementAsync` atomically increments a key and optionally sets an expiry.
- All methods are asynchronous (`Task<T>` / `Task`).
- Uses `System.Text.Json` for serialization.
- The class is `sealed` and not intended for inheritance.

## Cross-References
- [[Storage Concepts]] — general overview of storage patterns in AGXIOMA
- [[RedisConnection]] — configuration details for Redis connections

## Source Files
- `AGXIOME.Storage/Stores/RedisCache.cs` (ingested 2026-05-14)