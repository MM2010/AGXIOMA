---
title: F8 Tools Infrastructure Tests
category: entity
tags: [tests, tools, infrastructure, caching, rate-limiting, filtering, stop-conditions]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# F8 Tools Infrastructure Tests

## Summary
This test suite validates the core infrastructure components of the F8 tools module: caching, call rate limiting, stop-after-tool evaluation, and tool filtering. Seven tests per component ensure correctness and edge case coverage for `ToolResultCache`, `ToolCallLimiter`, `StopAfterToolEvaluator`, and `ToolFilter`. The suite is part of the AGXIOME .NET agent framework's testing infrastructure.

## Key Details
- **ToolResultCache** (TI-01): Tests for set/get, miss, expiry, invalidation, cache clearing, hit/miss stats, and parameter-variant keying.
- **ToolCallLimiter** (TI-02): Tests for initial allowance, success/error counting, consecutive error reset, total call limit, consecutive error limit, and per-tool call limit enforcement.
- **StopAfterToolEvaluator** (TI-03): Tests for no-rule behavior, stop modes (`AfterAnyCall`, `AfterSuccess`, `AfterError`, `Never`), rule removal, and rule listing.
- **ToolFilter** (TI-04): Tests for allowlist/blocklist filtering, session-specific override, and filter reset.
- All tests use NSubstitute for mocking and xUnit as the test framework.
- The suite implements the strategy of 7 test cases per task (TI-01 to TI-06; TI-05 and TI-06 are not shown in the ingested snippet).

## Cross-References
- [[ToolResultCache]] — the caching component tested here
- [[ToolCallLimiter]] — the rate limiting component tested here
- [[StopAfterToolEvaluator]] — the run stopping logic component tested here
- [[ToolFilter]] — the tool filtering component tested here
- [[F8Tools]] — the parent module that uses these infrastructure pieces

## Source Files
- `AGXIOME.Tests/F8ToolsInfrastructureTests.cs` (ingested 2026-05-14)