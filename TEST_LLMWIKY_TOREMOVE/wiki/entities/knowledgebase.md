---
title: KnowledgeBase
category: entity
tags: [entity, storage, vector, knowledge]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# KnowledgeBase

## Summary
The `KnowledgeBase` class is the core component for ingesting documents into a vector store and performing semantic search over them. It orchestrates chunking, embedding, upserting to pgvector, and parallel search across per-document collections. Its design supports clean isolation per document (O(1) delete) via separate collections, with a trade‑off of parallel iteration over collections during search – acceptable for moderate corpus sizes.

## Key Details
- Implements `[[IKnowledgeBase]]`
- Each document is stored in its own vector collection (collection name = document ID)
- `IngestAsync(Document, ct)` – chunks content, embeds chunks in batch, upserts vector records to the document’s collection
- `IngestBatchAsync(IAsyncEnumerable<Document>, ct)` – streams and ingests an async sequence of documents
- `SearchAsync(query, options, ct)` – searches across all known collections in parallel, merges and rank‑limits results
- `IngestMarkdownFileAsync(filePath, ct)` – convenience method to read a markdown file, derive doc ID from filename, add metadata, and ingest
- `DeleteAsync(documentId, ct)` – deletes the entire collection (drop) of a document
- Architecture note: for large corpora (1M+ chunks) a single‑collection model with metadata filtering is recommended
- Uses `[[Chunker]]`, `[[Embedder]]`, `[[VectorStore]]`, and `[[KnowledgeSearchOptions]]` (held via options record `KnowledgeBaseOptions`)
- Options include `ChunkMaxTokens` (default 512), `ChunkOverlap` (default 64), `DefaultTopK` (10), `MinRelevanceScore` (0.65), and `MaxContextTokens` (4096)

## Cross-References
- [[IKnowledgeBase]] — the interface this class fulfills
- [[Document]] — the input data model for ingestion
- [[Chunker]] — splits document content into chunks
- [[Embedder]] — generates vector embeddings from text
- [[VectorStore]] — persists and searches vector records
- [[KnowledgeSearchOptions]] — search parameters (topK, minScore)

## Source Files
- `AGXIOME.Storage/KnowledgeBase.cs` (ingested 2026-05-14)