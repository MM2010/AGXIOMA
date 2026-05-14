---
title: RedisSessionStore
category: entity
tags: [Redis, session store, StackExchange.Redis, storage, ICommandStore, TTL]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# RedisSessionStore

## Summary
RedisSessionStore is a Redis-backed implementation of `ICommandStore` using the StackExchange.Redis client. It serializes entities to JSON, stores them with a configurable key prefix and entity type, and supports optional TTL expiry. This store is designed for ephemeral session data where fast, distributed storage is needed.

## Key Details
- Implements `ICommandStore` and `IDisposable`.
- Uses `ConnectionMultiplexer` from StackExchange.Redis to connect and manage Redis connections.
- Key format: `{KeyPrefix}:{EntityTypeName}:{Id}`.
- Default key prefix is `"agxioma:session"`.
- Expiry is controlled via `RedisSessionStoreOptions.Expiry`; if null, entries never expire.
- `SaveAsync` and `UpdateAsync` both perform a `StringSet` (idempotent in Redis).
- `DeleteAsync` uses `KeyDeleteAsync`.
- Entity ID is read from an `Id` property; if missing, generates a new GUID.
- Serialization uses `System.Text.Json` with indentation disabled.
- Configuration is provided through the `RedisSessionStoreOptions` record.

## Cross-References
- [[ICommandStore]] — contract implemented by this store
- [[RedisSessionStoreOptions]] — configuration record for connection string, key prefix, and TTL
- [[StorageProviderType]] — enum that identifies the provider type as `RedisSession`
- [[AGXIOME.Storage.Stores]] — namespace containing all store implementations

## Source Files
- `AGXIOME.Storage/Stores/RedisSessionStore.cs` (ingested 2026-05-14)