---
title: Memcached and Hazelcast Stores
category: entity
tags: [storage, memcached, hazelcast, caching, agxioma]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Memcached and Hazelcast Stores

## Summary
The `MemcachedCommandStore` and `HazelcastCommandStore` are implementations of the `ICommandStore` interface within the AGXIOMA framework. They provide distributed caching or in-memory data storage for entities using Memcached (via simple ASCII TCP protocol or injectable delegate) and Hazelcast (via the Hazelcast.Net client with lazy-initialized `IHMap`). Both stores serialize entities as JSON and support create, update, and delete operations by entity ID.

## Key Details
- Both stores implement `ICommandStore` and are registered under `StorageProviderType.Memcached` and `StorageProviderType.Hazelcast` respectively.
- **MemcachedCommandStore**:
  - Uses `MemcachedStoreOptions` with `Host`, `Port`, `KeyPrefix`, and `ExpirationSeconds`.
  - Stores are keyed as `{prefix}:{type.Name}:{id}`.
  - Default set/delete uses raw TCP ASCII protocol (`set`/`delete` commands); delegates can be injected for production (e.g., Enyim.Caching).
  - The `SaveAsync` and `UpdateAsync` are identical; `DeleteAsync` deletes by key.
- **HazelcastCommandStore**:
  - Uses `HazelcastStoreOptions` with `ClusterName`, `NetworkAddresses`, and `MapName`.
  - Lazily initializes a Hazelcast client and an `IHMap<string, string>` on first operation, guarded by a `SemaphoreSlim`.
  - Implements `IAsyncDisposable` to clean up the client and semaphore.
  - Id retrieval from entity via `Id` property reflection (falls back to a new GUID).
- Both stores use `System.Text.Json` for serialization.
- Neither store implements a read/query method; they are write-only as per the `ICommandStore` contract (which defines `SaveAsync`, `UpdateAsync`, `DeleteAsync`).

## Cross-References
- [[ICommandStore]] — interface implemented by both stores
- [[StorageProviderType]] — enum used to identify provider type
- [[AGXIOME Storage Overview]] — context for all storage implementations

## Source Files
- `AGXIOME.Storage/Stores/MemcachedAndHazelcastStores.cs` (ingested 2026-05-14)