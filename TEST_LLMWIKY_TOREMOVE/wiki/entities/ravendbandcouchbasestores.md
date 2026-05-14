---
title: RavenDB and Couchbase Stores
category: entity
tags: [store, persistence, RavenDB, Couchbase, ICommandStore]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# RavenDB and Couchbase Stores

## Summary
The RavenDbCommandStore and CouchbaseCommandStore are two concrete implementations of the [[ICommandStore]] interface within the AGXIOME framework. They provide asynchronous key-value persistence operations (Save, Update, Delete) for command entities, targeting RavenDB (using `Raven.Client.Documents`) and Couchbase (using `CouchbaseNetClient`). Both stores handle entity identification via an `Id` property and support optional authentication (certificate for RavenDB, username/password for Couchbase).

## Key Details
- Both stores implement `ICommandStore` and expose a `StorageProviderType` property (`RavenDB` / `Couchbase`).
- RavenDbCommandStore operates on an `IDocumentStore` instance configured with server URLs, database name, and optional certificate.
- CouchbaseCommandStore lazily connects to a Couchbase cluster via `Cluster.ConnectAsync` and caches the collection reference using a `SemaphoreSlim` for thread-safe initialization.
- `SaveAsync` and `UpdateAsync` use `UpsertAsync` (Couchbase) or `StoreAsync` + `SaveChangesAsync` (RavenDB); `DeleteAsync` uses `RemoveAsync` or `session.Delete`.
- Entity keys are derived from a public `Id` property via reflection; if absent, a new GUID is generated.
- RavenDbCommandStore implements `IDisposable`; CouchbaseCommandStore implements `IAsyncDisposable` for proper resource cleanup.
- Both stores accept cancellable tokens.

## Cross-References
- [[ICommandStore]] — the interface both stores implement
- [[StorageProviderType]] — enum indicating which backend is active
- [[AGXIOME.Storage]] — namespace containing the stores

## Source Files
- `AGXIOME.Storage/Stores/RavenDbAndCouchbaseStores.cs` (ingested 2026-05-14)