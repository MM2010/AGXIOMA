---
title: SQLite and DuckDB Embedded Vector Stores
category: entity
tags: [vectorstore, sqlite, duckdb, embedded, sqlitevec, duckdbvector]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# SQLite and DuckDB Embedded Vector Stores

## Summary
The `SqlEmbeddedVectorStores.cs` file provides two embedded vector store implementations – **SqliteVecStore** (VD-19) and **DuckDbVectorStore** (VD-21) – that store vectors as JSON TEXT in relational tables and perform brute‑force cosine similarity search in‑memory. These stores are optimized for moderate collection sizes (<100k vectors) and serve as simple drop‑in replacements when native vector extensions (e.g., `sqlite‑vec`, DuckDB VSS) are not yet available or desired.

## Key Details
- Both classes implement the [[IVectorStore]] interface and expose a `ProviderType` property (`VectorDbProviderType.SqliteVec` and `VectorDbProviderType.DuckDBVector`).
- Vectors are serialized as JSON arrays using `System.Text.Json` with camelCase naming and named floating‑point literal support.
- **SqliteVecStore** uses `Microsoft.Data.Sqlite` and creates a table per collection with columns `id`, `vector_json`, `text_content`, and `metadata`. Accepts a `SqliteVecOptions` record (mandatory `ConnectionString`, optional `AutoCreateSchema`).
- **DuckDbVectorStore** uses `DuckDB.NET.Data` and a persistent `DuckDBConnection` (connection‑scoped in‑memory mode). Accepts a `DuckDbVectorOptions` record (mandatory `DataSource`).
- Both stores implement: `EnsureCollectionAsync`, `DeleteCollectionAsync`, `ListCollectionsAsync`, `UpsertAsync`, `UpsertBatchAsync`, `GetAsync`, `DeleteAsync`, and `SearchAsync`.
- `SearchAsync` loads all vectors from the collection, computes cosine similarity row‑by‑row, applies `minScore` filter, and returns the top‑K results via brute force.
- Collection names are validated with a regex (`^[a-zA-Z_][a-zA-Z0-9_]*$`) to prevent SQL injection.
- **Limitations**: No native index (HNSW/IVF). Performance degrades linearly with collection size. Recommended to migrate to `sqlite-vec` (SQLite) or DuckDB VSS extension when stable.

## Cross-References
- [[IVectorStore]] — Contract implemented by both stores
- [[VectorSearchResult]] — Return type of `SearchAsync`
- [[VectorRecord]] — Data record type used in upsert and get operations
- [[VectorDbProviderType]] — Enum defining supported providers

## Source Files
- `AGXIOME.Storage/VectorStores/SqlEmbeddedVectorStores.cs` (ingested 2026-05-14)