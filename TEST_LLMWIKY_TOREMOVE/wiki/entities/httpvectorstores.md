---
title: HttpVectorStores
category: entity
tags: [vector store, HTTP, REST, Pinecone, Weaviate, Milvus, ChromaDb, Vald, Vespa, LanceDb, ScaNN]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# HttpVectorStores

## Summary
The `HttpVectorStores` class file provides HTTP-based implementations of the `IVectorStore` interface for eleven different vector database providers. All stores communicate via REST APIs and accept an injected `HttpClient`, enabling unit testing with mock HTTP handlers. Providers range from managed cloud services (Pinecone, Weaviate Cloud, Milvus Cloud, ChromaDb Cloud) to self-hosted/alternative engines (Weaviate Self-Hosted, Milvus Self-Hosted, ChromaDb Self-Hosted, Vald, Vespa, LanceDb, ScaNN). A shared `Json` helper defines camelCase, null‑ignoring serialization options.

## Key Details
- **IVectorStore Implementations**: PineconeStore, WeaviateCloudStore, MilvusCloudStore, ChromaDbCloudStore, WeaviateSelfHostedStore, MilvusSelfHostedStore, ChromaDbSelfHostedStore, ValdStore, VespaStore, LanceDbStore, ScaNNStore.
- **Provider Types**: Each store is associated with a distinct `VectorDbProviderType` (VD-01 through VD-28).
- **Shared JSON Config**: All HTTP calls use `JsonSerializerOptions` with `CamelCase`, `IgnoreNull`, and `AllowNamedFloatingPointLiterals`.
- **Collection Handling**: Most stores expect a collection or namespace string; `PineconeStore` treats it as a namespace, `WeaviateVectorStoreBase` as a class name.
- **Idempotent Creation**: `WeaviateVectorStoreBase.EnsureCollectionAsync` ignores 422 (class already exists) responses.
- **Search & Upsert**: All stores implement `SearchAsync` (with `topK`, `minScore`, optional filter) and `UpsertAsync`/`UpsertBatchAsync`.
- **Pinecone-specific**: No collection listing; deletion uses `deleteAll` on a namespace.
- **Vald**: Index creation via `upsert` endpoint; search uses a separate body structure.
- **LanceDb**: Uses a simple REST wrapper; schema is automatically inferred.
- **ScaNN**: Assumes an HTTP wrapper over its gRPC interface; supports index building on first insert.

## Cross-References
- [[IVectorStore]] — Interface contract implemented by all HTTP stores.
- [[VectorRecord]] — Data model for vector + metadata.
- [[VectorSearchResult]] — Result model returned by search operations.
- [[VectorDbProviderType]] — Enum identifying each provider.
- [[VectorFilter]] — Optional filter applied during vector search.

## Source Files
- `AGXIOME.Storage/VectorStores/HttpVectorStores.cs` (ingested 2026-05-14)