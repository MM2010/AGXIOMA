---
title: CsvRowChunker
category: entity
tags: [chunker, csv, chunking, storage]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# CsvRowChunker

## Summary
The `CsvRowChunker` is a specialized chunker for CSV text that splits data rows into self-contained chunks, each including the header row for independent retrieval. It reinterprets the `maxTokens` parameter as rows per chunk and supports optional overlap between chunks to preserve context for vector search. If no header is detected (first line lacks a comma), it falls back to [[FixedSizeChunker]].

## Key Details
- Implements `IChunker` with `ChunkerType = ChunkerType.CsvRow`.
- First line is always treated as header and prepended to every chunk.
- Empty lines are skipped via `StringSplitOptions.RemoveEmptyEntries`.
- Fallback to `FixedSizeChunker` if header has no comma.
- `maxTokens` is reinterpreted as rows per chunk (minimum 1).
- `overlap` rows from previous chunk are prepended (after header) to preserve windowed context.
- Overlap cannot exceed `rowsPerChunk - 1` and defaults to 0 if negative.
- Produces `DocumentChunk` objects with content = header + newline + joined rows, and start/end offsets based on original text cursor.
- Offsets account for header length and newline characters after each row.

## Cross-References
- [[FixedSizeChunker]] — fallback chunker when CSV header is missing
- [[IChunker]] — interface that `CsvRowChunker` implements
- [[DocumentChunk]] — output model for each chunk

## Source Files
- `AGXIOME.Storage/Chunkers/CsvRowChunker.cs` (ingested 2026-05-14)