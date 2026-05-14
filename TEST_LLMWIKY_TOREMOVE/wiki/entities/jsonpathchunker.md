---
title: JsonPathChunker
category: entity
tags: [chunker, json, parsing]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# JsonPathChunker

## Summary
The `JsonPathChunker` splits a JSON document into chunks by navigating a list of JSON Pointer (RFC 6901) paths. Each matched node is serialised to compact JSON and becomes one chunk. It gracefully falls back to [[RecursiveChunker]] for non‑JSON input or when an individual chunk exceeds the token budget. Overlap is applied as text‑level tail‑carry after serialisation, and each chunk carries the originating JSON path in its metadata for traceability.

## Key Details
- Implements `IChunker` with `ChunkerType = JsonPath`.
- Accepts an optional `JsonPaths` list (RFC 6901 pointers like `/items/0/text`). When empty, top‑level array items or object property values are used as default paths.
- If the input is not valid JSON, the chunker falls back to `RecursiveChunker`.
- For each extracted value that fits within the token budget (4 characters per token estimate), a single `DocumentChunk` is created.
- Over‑budget values are sub‑chunked recursively via `RecursiveChunker` with zero overlap.
- Overlap (text‑level tail‑carry) is applied after serialisation by prepending the trailing characters of the previous chunk’s value.
- Each chunk’s `Metadata` dictionary contains the key `"json_path"` with the originating JSON pointer string.
- The class is sealed and lives in the `AGXIOME.Storage.Chunkers` namespace.
- The `SlidingWindowSplitter` logic is not used; splitting is purely based on JSON pointer paths.

## Cross-References
- [[RecursiveChunker]] — fallback engine for non‑JSON / oversized chunks
- [[IChunker]] — interface that `JsonPathChunker` implements
- [[DocumentChunk]] — the chunk type produced
- [[ChunkerType]] — enumeration identifying chunker variants

## Source Files
- `AGXIOME.Storage/Chunkers/JsonPathChunker.cs` (ingested 2026-05-14)