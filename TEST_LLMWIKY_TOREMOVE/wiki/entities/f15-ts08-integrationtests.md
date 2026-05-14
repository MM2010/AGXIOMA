---
title: F15-TS08 Integration Tests
category: entity
tags: [integration-tests, orchestration, hotkeys, di-registration, pipeline]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# F15-TS08 Integration Tests

## Summary
This test suite validates the end-to-end integration of the save–notify–poll pipeline (using in-memory stores), enforces HotKeys naming uniqueness across task/plan identifiers, and confirms that the DI extension `AddAgxiomeOrchestration` registers all required stores. Comprising 10 tests across three logical groups, it serves as the primary regression barrier for the orchestration core.

## Key Details
- **Test groups**: HotKeys uniqueness (3 tests), save–notify–poll pipeline (4 tests), DI extension sanity (3 tests).
- **HotKeys tests** verify that `TaskResult`/`TaskStatus`/`TaskJobId` produce different keys for different task IDs, same for plan IDs, and that result vs. status keys have different prefixes.
- **Pipeline tests** exercise `SaveResultTool` → `NotifyCompletionTool` → `PollResultsTool` (via `ICompletionNotifier.SubscribeAsync`) with both success and failure cases, a timeout scenario, and cold‑store persistence.
- **DI tests** confirm that `AddAgxiomeOrchestration()` registers `IHotStore` and `IOrchestrationStore` as resolvable services.
- All tests use in-memory implementations (`InMemoryHotStore`, `InMemoryOrchestrationStore`, `InMemoryCompletionNotifier`) and get a fresh state per test.
- The timeout test verifies that `PollResultsTool` succeeds even when no result is published, reporting timed‑out tasks in its output.

## Cross-References
- [[HotKeys]] — naming conventions validated by the uniqueness tests.
- [[SaveResultTool]] — first step of the integration pipeline.
- [[NotifyCompletionTool]] — second step that triggers notification.
- [[PollResultsTool]] — final step that consumes results.
- [[InMemoryStores]] — backing infrastructure for all integration tests.
- [[DI Extension Registration]] — registration pattern confirmed by the DI tests.

## Source Files
- `AGXIOME.Tests/F15/F15_TS08_IntegrationTests.cs` (ingested 2026-05-14)