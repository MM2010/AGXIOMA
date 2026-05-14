---
title: CodeChunker
category: entity
tags: [chunker, code, roslyn, fallback]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# CodeChunker

## Summary
CodeChunker is an implementation of [[IChunker]] that splits source code into syntactically self-contained chunks using Roslyn (Microsoft.CodeAnalysis) via late binding to avoid a hard compile-time dependency. If Roslyn is unavailable, it falls back transparently to [[RecursiveChunker]] or a heuristic keyword-based split.

## Key Details
- Implements `IChunker` with `ChunkerType = Code`.
- Uses reflection to call `CSharpSyntaxTree.ParseText`; no direct NuGet reference required.
- Top-level members (classes, structs, methods, etc.) become individual chunks; nested members are included in the parent chunk.
- If Roslyn is missing (e.g. assembly not loaded), `TryRoslynChunk` returns null and the fallback logic runs.
- Fallback path: `HeuristicCodeChunk` splits on keywords like `\nclass `, `\npublic `, etc., then subdivides oversized segments using `RecursiveChunker`.
- Gracefully handles empty input, returning a single empty chunk.
- Designed to produce chunks that are syntactically valid and logically cohesive for downstream processing.

## Cross-References
- [[IChunker]] — interface that CodeChunker implements.
- [[RecursiveChunker]] — fallback used when Roslyn is unavailable or heuristic sub-chunking is needed.
- [[DocumentChunk]] — output type of the `Chunk` method.

## Source Files
- `AGXIOME.Storage/Chunkers/CodeChunker.cs` (ingested 2026-05-14)