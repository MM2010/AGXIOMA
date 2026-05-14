---
title: ContentStore
category: entity
tags: [storage, content, knowledge base, RAG]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# ContentStore

## Summary
The `ContentStore` is a typed, server-side storage for knowledge content entries (text, markdown, code, etc.) that backs a knowledge base feeding RAG pipelines. It combines an `ICommandStore` for persistence with an in-memory index for fast filtering by namespace, tag, and keyword.

## Key Details
- Stores and manages `ContentEntry` records — flat DTOs with `Id`, `Title`, `Body`, `DocumentType`, `ContentTag`, `Namespace`, `SourceUri`, metadata, and timestamps.
- Provides a typed interface `IContentStore` with operations: `UpsertAsync`, `GetAsync`, `DeleteAsync`, `SearchAsync`, `ListAsync`.
- Search supports AND‑combined filters on namespace, `ContentTag`, and case‑insensitive keyword matching against title/body.
- Classification uses a strict `ContentTag` enum (no magic strings): `Unclassified`, `Guide`, `Reference`, `Tutorial`, `Example`, `Policy`, `Faq`, `Changelog`, `Api`, `DataSchema`.
- `ContentEntry.DocType` maps to `DocumentType` from `AGXIOME.Shared.Models` for reader‑pipeline compatibility.
- Designed to pair with a vector store for semantic retrieval in production RAG setups.
- Content bodies are server‑side only; `SourceUri` must be server‑controlled for security.

## Cross-References
- [[ICommandStore]] — used as the persistent backing store.
- [[DocumentType]] — format enum referenced by `ContentEntry.DocType`.
- [[IKnowledgeBase]] — companion for vector‑based semantic search.

## Source Files
- `AGXIOME.Core/Storage/ContentStore.cs` (ingested 2026-05-14)