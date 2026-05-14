---
title: RocksDB and SQL Server Vector Store Implementations
category: entity
tags: [vector-store, rocksdb, hnsw, sql-server, persistence]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# RocksDB and SQL Server Vector Store Implementations

## Summary
This source file provides two concrete implementations of the `IVectorStore` interface for the AGXIOME framework: `RocksDbHnswStore` (VD-29) and `SqlServerVectorStore` (VD-30). `RocksDbHnswStore` combines a persistent RocksDB key-value store with an in-memory concurrent dictionary for exact cosine similarity search, rebuilding its index from disk on startup. `SqlServerVectorStore` leverages SQL Server 2025's native `VECTOR(dim)` column type and the `VECTOR_DISTANCE` function for ANN search, treating each collection as a database table.

## Key Details
- **RocksDbHnswStore**: Key format is `"collection\0id"` (null-separated). Uses RocksDB for persistence and a `ConcurrentDictionary<string, float[]>` in memory for fast ANN searches. Index is rebuilt on construction by iterating over all RocksDB keys.
- **SqlServerVectorStore**: Collection names become table names (validated against SQL injection). Each table has columns `Id NVARCHAR(900)`, `Vector VECTOR(dim)`, `Text NVARCHAR(MAX)`, and `Metadata NVARCHAR(MAX)` containing serialized JSON. Search uses `VECTOR_DISTANCE('cosine', @query, Vector)`.
- Both stores implement `IVectorStore` supporting `EnsureCollectionAsync`, `DeleteCollectionAsync`, `ListCollectionsAsync`, `UpsertAsync`, `UpsertBatchAsync`, `GetAsync`, `DeleteAsync`, and `SearchAsync`.
- `RocksDbHnswStore` supports optional `VectorFilter` (not yet applied in the provided snippet), while `SqlServerVectorStore` supports filtering via WHERE clause based on the filter parameter.
- `RocksDbHnswStore` uses exact cosine similarity computed in C#; `SqlServerVectorStore` delegates similarity computation to the database engine.
- The `SqlServerVectorStore` requires SQL Server 2025 or later (vector support in preview).
- Both stores handle null vectors and edge cases such as empty collections or zero vectors gracefully.
- No batching optimizations for SQL Server inserts are shown—each upsert fires a separate SQL command.

## Cross-References
- [[IVectorStore]] — The interface both stores implement.
- [[VectorRecord]] — The data model passed to/from these stores.
- [[VectorSearchResult]] — The result type returned by search operations.
- [[VectorDbProviderType]] — Enum indicating the backend type (RocksDBHNSW, SQLServerVector).

## Source Files
- `AGXIOME.Storage/VectorStores/RocksDbAndSqlServerVectorStores.cs` (ingested 2026-05-14)