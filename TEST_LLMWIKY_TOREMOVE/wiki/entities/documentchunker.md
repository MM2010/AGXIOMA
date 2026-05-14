---
title: DocumentChunker
category: entity
tags: [chunker, document, text-splitting, hierarchical]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# DocumentChunker

## Summary
The `DocumentChunker` is a hierarchical text-splitting strategy that partitions documents along natural structural boundaries. It first divides by ATX headings (`#`, `##`, `###`), then by paragraphs (`\n\n`), and finally by sentence endings (`.`, `!`, `?`). Any segment still exceeding the token budget is hard-split using the `RecursiveChunker` fallback. Overlap is applied at the leaf level by prepending the tail of the previous chunk to the next, preserving contextual coherence across chunk boundaries.

## Key Details
- Implements `IChunker` with `ChunkerType = Document`.
- Splits in four levels: headings → paragraphs → sentences → recursive fallback.
- Default token budget of 512 tokens (mapped to character budget via `maxTokens * 4`).
- Overlap default of 64 tokens (64 * 4 characters), applied only between adjacent leaf segments.
- Headings are recognized as lines starting with `#` (ATX style); each heading starts a new section.
- Empty sections and whitespace-only parts are discarded.
- The `RecursiveChunker` fallback is invoked with zero overlap to avoid double overlap.
- Returns a list of `DocumentChunk` objects with content, index, start, and end character positions.
- Designed to produce semantically meaningful chunks while respecting token limits.

## Cross-References
- [[entities/RecursiveChunker]] — fallback used for hard-splitting over-budget segments.
- [[concepts/IChunker]] — interface implemented by this class.
- [[models/DocumentChunk]] — output model containing chunk content and metadata.
- [[enums/ChunkerType]] — enumeration that includes `Document` as a member.

## Source Files
- `AGXIOME.Storage/Chunkers/DocumentChunker.cs` (ingested 2026-05-14)