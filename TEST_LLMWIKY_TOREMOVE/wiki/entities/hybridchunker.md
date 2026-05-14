---  
title: HybridChunker  
category: entity  
tags: [chunker, hybrid, semantic, recursive]  
source_count: 1  
created_at: 2026-05-14  
updated_at: 2026-05-14  
---  

# HybridChunker  

## Summary  
The **HybridChunker** is a two-pass text chunker that combines semantic topic‑aware splitting with recursive size‑enforcement. In the first pass it uses [[SemanticChunker]] to identify coarse topic boundaries, and in the second pass it applies [[RecursiveChunker]] to any section that still exceeds the token budget. Overlap is applied only during the final leaf‑level assembly, avoiding double‑overlap. When no `IEmbedder` is provided the first pass degrades to syntactic sentence‑grouping.  

## Key Details  
- Sealed class implementing `IChunker` with `ChunkerType.Hybrid`.  
- Constructor accepts an optional `IEmbedder`; defaults to `null` (syntactic fallback).  
- `Chunk(string text, int maxTokens = 512, int overlap = 64)` returns `IReadOnlyList<DocumentChunk>`.  
- **Pass 1 (Semantic):** Calls `SemanticChunker.Chunk` with `maxTokens * 4` and `overlap: 0` to create coarse sections.  
- **Pass 2 (Recursive):** For each semantic section that still exceeds `maxTokens * 4` characters, calls `RecursiveChunker.Chunk` with the original `maxTokens` and `overlap: 0`.  
- Overlap is added only at the final assembly step by prepending the overlapping tail of the previous leaf segment.  
- Empty input returns a single empty `DocumentChunk`.  
- The result is always bounded by the specified `maxTokens`.  

## Cross-References  
- [[SemanticChunker]] — used in the first pass for topic‑aware splitting.  
- [[RecursiveChunker]] — used in the second pass for size enforcement.  
- [[IChunker]] — the interface this class implements.  
- [[IEmbedder]] — optional dependency for semantic awareness.  
- [[DocumentChunk]] — the output data model.  

## Source Files  
- `AGXIOME.Storage/Chunkers/HybridChunker.cs` (ingested 2026-05-14)