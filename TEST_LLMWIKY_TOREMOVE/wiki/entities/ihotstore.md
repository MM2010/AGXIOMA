---
title: IHotStore
category: entity
tags: [interface, storage, caching, abstraction]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# IHotStore

## Summary
IHotStore is an agnostic contract for fast transient storage (e.g., Redis, Hazelcast, Memcached, InMemory). It exposes only string-based operations; callers own serialization. In addition to key-value CRUD, it provides atomic counter increment, set operations, and optional pub/sub channels for lightweight messaging.

## Key Details
- Defined in `AGXIOME.Orchestration.Abstractions` namespace.
- All values are serialised strings – caller must handle serialization/deserialization.
- Core CRUD: `SetAsync`, `GetAsync`, `DeleteAsync`, `ExistsAsync` with optional TTL and CancellationToken.
- Atomic counter: `IncrementAsync` returns new value; used for step indices.
- Set operations: `SetAddAsync` and `SetMembersAsync` for tracking task IDs per plan.
- Pub/Sub: `SubscribeAsync` returns `IAsyncEnumerable<string>`; `PublishAsync` sends messages. Implementations may throw `NotSupportedException` if pub/sub is not available.
- Designed to be backend-agnostic – can be backed by Redis, in-memory cache, Hazelcast, etc.

## Cross-References
- [[IPlanStore]] – complementary persistent storage contract.
- [[OrchestrationEngine]] – consumes IHotStore for transient state.

## Source Files
- `AGXIOME.Orchestration/Abstractions/IHotStore.cs` (ingested 2026-05-14)