---
title: Pgvectorvariants
category: entity
tags: [pgvector, npgsql, vector-store, storage-provider, postgresql]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Pgvectorvariants

## Summary
The `Pgvectorvariants` entity defines four concrete pgvector-based vector store implementations—`SupabasePgVectorStore`, `NeonPgVectorStore`, `TimescalePgVectorStore`, and `CockroachDbPgVectorStore`—each targeting a different cloud-managed PostgreSQL service that supports the pgvector extension. All share a common abstract base, `NpgsqlPgVectorBase`, which encapsulates the full Npgsql + pgvector logic, so the variant stores differ only in their `ProviderType` label and connection-string format. This pattern eliminates duplication while exposing provider-specific types for configuration and telemetry.

## Key Details
- Abstract base `NpgsqlPgVectorBase` implements `IVectorStore` using raw SQL, Dapper, and the Npgsql driver.
- Provider-specific stores inherit from the base and override `ProviderType` to return the appropriate `VectorDbProviderType` enum value.
- Supported variants: Supabase, Neon, Timescale, and CockroachDB—all expose standard PostgreSQL wire protocol with pgvector.
- Underlying SQL is identical across variants: CREATE TABLE, INSERT ... ON CONFLICT, cosine similarity search via `<=>` operator, JSONB metadata.
- `UpsertAsync` and `UpsertBatchAsync` use parameterized queries with JSON serialization for metadata.
- `SearchAsync` computes cosine similarity as `1 - (vector <=> @Vec)` and supports `topK`/`minScore` filtering.
- Collection names are validated against a strict regex to prevent SQL injection.
- Vector formatting uses invariant culture with eight decimal places.
- The base class also provides `EnsureCollectionAsync`, `DeleteCollectionAsync`, `ListCollectionsAsync`, `GetAsync`, and `DeleteAsync`.

## Cross-References
- [[IVectorStore]] — interface implemented by all pgvector variant stores.
- [[VectorDbProviderType]] — enum values `SupabasePgVector`, `NeonPgVector`, `TimescalePgVector`, `CockroachDbPgVector` correspond to each variant.
- [[PgVectorStore]] — canonical pgvector store implementation; the variants reuse its SQL and vector logic.

## Source Files
- `AGXIOME.Storage/VectorStores/PgVectorVariants.cs` (ingested 2026-05-14)