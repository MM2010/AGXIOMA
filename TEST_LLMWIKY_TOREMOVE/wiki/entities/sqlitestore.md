---
title: SqliteStore
category: entity
tags: [sqlite, storage, store, dapper, commandstore]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# SqliteStore

## Summary
The `SqliteCommandStore` is a lightweight, embedded SQLite-backed implementation of the `ICommandStore` interface using `Microsoft.Data.Sqlite` and Dapper. It automatically creates table schemas from entity types on first use (when `AutoCreateSchema` is enabled), and supports standard CRUD operations (`SaveAsync`, `UpdateAsync`, `DeleteAsync`). The store uses a per-call connection model and provides its own table naming convention (entity type name + "s").

## Key Details
- **Class**: `SqliteCommandStore` (sealed) in namespace `AGXIOME.Storage.Stores`
- **Options**: `SqliteStoreOptions` record requiring a `ConnectionString` and optional `AutoCreateSchema` flag (default `true`)
- **Interface**: Implements `ICommandStore` and `IDisposable` (dispose is a no-op because connections are created per call)
- **Provider Type**: `StorageProviderType.SQLite`
- **CRUD operations**: All methods are asynchronous with `CancellationToken` support
- **Schema generation**: `EnsureTableAsync<T>` maps CLR property types to SQLite types (INTEGER for int/long, REAL for float/double, TEXT otherwise) and creates a table named `<TypeName>s`
- **Insert/Update**: Uses `INSERT OR REPLACE` for upsert; `UpdateAsync` updates all columns except `Id`, keyed by `Id`
- **Dependencies**: Requires `Microsoft.Data.Sqlite` and `Dapper` NuGet packages

## Cross-References
- [[ICommandStore]] — Interface that `SqliteCommandStore` implements
- [[StorageProviderType]] — Enum used to identify the storage backend

## Source Files
- `AGXIOME.Storage/Stores/SqliteStore.cs` (ingested 2026-05-14)