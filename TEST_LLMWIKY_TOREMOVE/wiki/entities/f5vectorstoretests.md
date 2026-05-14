---
title: F5VectorStoreTests
category: entity
tags: [vector-store, testing, unit-tests, integration-tests]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# F5VectorStoreTests

## Summary
This test suite validates all 30 vector store providers supported by the AGXIOME framework. It contains 210 tests (7 per provider) covering provider type identification, null argument handling, round-trip operations, search, batch upsert, deletion, and collection listing. Tests are categorized by store type: in-memory, embedded, persistent disk, HTTP-based, and SDK-dependent. Live endpoints are required for HTTP and SDK-dependent stores; those tests are skipped by default.

## Key Details
- **Total stores tested**: 30 (Pinecone, Weaviate, Qdrant, Milvus, Chroma, Supabase pgvector, etc.)
- **Total tests**: 210 (7 per store)
- **Test categories per store**:
  - `ProviderType_Is<Name>` – verifies correct [[VectorDbProviderType]] enum value
  - `Constructor_Null*_Throws` – validates null argument guard clauses
  - `EnsureCollection_Upsert_Get_RoundTrip` – end-to-end insertion and retrieval (skipped for remote stores)
  - `Search_EmptyCollection_ReturnsEmpty` – search on empty collection (skipped for remote stores)
  - `UpsertBatch_MultipleRecords` – batch upsert behavior (skipped for remote stores)
  - `Delete_RemovesRecord` – deletion correctness (skipped for remote stores)
  - `ListCollections_Returns*` – collection enumeration (skipped for remote stores)
- **Store type test coverage**:
  - In-memory (HNSWLib, FAISS, Annoy), embedded (SqliteVec, DuckDB), persistent (RocksDB): all 7 tests run
  - HTTP stores (Pinecone, Weaviate, Milvus, Chroma, Vald, Vespa, LanceDB, ScaNN): only first 2 tests run (provider type + null arg), remaining 5 skipped
  - SDK-dependent stores (Qdrant, pgvector variants, SingleStore, Elasticsearch, Meilisearch, Redis, MongoDB, Cosmos, SqlServer): same as HTTP stores
- **Mock infrastructure**: Uses custom `TestHttpMessageHandler` for HTTP-based stores to simulate API responses
- **Test discovery pattern**: Each store has its own test class named `<StoreName>StoreTests`

## Cross-References
- [[VectorDbProviderType]] — enum validated by `ProviderType_Is*` tests
- [[TestHttpMessageHandler]] — mock HTTP handler used for HTTP-backed stores
- [[AGXIOME.Storage.VectorStores]] — namespace containing all vector store implementations

## Source Files
- `AGXIOME.Tests/F5VectorStoreTests.cs` (ingested 2026-05-14)