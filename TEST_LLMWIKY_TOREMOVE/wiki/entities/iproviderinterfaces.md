---
title: IProviderInterfaces
category: entity
tags: [interfaces, providers, abstraction, async, thread-safe]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# IProviderInterfaces

## Summary
The `IProviderInterfaces.cs` file defines the core abstraction contracts for pluggable providers in the AGXIOMA .NET agent framework. These interfaces cover embedding, chunking, reranking, vector storage, document reading, toolkits, and database operations. All implementations are expected to be thread-safe, async-only, and stateless for per-call invocations.

## Key Details
- **IEmbedder**: Converts text to dense float vectors. Provides `EmbedAsync` (single) and `EmbedBatchAsync` (batch, order-preserved). Empty input returns zero vector.
- **IChunker**: Splits documents into overlapping/non‑overlapping chunks based on token count. Stateless value‑type pattern; one instance reused.
- **IReranker**: Re‑orders retrieval candidates by relevance to a query. Top‑K enforcement; 0 returns all.
- **IVectorStore**: Manages vector records with upsert, delete, and ANN search. Supports metadata pre‑filtering via `VectorFilter`. Collection = logical namespace. All writes idempotent by ID.
- **IDocumentReader**: Reads heterogeneous sources (file, HTTP, stream) into structured `DocumentContent` with sections. Throws `NotSupportedException` for unsupported URI schemes.
- **IToolkit**: Contains 1‑N named tools callable by an agent. Stateless invocations; provides a descriptor for each tool.
- **IDbProvider** (truncated in source): Generic async CRUD+query over any storage backend.
- All interfaces enforce async‑only patterns (no blocking I/O) and thread‑safety.
- Record types (`DocumentChunk`, `RerankCandidate`, `VectorRecord`, `VectorFilter`, etc.) provide structured data transfer.

## Cross-References
- [[IVectorStore]] — vector storage contract defined in the same file
- [[IEmbedder]] — embedding contract
- [[IChunker]] — chunking contract
- [[IReranker]] — reranking contract
- [[IDocumentReader]] — document reading contract
- [[IToolkit]] — toolkit contract

## Source Files
- `AGXIOME.Shared/Interfaces/IProviderInterfaces.cs` (ingested 2026-05-14)