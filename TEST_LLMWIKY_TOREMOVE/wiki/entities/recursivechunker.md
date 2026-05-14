---
title: RecursiveChunker
category: entity
tags: [chunking, text-splitting, recursive, IChunker]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# RecursiveChunker

## Summary
`RecursiveChunker` is an implementation of [[IChunker]] that splits text into overlapping chunks by applying a priority-ordered list of separators recursively. It starts with paragraph breaks (`\n\n`), then line breaks (`\n`), sentence terminators (`. `, `? `, `! `), and finally whitespace (` `). If a fragment still exceeds the token budget after exhausting all separators, it falls back to a hard character‑based split (via `FixedSizeChunker`). Overlap context is injected by prepending the tail of the previous chunk’s content.

## Key Details
- Implements `IChunker` with `ChunkerType.Recursive`.
- Default separator priority: `\n\n` → `\n` → `. ` → `? ` → `! ` → ` ` (single space).
- Token budget is approximated as `maxTokens * 4` characters; `overlap` is converted to characters as `overlap * 4`.
- Recursion: if a segment exceeds the hard‑split threshold, the next separator in the list is tried; if no separator is found, the segment is passed to `FixedSizeChunker` (hard split).
- **MergeShort** logic: consecutive very‑short segments (below 10% of the character budget, minimum 20 chars) are combined into a single chunk to avoid producing many tiny fragments.
- Overlap is applied at the leaf level: each chunk (except the first) is prefixed with the trailing `overlap` characters of the previous chunk.
- Returns a list of `DocumentChunk` objects with approximate character‑offset tracking (`start`, `end`) and a chunk index.

## Cross-References
- [[FixedSizeChunker]] — used as a fallback when no separator can reduce a fragment to the token budget.
- [[IChunker]] — interface that `RecursiveChunker` implements.
- [[DocumentChunk]] — the output model for all chunkers.
- [[AGXIOME.Storage/Chunkers]] — namespace containing this and other chunker implementations.

## Source Files
- `AGXIOME.Storage/Chunkers/RecursiveChunker.cs` (ingested 2026-05-14)