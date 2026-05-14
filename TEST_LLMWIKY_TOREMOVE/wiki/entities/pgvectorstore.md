---
title: PgVectorStore
category: entity
tags: [pgvector, vector store, PostgreSQL, IVectorStore]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# PgVectorStore

## Summary
`PgVectorStore` is a concrete implementation of the [[IVectorStore]] interface backed by PostgreSQL with the pgvector extension. It stores vector embeddings as `vector` columns in schema-qualified tables, supports idempotent upserts (INSERT … ON CONFLICT DO UPDATE), and provides cosine-similarity search with optional minimum score filtering. All write operations are atomic and durable, and collection names are validated against a strict pattern to prevent SQL injection.

## Key Details
- Requires PostgreSQL 16+ with the `pgvector` extension enabled.
- Each collection maps to a table in the `public` schema; the table is created with columns `id` (TEXT PK), `vector` (vector(N)), `text_content` (TEXT), and `metadata` (JSONB).
- Collection names must match the regex `^[a-zA-Z_][a-zA-Z0-9_.]*$` (validated by `ValidateCollection`).
- `UpsertAsync` and `GetAsync` use parameterized queries via Dapper and Npgsql; `UpsertBatchAsync` currently performs sequential upserts (no COPY optimizations).
- `SearchAsync` computes cosine similarity as `1 - cosine_distance` and supports a `minScore` threshold and `topK` limit. Metadata filtering is not yet implemented.
- `ListCollectionsAsync` discovers tables in `public` schema that have a column of type `vector`.
- Vectors are formatted as `[0.10000000,0.20000000,…]` for pgvector and parsed back from the same representation.
- `EnsureCollectionAsync` idempotently creates the extension and the table; `DeleteCollectionAsync` drops the table.
- Command timeout set to 30 seconds for read/write operations.

## Cross-References
- [[IVectorStore]] — interface contract that `PgVectorStore` implements.
- [[VectorRecord]] — data transfer object used for upserts and retrieval.
- [[VectorDbProviderType]] — enum value `PgVector` returned by `ProviderType`.
- [[VectorSearchResult]] — result type returned by `SearchAsync`.

## Source Files
- `AGXIOME.Storage/VectorStores/PgVectorStore.cs` (ingested 2026-05-14)