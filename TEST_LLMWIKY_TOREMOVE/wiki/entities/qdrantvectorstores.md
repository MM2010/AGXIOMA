---
title: QdrantVectorStores
category: entity
tags: [vector store, Qdrant, gRPC, cosine similarity, storage]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# QdrantVectorStores

## Summary
An abstract base class and two concrete implementations for using Qdrant as a vector database. The `QdrantVectorStoreBase` class implements the `IVectorStore` interface using the official Qdrant gRPC client, exposing operations for collection lifecycle, upsert, retrieval, deletion, and similarity search with cosine distance. Two sealed variants—`QdrantCloudStore` and `QdrantSelfHostedStore`—provide identical functionality while differing only in their `VectorDbProviderType` enumeration value, allowing the framework to distinguish between cloud and self-hosted deployments.

## Key Details
- Abstract class `QdrantVectorStoreBase` wraps `QdrantClient` (thread-safe, manages gRPC channel).
- Concrete sealed classes: `QdrantCloudStore` (VD-03) and `QdrantSelfHostedStore` (VD-11).
- Collection creation uses `Distance.Cosine` as the similarity metric.
- `EnsureCollectionAsync` checks existence before creating; `DeleteCollectionAsync` and `ListCollectionsAsync` delegate directly to the SDK.
- `UpsertAsync` and `UpsertBatchAsync` convert `VectorRecord` objects to `PointStruct`, embedding text content and metadata into payload fields.
- `GetAsync` retrieves a single vector by ID using `RetrieveAsync` with vectors included.
- `SearchAsync` uses `SearchAsync` with configurable `topK` and `minScore` (`scoreThreshold`). Results are mapped to `VectorSearchResult` with text content from the payload.
- ID mapping: attempts `Guid.TryParse` first; otherwise uses the string as a UUID directly.
- No explicit `IVectorStore` interface is shown in this snippet, but the class implements it (see [[IVectorStore]]).
- Dependencies: `Qdrant.Client` NuGet package.

## Cross-References
- [[IVectorStore]] — interface that `QdrantVectorStoreBase` implements
- [[VectorDbProviderType]] — enumeration used for provider identification
- [[VectorRecord]] — standard record type consumed by upsert methods
- [[VectorSearchResult]] — result type returned by `SearchAsync`
- [[VectorFilter]] — optional filter parameter for search queries

## Source Files
- `AGXIOME.Storage/VectorStores/QdrantVectorStores.cs` (ingested 2026-05-14)