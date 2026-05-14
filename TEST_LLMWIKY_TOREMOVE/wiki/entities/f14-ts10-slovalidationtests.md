---
title: F14 TS-10: SLO Validation Tests
category: entity
tags: [tests, SLO, error budget, latency, validation, F14]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# F14 TS-10: SLO Validation Tests

## Summary

TS-10 validates that the AGXIOMA framework meets its **Service Level Objectives (SLOs)** for error rates, latency budgets, and retry exhaustion behavior across critical paths. It comprises 20 tests split between `TS-10-A` (error rate thresholds, 7 tests) and `TS-10-B` (latency budget compliance, 7 tests), with additional retry exhaustion scenarios. The tests exercise storage providers (SQLite, JsonFile), chunking strategies (SentenceChunker, SlidingWindowChunker, etc.), and operation patterns (save, delete, concurrency) to ensure the framework stays within defined reliability and performance bounds.

## Key Details

- **Error rate thresholds**: Requires zero errors for 100+ operations on `SqliteCommandStore`, `SlidingWindowChunker`, and all built-in chunkers (including empty input). Concurrent saves on `JsonFileCommandStore` allow < 5% error rate (≤2 errors out of 50).
- **Latency budget compliance**: Asserts p95 latency < 200ms for SQLite delete, `SentenceChunker` (100 ops), `SlidingWindowChunker` (50 ops), `SqliteCommandStore` save, and `JsonFileCommandStore` save (100 ops each).
- **Retry exhaustion behavior**: Tests (not fully shown in source) validate that the system gracefully handles repeated failures without cascading errors.
- **Other validations**: `StorageProviderType` enum parsing never throws; chunk results never contain null `Content` over 100 iterations.
- All tests are in the `AGXIOME.Tests.F14` namespace and belong to the F14 test suite.

## Cross-References

- [[F14 Baseline Tests]] — core functional tests that complement these SLO validations
- [[SqliteCommandStore]] — primary storage provider tested for error rate and latency
- [[Chunker Implementations]] — all chunker types covered in SLO tests
- [[SLO Framework]] — definition of AGXIOMA service level objectives and error budget policy

## Source Files

- `AGXIOME.Tests/F14/F14_TS10_SloValidationTests.cs` (ingested 2026-05-14)