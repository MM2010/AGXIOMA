---
title: F14 Ts08 Runtimeintegrationtests
category: entity
tags: [tests, integration, runtime, agent, run]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# F14 Ts08 Runtimeintegrationtests

## Summary
`F14_TS08_RuntimeIntegrationTests` extends the runtime integration test suite beyond the foundational `F11RuntimeTests`. It contains 133 tests that verify additional API endpoints and runtime behaviors for the Agent and Run controllers. Tests are executed using `WebApplicationFactory<Program>` for full HTTP-level integration, with a shared helper that stubs the `IApiKeyStore` to simulate an admin user, ensuring consistent authorization across all test scenarios.

## Key Details
- Test file: `AGXIOME.Tests/F14/F14_TS08_RuntimeIntegrationTests.cs`
- Contains two main test classes: `TS08_AgentControllerExtendedTests` (15 tests) and `TS08_RunControllerExtendedTests` (12 tests; additional classes were truncated in source)
- Helper class `F14RuntimeHelper` provides `AdminClient()` method that sets up a factory with a mocked `IApiKeyStore` that accepts any `X-Api-Key` header as an admin role.
- Covers CRUD operations on agents: create, read, update, delete, clone, and error cases (non-existent, empty name, unauthorized).
- Run controller tests (partially shown) verify listing runs and starting runs (202 Accepted).
- No-auth requests are asserted to return 401 Unauthorized.
- Tests rely on `IClassFixture<WebApplicationFactory<Program>>` for lifecycle management.

## Cross-References
- [[F11-RuntimeTests]] — sibling integration test suite with foundational endpoint coverage.
- [[AgentController]] — the controller under test in `TS08_AgentControllerExtendedTests`.
- [[RunController]] — the controller under test in `TS08_RunControllerExtendedTests`.
- [[ApiKeySecurity]] — security middleware that enforces the API key validation stubbed by the test helper.

## Source Files
- `AGXIOME.Tests/F14/F14_TS08_RuntimeIntegrationTests.cs` (ingested 2026-05-14)