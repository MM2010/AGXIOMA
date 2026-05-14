---
title: F14 Ts07 ChunkerRerankerTests
category: entity
tags: [chunker, reranker, tests, F14]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# F14 Ts07 ChunkerRerankerTests

## Summary
This test suite (TS-07) provides extended validation for all chunker types (Sentence, Recursive, SlidingWindow, Heading, FixedSize) and reranker implementations. It covers overlap constraints, max tokens enforcement, boundary edge cases, reranker scoring, and chunk index sequencing. The suite ensures that each chunker correctly reports its `ChunkerType`, handles empty/whitespace input, enforces token limits, and produces non-empty chunks. Reranker tests validate priority-based and threshold-based filtering, scoring normalization, and integration with chunker output.

## Key Details
- Contains 40+ tests across six sub-groups: TS-07-A through TS-07-F.
- TS-07-A: SentenceChunker extended (6 tests) — verifies type, single-sentence, chunk index sequencing, max tokens limit, overlap constraints.
- TS-07-B: RecursiveChunker extended (6 tests) — verifies type, empty/whitespace input, multi-paragraph splitting, non-empty chunks, large overlap.
- TS-07-C: SlidingWindowChunker extended (5 tests) — verifies type, empty/single-word input, word count, content preservation.
- TS-07-D: HeadingChunker extended (4 tests) — verifies type, no-headings fallback, splits at H1, empty markdown.
- TS-07-E: FixedSizeChunker extended (4 tests) — verifies type, empty input, multi-chunk splitting, non-empty chunks.
- TS-07-F: Rerankers extended (15 tests) — covers reranker scoring, priority, threshold filtering, edge cases.
- Tests use NSubstitute for mocking where needed, but most chunker tests instantiate concrete chunkers directly.

## Cross-References
- [[entities/F02-chunkerrerankertests]] — earlier, more basic chunker/reranker tests (F2)
- [[concepts/chunker]] — overview of chunker types and usage
- [[concepts/reranker]] — reranker responsibilities and scoring
- [[entities/F14-index]] — index page for F14 test suite

## Source Files
- `AGXIOME.Tests/F14/F14_TS07_ChunkerRerankerTests.cs` (ingested 2026-05-14)