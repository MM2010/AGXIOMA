---
title: F14 TS-09: Benchmark/Performance Tests
category: entity
tags: [benchmarks, performance, chunkers, stores, tests, agxiome]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# F14 TS-09: Benchmark/Performance Tests

## Summary

TS-09 is a suite of 30 performance regression tests for the AGXIOME .NET agent framework. These tests use `Stopwatch` to assert that hot-path operations — text chunking and store persistence — complete within defined time budgets (SLOs). They are not replacements for micro-benchmarks but serve as CI guards against catastrophic slowdowns caused by code changes or environment issues.

The suite is split into two groups: **TS-09-A** (10 tests) validates chunker throughput across five chunker implementations, and **TS-09-B** (10 tests, at least) validates store serialisation budgets for SQLite and JSON file stores. Some store tests use deletes as a proxy for reads because the `ICommandStore` interface lacks `GetAsync`.

## Key Details

- **30 tests total:** 10 for chunker throughput, 10+ for store serialisation (source truncated; at least 20 described).
- **All tests use `System.Diagnostics.Stopwatch`** for timing assertions, not a full benchmarking library.
- **Chunker SLOs:** chunking 10 KB of text must finish in under 500 ms (generous for CI); smaller inputs have tighter budgets (e.g., 100 ms for 1 KB, 10 ms for empty input in a loop).
- **Chunkers tested:** `SentenceChunker`, `RecursiveChunker`, `SlidingWindowChunker`, `FixedSizeChunker`, `HeadingChunker` — all from the `AGXIOME.Storage.Chunkers` namespace.
- **Store SLOs:** 100 SQLite saves must complete in under 10 s; 50 JSON file saves in under 15 s (budgets raised to accommodate parallel CI load).
- **Store tests use temporary files** (`Path.GetTempPath()`) and clean up after themselves, but `JsonFileCommandStore` requires a directory-based store.
- **Store “reads” are measured via deletes,** because `ICommandStore` exposes `DeleteAsync` instead of `GetAsync`; this is a workaround for round-trip verification.
- Tests are collected under `[Collection("Benchmarks")]` to allow parallel execution control.

## Cross-References

- [[AGXIOME.Storage.Chunkers.SentenceChunker]] — Implementation tested in TS-09-A.
- [[AGXIOME.Storage.Stores.SqliteCommandStore]] — Implementation tested in TS-09-B.
- [[AGXIOME.Storage.Stores.JsonFileCommandStore]] — Implementation tested in TS-09-B.
- [[F14-Test-Suite]] — Parent test feature (F14) containing this benchmark suite.
- [[AGXIOME.Shared.Interfaces.IChunker]] — Interface that all chunkers implement.
- [[AGXIOME.Shared.Interfaces.ICommandStore]] — Interface for stores; missing `GetAsync` affects test design.

## Source Files

- `AGXIOME.Tests/F14/F14_TS09_BenchmarkTests.cs` (ingested 2026-05-14)