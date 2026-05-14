---
title: Search Stores
category: entity
tags: [elasticsearch, meilisearch, commandstore, search, storage]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Search Stores

## Summary
The **Searchstores** are concrete implementations of the [[ICommandStore]] interface, providing command persistence via Elasticsearch (using the official `.NET` client) and Meilisearch (using the Meilisearch.NET SDK). Each store supports save, update, and delete operations, using a configurable index prefix to isolate entities of different types. The stores are designed for high-performance search‑oriented workloads, leveraging Elasticsearch’s BM25 ranking and Meilisearch’s typo‑tolerant indexing.

## Key Details
- `ElasticsearchCommandStore` connects to Elasticsearch via Cloud ID or a single‑node URI, authenticating with an API key.
- `MeilisearchCommandStore` connects to a Meilisearch instance at a given API URL and API key.
- Both stores use an index naming convention: `{IndexPrefix}-{entityTypeNameLowercase}`.
- Entity identity is derived from a public `Id` property; if missing, a new GUID is generated (Elasticsearch store) or the primary key must be specified (Meilisearch – default `"Id"`).
- `SaveAsync` and `UpdateAsync` are identical (upsert semantics); `DeleteAsync` performs document removal.
- Elasticsearch store validates response status and throws `InvalidOperationException` on failure; Meilisearch store returns a `Task` (fire‑and‑forget style) for document addition.
- Both stores implement `ICommandStore` and expose `StorageProviderType` (`Elasticsearch` or `Meilisearch`).
- Store options are exposed via records (`ElasticsearchStoreOptions`, `MeilisearchStoreOptions`) with required fields.
- The stores reside in the `AGXIOME.Storage.Stores` namespace and support cancellation tokens on all operations.

## Cross-References
- [[ICommandStore]] — the interface implemented by both search stores.
- [[StorageProviderType]] — enum used to identify the underlying provider.
- [[CommandStore]] — other command store implementations (e.g., relational).

## Source Files
- `AGXIOME.Storage/Stores/SearchStores.cs` (ingested 2026-05-14)