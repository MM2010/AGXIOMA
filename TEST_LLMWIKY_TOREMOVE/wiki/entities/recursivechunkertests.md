---
title: RecursiveChunkerTests
category: entity
tags: [test, chunker, recursive, unit-test]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# RecursiveChunkerTests

## Summary
The `RecursiveChunkerTests` class provides a unit test suite for the [[RecursiveChunker]] component within the AGXIOMA framework. It validates core chunking behaviors including separator-based splitting, hard boundary fallback, overlap propagation, and chunk indexing, ensuring correctness and data integrity across various input scenarios.

## Key Details
- Test class targets `RecursiveChunker`, which implements `IChunker` and returns `ChunkerType.Recursive`.
- Five test methods cover empty input, paragraph boundaries, long unseparated text, overlap handling, and sequential chunk indexes.
- `EmptyInput_ReturnsSingleEmptyChunk` verifies that an empty string yields exactly one chunk with empty content.
- `ParagraphBoundary_SplitsOnDoubleNewline` ensures that text separated by `\n\n` is split into at least two chunks.
- `LongTextWithNoSeparator_SplitsOnHardBoundary` checks hard character-limit splitting with no data loss (total length preserved).
- `Overlap_PreviousTailAppearedInNextChunk` confirms that overlap tokens from a previous chunk appear at the start of the next chunk and that chunk indexes are monotonic.
- `ChunkIndexes_AreSequential` asserts that generated chunks have zero-based sequential `ChunkIndex` values.
- All tests use `maxTokens` and `overlap` parameters passed directly to `Chunk()`.
- The test file resides in the `AGXIOME.Tests` project and uses xUnit.

## Cross-References
- [[RecursiveChunker]] — the chunker implementation being tested
- [[ChunkerType]] — enumeration used to identify chunker strategy
- [[ChunkResult]] — output model with `Content` and `ChunkIndex` properties

## Source Files
- `AGXIOME.Tests/RecursiveChunkerTests.cs` (ingested 2026-05-14)