---
title: F14 TS11 Property Based Tests
category: entity
tags: [property-based testing, chunker, store, deterministic randomization]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# F14 TS11 Property Based Tests

## Summary
Extended coverage property-based tests for the AGXIOME .NET agent framework, comprising 40 deterministic tests organized into two groups: TS-11-A (chunker output coverage, monotonicity, idempotency, invariants) and TS-11-B (store data invariants). Uses manual seeded random generation (`Random(42)`) to ensure reproducibility. These tests supplement the existing 10 property-based tests to reach a target of 50.

## Key Details
- **Chunker properties (10 tests)**: Verify chunk count ≥ 1, non-null content, ascending indexes, word coverage, empty input yields exactly 1 chunk, larger maxTokens produces ≤ chunks (monotonicity), deterministic output, overlap never reduces chunk count, token count within bounds, single-word text produces one chunk.
- **Store properties (remaining tests from source)**: SaveAsync never throws for valid records, DeleteAsync never throws for missing IDs, Save+Delete maintains consistency, etc. (truncated in source).
- All tests use `Random(42)` for deterministic pseudo-random text generation across trials.
- Covers five chunker implementations: `SentenceChunker`, `RecursiveChunker`, `SlidingWindowChunker`, `FixedSizeChunker`, `HeadingChunker`.
- Source file: `AGXIOME.Tests/F14/F14_TS11_PropertyBasedTests.cs` (ingested 2026-05-14).
- Part of test suite F14 targeting property-based validation of core framework components.

## Cross-References
- [[F14]] — Parent test suite containing comprehensive property-based tests.
- [[Chunker]] — Target of TS-11-A properties (sliding window, recursive, sentence, fixed size, heading chunkers).
- [[Store]] — Target of TS-11-B properties (SQLite command store invariants).
- [[PropertyBasedTests]] — Existing 10 property-based tests extended by this TS-11.

## Source Files
- `AGXIOME.Tests/F14/F14_TS11_PropertyBasedTests.cs` (ingested 2026-05-14)