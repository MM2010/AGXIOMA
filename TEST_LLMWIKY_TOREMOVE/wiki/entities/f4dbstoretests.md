---
title: F4DbStoreTests (DB-01..DB-25)
category: entity
tags: [tests, database, storage, provider, validation]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# F4DbStoreTests (DB-01..DB-25)

## Summary
The F4DbStoreTests class validates construction, provider-type identity, null-guard, and basic round-trip operations for all database store implementations in the AGXIOMA framework. It covers SQLite, SQL Server, SQL Server LocalDB, Neon, Supabase, and other stores using in-memory or temp-file databases, mock delegates, and expected failure patterns. These tests do not require a live server; they exercise the store surface area for correctness and robustness.

## Key Details
- Tests are organized by store type: SQLite (DB-01), SQL Server (DB-02), SQL Server LocalDB (DB-03), Neon (DB-04), Supabase (DB-05), Citus (DB-06), Redis (DB-07), MongoDB (DB-08), MariaDB (DB-09), MySQL (DB-10), PostgreSQL (DB-11), CockroachDB (DB-12), TiDB (DB-13), YugabyteDB (DB-14), Elasticsearch (DB-15), DynamoDB (DB-16), CosmosDB (DB-17), Firestore (DB-18), Bigtable (DB-19), and others.
- Every store class is instantiated with `StoreOptions` and its `ProviderType` property is verified against the expected enum.
- Constructor with `null` options always throws `ArgumentNullException`.
- Round-trip tests (`SaveAsync` + `DeleteAsync`, `UpdateAsync`) use temporary SQLite files or in-memory databases to avoid external dependencies.
- Tests that require a live server (e.g., Neon, Supabase) are marked `[Fact(Skip = "...")]` and are not executed in CI.
- Dispose is tested for idempotency: calling `Dispose` twice does not throw.
- Invalid connection strings are used to verify that methods throw `Exception` (or derived) when the target is unreachable, with a cancellation timeout of 3 seconds.

## Cross-References
- [[StorageProviderType]] — enum validated in every store test
- [[SqliteStoreOptions]] — options class used in SQLite tests
- [[SqlServerStoreOptions]] — options class for SQL Server tests
- [[NeonStoreOptions]] — options class for Neon tests
- [[SupabaseStoreOptions]] — options class for Supabase tests

## Source Files
- `AGXIOME.Tests/F4DbStoreTests.cs` (ingested 2026-05-14)