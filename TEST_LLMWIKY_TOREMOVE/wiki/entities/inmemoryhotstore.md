---
title: InMemoryHotStore
category: entity
tags: [in-memory, hotstore, caching, pub-sub, testing, orchestration, infrastructure]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# InMemoryHotStore

## Summary
In-memory implementation of `IHotStore` backed by `ConcurrentDictionary` with sliding TTL expiration. Provides key-value storage, set operations, and pub/sub channels via per-channel `Channel<string>` instances. Designed for unit tests and local development — no persistence across process restarts.

## Key Details
- Uses `ConcurrentDictionary<string, Entry>` where `Entry` stores value and optional expiration timestamp.
- `SetAsync` supports optional `TimeSpan` TTL; expired entries are lazily removed on read or existence check.
- `GetAsync` and `ExistsAsync` automatically remove expired entries and return `null`/`false`.
- `IncrementAsync` uses a spin-retry loop (no external lock) for atomic long increment.
- Set operations (`SetAddAsync`, `SetMembersAsync`) backed by `ConcurrentDictionary<string, ConcurrentDictionary<string, byte>>`.
- Pub/sub: `SubscribeAsync` returns `IAsyncEnumerable<string>`; each subscriber gets an unbounded `Channel<string>`; `PublishAsync` writes to all channels in the channel's bag.
- `Clear()` removes all stored data for test isolation.
- Thread-safe; intended for single-process environments only.

## Cross-References
- [[IHotStore]] — interface contract implemented by this class
- [[RedisHotStore]] — persistent alternative implementation

## Source Files
- `AGXIOME.Orchestration/Infrastructure/InMemoryHotStore.cs` (ingested 2026-05-14)