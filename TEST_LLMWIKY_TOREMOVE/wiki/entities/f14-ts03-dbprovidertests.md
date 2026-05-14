---
title: F14 TS03 DbProviderTests
category: entity
tags: [tests, db-provider, concurrency, storage-provider]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# F14 TS03 DbProviderTests

## Summary
Extended coverage test suite (60 tests) for the AGXIOMA storage provider layer. Validates store APIs (`SaveAsync`, `UpdateAsync`, `DeleteAsync` – note no `GetAsync`), `ProviderType` contract on each store implementation, options validation (null checks, defaults), and concurrency behaviour. Covers SQLite, PostgreSQL, and JSON file stores, and ensures the `StorageProviderType` enum consistency and uniqueness.

## Key Details
- **60 tests** distributed across five sub-sections: SQLite extended (8), PostgreSQL contract (5), JSON file store (5), enum coverage (5), and concurrency (remaining).
- `ICommandStore` only exposes `SaveAsync`, `UpdateAsync`, `DeleteAsync`; `GetAsync` is **not** part of the contract.
- `SqliteCommandStore` tests include batch saves, delete-non-existent, save-then-update, save-then-delete, dispose, null options, and `AutoCreateSchema` default.
- `PostgresCommandStore` tests validate constructor acceptance, distinct instances, and enum definition for `StorageProviderType.PostgreSQL`.
- `JsonFileCommandStore` tests confirm `ProviderType`, save/delete operations, null options, and multiple saves.
- `StorageProviderType` enum tests ensure all 20+ values are unique and distinct from one another.
- Concurrency tests exercise concurrent saves and sequential cycles without corruption (SQLite via temporary database files).
- All store constructors throw `ArgumentNullException` when options are null (where applicable).

## Cross-References
- [[F14-ts02-dbrecords]] — Defines `F14DbRecord` model used in all tests.
- [[f14-ts01-commandstore-interface]] — Describes the `ICommandStore` contract.
- [[entities/f14-sqlite-commandstore]] — Implementation of SQLite store tested here.
- [[entities/f14-postgres-commandstore]] — Implementation of PostgreSQL store tested here.
- [[entities/f14-jsonfile-commandstore]] — Implementation of JSON file store tested here.

## Source Files
- `AGXIOME.Tests/F14/F14_TS03_DbProviderTests.cs` (ingested 2026-05-14)