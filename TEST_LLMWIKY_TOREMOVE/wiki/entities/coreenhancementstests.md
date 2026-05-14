---
title: Core Enhancement Tests
category: entity
tags: [tests, core, enhancements, unit-tests]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Core Enhancement Tests

## Summary
This test file validates the core enhancements in the AGXIOMA framework, covering background job execution (`BackgroundRunWorker`), run cancellation management (`CancellationManager`), and remote agent communication (`RemoteAgent`). It ensures that the core infrastructure for asynchronous run scheduling, cancellation, and HTTP-based agent interaction behaves correctly under various conditions, including error handling and state transitions.

## Key Details
- **BackgroundRunWorkerTests (10 tests)**: Verifies job enqueuing, state retrieval, job record values, terminal statuses (completed, cancelled, failed), empty job list default, cancellation of unknown jobs, and enumeration consistency for `BackgroundJobType` and `BackgroundJobStatus`.
- **CancellationManagerTests (8 tests)**: Tests token registration, cancellation of existing and unknown runs, active state tracking, removal of runs, `CancelAll` behavior, and reflection of active run IDs.
- **RemoteAgentTests (8 tests)**: Validates constructor with base URL, successful `RunAsync` response, HTTP error throwing, default timeout (60s), custom timeout storage, factory creation, null options argument exception, and null HTTP client argument exception (last test truncated).
- Uses NSubstitute for mocking `IBackgroundRunWorker` and a custom `TestHttpMessageHandler` for `HttpClient`.
- All tests are synchronous (fact) except `RunAsync` tests which are async.

## Cross-References
- [[BackgroundRunWorker]] — interface tested in BackgroundRunWorkerTests
- [[CancellationManager]] — class tested in CancellationManagerTests
- [[RemoteAgent]] — class tested in RemoteAgentTests
- [[RemoteAgentOptions]] — configuration object used by RemoteAgent

## Source Files
- `AGXIOME.Tests/CoreEnhancementsTests.cs` (ingested 2026-05-14)