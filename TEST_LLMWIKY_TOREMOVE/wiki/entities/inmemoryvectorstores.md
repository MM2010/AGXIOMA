---
title: In-Memory Vector Stores
category: entity
tags: [vector-stores, in-memory, development, cosine-similarity, AGXIOME]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# In-Memory Vector Stores

## Summary
The `InMemoryVectorStores.cs` file provides three pure C#, thread-safe implementations of the [[IVectorStore]] interface: `HNSWLibStore`, `FaissStore`, and `AnnoyStore`. These stores are designed as development and testing substitutes for production-grade native vector database libraries (HNSWLib, FAISS, Annoy) that lack stable .NET 10 NuGet wrappers. All three use brute-force cosine similarity (exact, O(n·d)) over a `ConcurrentDictionary`-backed collection, making them suitable for moderate workloads (<100k vectors). They mimic the contract of their native counterparts and can be swapped for a faster kernel when a production binding becomes available.

## Key Details
- Three classes implement `IVectorStore`: `HNSWLibStore`, `FaissStore`, and `AnnoyStore`.
- Each reports its provider type via `ProviderType` (e.g., `VectorDbProviderType.HNSWLib`).
- Storage uses `InMemoryVectorCollection`, backed by a `ConcurrentDictionary<string, VectorRecord>`.
- `FaissStore` normalizes vectors to unit length (L2) on upsert and uses inner product for cosine similarity.
- `HNSWLibStore` and `AnnoyStore` compute cosine similarity directly from raw vectors.
- All methods return `Task` and are fully async-compatible.
- Collection management: `EnsureCollectionAsync`, `DeleteCollectionAsync`, `ListCollectionsAsync`.
- CRUD operations: `Upsert`, `UpsertBatch`, `Get`, `Delete`.
- `SearchAsync` returns top-k results filtered by `minScore` and optional `VectorFilter`.
- Search is O(n·d) brute-force; exact, not approximate.
- No external dependencies; fully self-contained in `AGXIOME.Storage` namespace.
- Suitable for development, testing, and collections <100k vectors.

## Cross-References
- [[IVectorStore]] — the interface these stores implement.
- [[VectorDbProviderType]] — the enum for identifying the provider.
- [[VectorSearchResult]], [[VectorRecord]] — data models used in store operations.

## Source Files
- `AGXIOME.Storage/VectorStores/InMemoryVectorStores.cs` (ingested 2026-05-14)