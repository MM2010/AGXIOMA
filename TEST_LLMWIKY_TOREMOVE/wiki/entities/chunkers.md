---
title: Chunkers
category: entity
tags: [chunking, text-splitting, IChunker, DocumentChunk, tokenization]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Chunkers

## Summary
Chunkers are components that split input text into discrete, manageable pieces for downstream processing (e.g., LLM embedding or retrieval). The AGXIOMA framework provides three built-in `IChunker` implementations — `FixedSizeChunker`, `HeadingChunker`, and `SentenceChunker` — each following a different splitting strategy to balance chunk coherence, token budget, and offset accuracy. They all use approximate token counting (1 token ≈ 4 characters) and support configurable `maxTokens` and `overlap` parameters.

## Key Details
- All chunkers implement the `IChunker` interface, which exposes a `Chunk(text, maxTokens, overlap)` method returning `IReadOnlyList<DocumentChunk>`.
- Each chunker returns a distinct `ChunkerType` value: `Fixed`, `Heading`, or `Sentence`.
- **FixedSizeChunker** splits on word boundaries, producing chunks of approximately `maxTokens` words, stepping forward by `maxTokens - overlap` words to create overlap.
- **HeadingChunker** splits markdown text at ATX heading lines (`#`, `##`, `###`) so each heading and its content become one chunk. Falls back to FixedSizeChunker-like behavior for non-markdown content (though the code currently doesn't fall back; it simply treats each section as a chunk).
- **SentenceChunker** splits on sentence-ending punctuation (`.` `!` `?` and newlines) and groups sentences until the approximate character budget (`maxTokens * 4`) is reached; preserves the last sentence as overlap for the next chunk.
- Offsets are approximate (character-level) — exact source scanning is not performed; a dedicated scanning chunker should be used when precise offsets are required.
- All three handle null/empty input by returning a single empty `DocumentChunk`.

## Cross-References
- [[Ichunker]] — The interface all chunkers implement.
- [[DocumentChunk]] — The output model containing text, index, start offset, and end offset.
- [[ChunkerType]] — Enum identifying which strategy a chunker uses.

## Source Files
- `AGXIOME.Storage/Chunkers/Chunkers.cs` (ingested 2026-05-14)