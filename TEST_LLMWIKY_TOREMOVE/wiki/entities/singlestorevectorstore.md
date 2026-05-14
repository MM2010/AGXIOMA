---
title: SingleStoreVectorStore
category: entity
tags: [vector-store, singlestore, sql, dapper]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# SingleStoreVectorStore

## Summary
An implementation of [[IVectorStore]] that stores vector embeddings in SingleStore (MySQL‑wire compatible) tables. Vectors are serialised as JSON arrays and similarity search is performed in‑memory using cosine distance. The implementation is intended for rapid prototyping; production deployments should migrate to SingleStore’s native `VECTOR` column type and `DOT_PRODUCT()` function for efficient approximate search.

## Key Details
- Provider type: `VectorDbProviderType.SingleStoreVector`
- Uses `MySqlConnector` and Dapper for SQL access.
- Table schema: `id` (VARCHAR PK), `vector_json` (LONGTEXT), `text_content` (TEXT), `metadata` (JSON).
- Options class: `SingleStoreVectorOptions` requires a `ConnectionString` and optional `Dimensions` (default 1536).
- `EnsureCollectionAsync` creates the table if it does not exist.
- `UpsertAsync` and `UpsertBatchAsync` perform INSERT … ON DUPLICATE KEY UPDATE.
- `GetAsync`, `DeleteAsync`, `ListCollectionsAsync` are standard CRUD operations.
- `SearchAsync` does a brute‑force scan of all rows, computes cosine similarity, filters by `minScore`, and returns `topK` results. No server‑side vector index used.
- Vectors are serialised with `JsonSerializer` using camelCase naming and `AllowNamedFloatingPointLiterals`.
- Command timeouts: 30 seconds for single operations, 60 seconds for search.

## Cross-References
- [[IVectorStore]] — the interface this class implements.
- [[VectorRecord]] — record type used for upsert and retrieval.
- [[VectorSearchResult]] — result type returned by `SearchAsync`.
- [[VectorDbProviderType]] — enum that identifies this provider.

## Source Files
- `AGXIOME.Storage/VectorStores/SingleStoreVectorStore.cs` (ingested 2026-05-14)