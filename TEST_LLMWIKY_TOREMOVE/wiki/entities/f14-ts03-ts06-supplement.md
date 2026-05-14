---
title: F14 TS03 TS06 Supplement
category: entity
tags: [f14, tests, supplement, storage, embedder]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# F14 TS03 TS06 Supplement

## Summary
This page documents the supplement test sets for F14 TS-03 and TS-06, adding 15 and 11 tests respectively to meet coverage targets for database storage provider and embedder scenarios. The tests validate edge cases in `SqliteCommandStore` (schema creation, extreme values, dispose safety) and verify `StorageProviderType` enum completeness. For embedders, they check custom dimensions/models, default model strings, and error handling for empty API responses.

## Key Details
- **TS-03 supplement**: 15 tests covering `SqliteCommandStore` schema auto-creation, large/negative/zero values, multiple dispose calls, and `StorageProviderType` enum containing at least 20 values including SQLite, PostgreSQL, MongoDB, Redis, DynamoDB, CosmosDB, JsonFile, SQLServer, MySQL.
- **TS-06 supplement**: 11 tests for Anthropic, Cohere, Gemini, Mistral, Voyage, and HuggingFace embedders; validates custom options, default model non-empty, and handling of empty HTTP response bodies.
- Edge cases tested: `int.MinValue`, `int.MaxValue`, zero, empty input, `HttpStatusCode.OK` with empty JSON.
- All tests reside in namespace `AGXIOME.Tests.F14`.

## Cross-References
- [[entities/f14]] — Main F14 entity; this supplement extends its test suites.
- [[entities/f14-ts03]] — TS-03 test suite (DB store scenarios)
- [[entities/f14-ts06]] — TS-06 test suite (embedder scenarios)

## Source Files
- `AGXIOME.Tests/F14/F14_TS03_TS06_Supplement.cs` (ingested 2026-05-14)