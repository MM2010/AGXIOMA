---
title: Npgsql Variant Stores
category: entity
tags: [npgsql, postgresql, stores, commandstore, dapper]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Npgsql Variant Stores

## Summary
Four PostgreSQL-wire‑protocol command stores sharing a common abstract base (`NpgsqlCommandStoreBase`) that uses Dapper and native PostgreSQL UPSERT (`ON CONFLICT DO UPDATE`). Each concrete store targets a distinct PostgreSQL‑compatible database (Neon Serverless, Supabase, CockroachDB, TimescaleDB) and expects only a different connection string. The base class provides `SaveAsync`, `UpdateAsync`, and `DeleteAsync`; individual stores may add provider‑specific concerns (e.g., Supabase RLS bypass, TimescaleDB explicit transactions).

## Key Details
- Abstract base `NpgsqlCommandStoreBase` implements `ICommandStore` via Npgsql + Dapper.
- All write operations use parameterised SQL with 30‑second command timeout.
- `SaveAsync` builds an `INSERT … ON CONFLICT("Id") DO UPDATE` UPSERT (PostgreSQL‑native).
- `UpdateAsync` builds `UPDATE … SET … WHERE "Id" = @Id` (omits Id from SET clause).
- `DeleteAsync` uses `DELETE FROM … WHERE "Id" = @Id`.
- Table name derived from entity type name + `"s"` (lower‑cased).
- Four concrete stores, each requiring an options record with a `ConnectionString`:
  - `NeonCommandStore` — no extra behaviour.
  - `SupabaseCommandStore` — overrides `SaveAsync` to optionally `SET LOCAL role = 'service_role'` to bypass Row Level Security.
  - `CockroachDbCommandStore` — no extra behaviour (uses default serialisable isolation).
  - `TimescaleDbCommandStore` — `UseExplicitTransaction` option (flag only; actual transaction wrapping not shown in snippet).
- Property provider (`StorageProviderType`) returns respective enum value.
- All classes are `public sealed` except the abstract base.

## Cross-References
- [[ICommandStore]] — the interface implemented by the base class.
- [[StorageProviderType]] — enum values returned by each store.
- [[Dapper]] — micro-ORM used for query execution.
- [[Npgsql]] — PostgreSQL ADO.NET driver used for all connections.
- [[NeonStoreOptions]] — configuration record for Neon.
- [[SupabaseStoreOptions]] — configuration record for Supabase.
- [[CockroachDbStoreOptions]] — configuration record for CockroachDB.
- [[TimescaleDbStoreOptions]] — configuration record for TimescaleDB.

## Source Files
- `AGXIOME.Storage/Stores/NpgsqlVariantStores.cs` (ingested 2026-05-14)