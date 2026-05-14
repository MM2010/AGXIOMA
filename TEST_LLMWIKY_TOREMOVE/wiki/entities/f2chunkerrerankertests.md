---  
title: F2 Chunker & Reranker Tests  
category: entity  
tags: [tests, chunker, reranker, unit-tests]  
source_count: 1  
created_at: 2026-05-14  
updated_at: 2026-05-14  
---  

# F2 Chunker & Reranker Tests  

## Summary  
This source file contains the unit test suite for the chunking and reranking components of the AGXIOME framework. It covers five chunker implementations (AgenticChunker, CodeChunker, DocumentChunker, CsvRowChunker, JsonPathChunker) and, based on the file name, likely also includes reranker tests. Each chunker test class validates core behaviors: correct `ChunkerType` property, handling of empty input, content boundary splitting, sequential or monotonic chunk indexing, LLM integration (for agentic chunking), header prepending (CSV), and fallback strategies when token budgets are exceeded.  

## Key Details  
- File name: `AGXIOME.Tests/F2ChunkerRerankerTests.cs`  
- Contains 5+ test classes (AgenticChunkerTests, CodeChunkerTests, DocumentChunkerTests, CsvRowChunkerTests, JsonPathChunkerTests) each with 5 tests (25+ tests total).  
- Tests use NSubstitute for mocking and xUnit as the test framework.  
- Common patterns tested across chunkers:  
  - `ChunkerType` returns the correct enum value.  
  - Empty or whitespace input produces a single empty chunk.  
  - Large content is split without data loss.  
  - Chunk indices are sequential and monotonically increasing.  
  - Overlap (where applicable) ensures rows or content appear in successive chunks.  
- The AgenticChunker test validates LLM-parsed boundary arrays and fallback to recursive chunking when no LLM is provided.  
- CodeChunker tests split C# code on class boundaries and fall back gracefully on plain text.  
- DocumentChunker tests split on markdown headings and break oversized sections by paragraph.  
- CsvRowChunker tests verify that the CSV header is prepended to every chunk and that overlap rows are carried into the next chunk.  
- JsonPathChunker (in the truncated portion) tests path-based chunking of JSON structures.  

## Cross-References  
- [[AgenticChunker]] — the chunker tested in AgenticChunkerTests  
- [[CodeChunker]] — the chunker tested in CodeChunkerTests  
- [[DocumentChunker]] — the chunker tested in DocumentChunkerTests  
- [[CsvRowChunker]] — the chunker tested in CsvRowChunkerTests  
- [[JsonPathChunker]] — the chunker tested in JsonPathChunkerTests  
- [[RerankerOverview]] — reranker tests likely included in the same file  

## Source Files  
- `AGXIOME.Tests/F2ChunkerRerankerTests.cs` (ingested 2026-05-14)