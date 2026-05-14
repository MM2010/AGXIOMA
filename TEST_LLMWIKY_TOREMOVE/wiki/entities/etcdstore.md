---
title: EtcdStore
category: entity
tags: [etcd, store, storage, command-store, distributed]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# EtcdStore

## Summary
The `EtcdCommandStore` class provides an implementation of `ICommandStore` backed by etcd, a distributed key-value store. It uses the `dotnet-etcd` client to persist JSON-serialized entities under hierarchical keys of the form `/{KeyPrefix}/{EntityType}/{Id}`. The store supports optional leases for TTL-based entries and is designed to be used within the AGXIOMA framework for command persistence, state storage, and leader election (exposed separately).

## Key Details
- Implements `ICommandStore` with `SaveAsync`, `UpdateAsync`, `DeleteAsync`.
- Uses `EtcdStoreOptions` record for configuration: Endpoint, KeyPrefix (default `/agxioma`), LeaseTtlSeconds (0 for no lease).
- Key format: `{KeyPrefix}/{typeof(T).Name}/{id}`.
- Entity ID is extracted from a public `Id` property; falls back to a new GUID if absent.
- Leases are granted conditionally if `LeaseTtlSeconds > 0`; the lease ID is attached to `PutRequest`.
- `Dispose` method disposes the underlying `EtcdClient`.
- Thread-unsafe by default (single client instance).
- Designed for eventual consistency and high availability via etcd's Raft consensus.
- Watch and leader election capabilities are available as separate methods outside the `ICommandStore` interface (not shown in source).

## Cross-References
- [[ICommandStore]] — the interface this store implements, used for command persistence in the AGXIOMA framework.
- [[InMemoryStore]] — an alternative in-memory store for testing or single-node scenarios.
- [[EtcdStoreOptions]] — the configuration record for this store.

## Source Files
- `AGXIOME.Storage/Stores/EtcdStore.cs` (ingested 2026-05-14)