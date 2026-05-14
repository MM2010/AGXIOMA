---
title: F14 BenchmarkCollectionDefinition
category: entity
tags: [benchmark, collection, xUnit, AGXIOME]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# F14 BenchmarkCollectionDefinition

## Summary
The `BenchmarkCollection` class is an xUnit collection definition that groups benchmark tests within the `F14` test suite. By applying the `[CollectionDefinition("Benchmarks")]` attribute, it ensures that all tests in this collection execute sequentially rather than in parallel, preventing resource contention and producing stable, reproducible timing measurements.

## Key Details
- Defined as a sealed `public class` inside the `AGXIOME.Tests.F14` namespace.
- Decorated with `[CollectionDefinition("Benchmarks")]` — the collection name used by test classes to opt into sequential execution.
- No members or logic are needed; the class itself acts solely as a marker for the xUnit collection infrastructure.
- Sequential execution prevents interference from sibling benchmark classes, yielding more accurate performance results.
- This pattern follows the standard xUnit technique for isolating parallel-unsafe tests.

## Cross-References
- [[F14 Benchmark Tests]] — Tests that reference this collection definition to run sequentially.

## Source Files
- `AGXIOME.Tests/F14/F14_BenchmarkCollectionDefinition.cs` (ingested 2026-05-14)