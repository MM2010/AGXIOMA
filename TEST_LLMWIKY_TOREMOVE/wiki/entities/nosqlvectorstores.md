---
title: NoSQL Vector Stores
category: entity
tags: [vector-store, redis-stack, mongodb-atlas, cosmos-db, nosql, ai]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# NoSQL Vector Stores

## Summary
A set of concrete implementations of the [[IVectorStore]] interface that provide vector storage and similarity search using popular NoSQL databases: Redis Stack (with RediSearch), MongoDB Atlas (with `$vectorSearch`), and Azure Cosmos DB for MongoDB vCore. These stores enable AI agents to persist and query high-dimensional embeddings directly within existing data infrastructure, reducing operational overhead by avoiding dedicated vector databases.

## Key Details
- Three providers: **RedisStackVectorStore** (VD-22), **MongoDbAtlasVectorStore** (VD-23), **CosmosDbVCoreStore** (VD-24)
- All implement [[IVectorStore]] with methods: `EnsureCollectionAsync`, `DeleteCollectionAsync`, `ListCollectionsAsync`, `UpsertAsync`, `UpsertBatchAsync`, `GetAsync`, `DeleteAsync`, `SearchAsync`
- Redis Stack uses `FT.CREATE` / `FT.SEARCH` with HNSW index on hash keys; requires Redis Stack 7.x
- MongoDB Atlas uses `$vectorSearch` aggregation pipeline; requires Atlas cluster with Vector Search index on `"vector"` field
- Cosmos DB vCore store (partial code) relies on Cosmos DB for MongoDB vCore API with a vector index
- Cosine distance is the default similarity metric across all stores
- Records are stored as [[VectorRecord]] objects with id, vector, optional text, and metadata
- Supports filtering via [[VectorFilter]] during search (not shown in snippet but declared in interface)
- Batch operations use provider-specific batching (e.g., Redis batch, MongoDB bulk writes)
- All stores are resilient to duplicate collection creation (idempotent `EnsureCollection`)

## Cross-References
- [[IVectorStore]] — interface that these stores implement
- [[VectorRecord]] — data model for stored vectors
- [[VectorFilter]] — optional search filter parameter
- [[VectorSearchResult]] — result type from `SearchAsync`
- [[VectorDbProviderType]] — enum identifying each provider

## Source Files
- `AGXIOME.Storage/VectorStores/NoSqlVectorStores.cs` (ingested 2026-05-14)