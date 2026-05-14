---
title: SingleStoreStore
category: entity
tags: [storage, store, SingleStore, MySQL, bulk insert, ICommandStore]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# SingleStoreStore

## Summary
`SingleStoreCommandStore` is a MySQL-compatible implementation of `ICommandStore` for SingleStore databases using the `MySqlConnector` library. It provides basic CRUD operations and high‑speed bulk insertion via batched multi-value `INSERT ... ON DUPLICATE KEY UPDATE` statements, making it suitable for high‑throughput event or command storage.

## Key Details
- Implements `AGXIOME.Shared.Interfaces.ICommandStore` for entity persistence.
- Configured via `SingleStoreOptions` which requires a `ConnectionString` and optional `BulkBatchSize` (default 500).
- Uses Dapper for parameterized queries and MySqlConnector for fully async, cross‑platform connectivity.
- `SaveAsync<T>` performs an idempotent upsert (`INSERT ... ON DUPLICATE KEY UPDATE`).
- `UpdateAsync<T>` issues a standard `UPDATE` statement.
- `DeleteAsync<T>` removes by `Id` column.
- `BulkSaveAsync<T>` splits a list of entities into configurable‑sized batches and executes a single multi‑value `INSERT` per batch.
- Automatically derives table names from `<T>.Name.ToLowerInvariant() + "s"` (e.g., `Order` → `orders`).
- Uses reflection on public instance properties to build column lists for SQL generation.
- Errors after 30 seconds (60 for bulk operations) via `commandTimeout`.

## Cross-References
- [[ICommandStore]] — the interface this store implements
- [[StorageProviderType]] — used to identify the provider type (SingleStore)

## Source Files
- `AGXIOME.Storage/Stores/SingleStoreStore.cs` (ingested 2026-05-14)