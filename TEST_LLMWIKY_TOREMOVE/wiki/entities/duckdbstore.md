---
title: DuckDB Store
category: entity
tags: [duckdb, storage, command-store]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# DuckDB Store

## Summary
The DuckDB Store provides an embedded analytical command store via DuckDB.NET, enabling persistent or in‑memory storage with automatic schema generation. It implements `ICommandStore` and uses upsert semantics (INSERT … ON CONFLICT) to simplify save operations, while supporting standard update and delete patterns.

## Key Details
- Implements `ICommandStore` and `IDisposable`.
- Options are configured via `DuckDbStoreOptions` record:
  - `DataSource`: file path for persistent storage or `":memory:"` for in‑process.
  - `AutoCreateSchema`: when `true`, automatically creates table on first `SaveAsync` (default: `true`).
- Table name derived from entity type name, lowercased and pluralized (e.g. `MyEntity` → `myentitys`).
- Automatic type mapping for C# properties: `int`/`long` → `BIGINT`, `double`/`float` → `DOUBLE`, `bool` → `BOOLEAN`, all others → `VARCHAR`.
- The `Id` property is declared **PRIMARY KEY** to enable upsert conflict resolution.
- `SaveAsync` uses `INSERT … ON CONFLICT(Id) DO UPDATE SET …` for atomic upsert.
- `Dispose` closes the underlying `DuckDBConnection`.
- Escape single quotes in string literals; complex types are serialized to JSON before storage.

## Cross-References
- [[ICommandStore]] — contract implemented by this store.
- [[StorageProviderType]] — enum value `DuckDB` returned by `ProviderType`.

## Source Files
- `AGXIOME.Storage/Stores/DuckDbStore.cs` (ingested 2026-05-14)