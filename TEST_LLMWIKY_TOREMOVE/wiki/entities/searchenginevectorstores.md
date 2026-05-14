---
title: SearchEngineVectorStores
category: entity
tags: [vector store, Elasticsearch, Meilisearch, IVectorStore, AGXIOME]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# SearchEngineVectorStores

## Summary
Two concrete implementations of the `IVectorStore` interface that leverage popular search engines for dense vector retrieval:
- **ElasticsearchDenseStore (VD-15)** uses Elasticsearch 8.x `dense_vector` with HNSW kNN search and dot-product similarity (pre-normalize vectors for cosine behavior).
- **MeilisearchVectorStore (VD-16)** uses Meilisearch v1.6+ hybrid (semantic + full-text) vector search, with vectors embedded in JSON payloads.

Both map a logical collection to an index/UID in their respective engine and support upsert, batch upsert, get, delete, and search operations.

## Key Details
- Both classes implement `IVectorStore` and expose `ProviderType` (`ElasticsearchDense` / `MeilisearchVector`).
- `EnsureCollectionAsync` creates the index (Elasticsearch) or ensures settings (Meilisearch) with the required dimensions and mappings.
- Vectors are stored as `float[]`; Elasticsearch uses `dense_vector` with dot_product similarity, Meilisearch uses the `_vectors` field.
- Search uses kNN (Elasticsearch) or Meilisearch's built-in semantic search; both accept `queryVector`, `topK`, `minScore`, and an optional `VectorFilter`.
- `GetAsync` retrieves a single record by ID; `DeleteAsync` removes by ID.
- Upsert is idempotent and uses the record’s `Id` as the document key.
- Error handling: operations that fail may throw or silently swallow (e.g., batch upsert logs the original exception if invalid).
- Both stores rely on external client libraries (`Elastic.Clients.Elasticsearch` and `Meilisearch`).

## Cross-References
- [[IVectorStore]] — interface these classes implement
- [[VectorDbProviderType]] — enum used to identify the provider
- [[VectorRecord]] — data type used for documents
- [[VectorSearchResult]] — result type for search queries

## Source Files
- `AGXIOME.Storage/VectorStores/SearchEngineVectorStores.cs` (ingested 2026-05-14)