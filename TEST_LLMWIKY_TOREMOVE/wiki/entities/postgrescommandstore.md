---
title: PostgresCommandStore
category: entity
tags: [postgresql, command-store, dapper, storage]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# PostgresCommandStore

## Summary

`PostgresCommandStore` is a sealed implementation of `ICommandStore` that provides PostgreSQL-backed write operations for the AGXIOME agent framework. It uses Npgsql and Dapper to perform asynchronous save, update, and delete commands, with SQL statements dynamically generated from entity type metadata via reflection.

## Key Details

- Implements `ICommandStore` from `AGXIOME.Shared.Interfaces`.
- Accepts a PostgreSQL connection string in its constructor.
- `SaveAsync<T>` generates an `INSERT` SQL statement from public instance properties of `T`, lowercasing property names and using the entity type name as the table name (pluralized with "s"). All columns and parameters are quoted.
- `UpdateAsync<T>` generates an `UPDATE` statement excluding the `Id` property (which is used in the `WHERE` clause). Also lowercases and quotes identifiers.
- `DeleteAsync<T>` performs a simple `DELETE` by `id` parameter, with table name derived from `typeof(T).Name.ToLower() + "s"`.
- All methods enforce a command timeout of 30 seconds.
- SQL is dynamically generated each call; no caching or attribute-based mapping is used.
- Entity properties are expected to follow PascalCase, while database columns are lowercased (e.g., `ProductName` → `"productname"`).
- The store is designed for write-only persistence; queries are assumed to be handled separately.

## Cross-References

- [[ICommandStore]] — interface that this store implements
- [[PostgresQueryStore]] — complementary read store for PostgreSQL
- [[AGXIOME.Shared.Interfaces]] — namespace containing `ICommandStore`

## Source Files

- `AGXIOME.Storage/Stores/PostgresCommandStore.cs` (ingested 2026-05-14)