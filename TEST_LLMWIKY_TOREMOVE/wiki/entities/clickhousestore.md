---
title: ClickHouseStore
category: entity
tags: [clickhouse, storage, command store]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# ClickHouseStore

## Summary
The `ClickHouseCommandStore` is an implementation of the [[ICommandStore]] interface that persists entities to a ClickHouse database using the `ClickHouse.Client` ADO.NET library. It targets a ReplacingMergeTree engine and inserts rows as JSON via the HTTP interface, while updates and deletes are handled through ClickHouse’s `ALTER TABLE … UPDATE/DELETE` mutation syntax, reflecting ClickHouse’s append-only design.

## Key Details
- Implements `ICommandStore` with `StorageProviderType.ClickHouse`.
- Requires a `ClickHouseStoreOptions` record containing a connection string and target database name.
- **SaveAsync**: Serializes the entity to JSON and uses `INSERT INTO … FORMAT JSONEachRow` to append a row.
- **UpdateAsync**: Builds an `ALTER TABLE … UPDATE` statement by reflecting over public instance properties (excluding `Id`), generating SET clauses with escaped string values.
- **DeleteAsync**: Uses `ALTER TABLE … DELETE WHERE Id = …` to remove rows by primary key.
- Table naming convention: entity type name lowered + "s" (e.g., `User` → `users`).
- Id resolution: reads the `Id` property of the entity via reflection.
- All operations are asynchronous with support for cancellation.
- Intended for use with ClickHouse’s ReplacingMergeTree engine (assumed by design).

## Cross-References
- [[ICommandStore]] — the interface this store implements
- [[AGXIOME.Shared.Interfaces]] — namespace containing the store interface
- [[AGXIOME.Shared.Models]] — namespace for shared entity models

## Source Files
- `AGXIOME.Storage/Stores/ClickHouseStore.cs` (ingested 2026-05-14)