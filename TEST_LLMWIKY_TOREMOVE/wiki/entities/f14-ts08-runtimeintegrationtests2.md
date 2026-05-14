---
title: F14 Ts08 Runtimeintegrationtests2
category: entity
tags: [runtime-integration-tests, security, dto, content-type, tests]
source_count: 30
created_at: 2026-05-14
updated_at: 2026-05-14
---

# F14 Ts08 Runtimeintegrationtests2

## Summary
This page documents the second part of TS-08 runtime integration tests (30 tests), which supplements the main integration test suite to reach a target of 133 tests. It covers three areas: content-type and serialization (10 tests), DTO record correctness (10 tests), and security header assertions (10 tests). The tests validate that API endpoints return JSON, DTO records are correctly implemented, and security headers like `X-Content-Type-Options` and `X-Frame-Options` are present.

## Key Details
- **TS-08-J (Content-type and serialization)**: Verifies all major `/api/*` endpoints return `application/json`; tests for wrong content type (returns 415 or 400), health check JSON, and 404 responses.
- **TS-08-K (DTO record correctness)**: Asserts equality, `with` mutation, non-null status fields, default values (e.g., `IsActive` defaults true), and proper storage of properties on DTO records (`AgentDto`, `RunDto`, `SessionDto`, `TeamDto`, `WorkflowDto`, `ToolDto`, `ErrorResponse`, `CreateAgentRequest`, `StartRunRequest`).
- **TS-08-L (Security header assertions)**: Checks that the `/health` endpoint includes `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`, and no CORS wildcard; also verifies unauthorized responses return an error code.
- Tests use `WebApplicationFactory<Program>` and an `AdminClient` helper for authenticated requests.
- The source file is organized into three `public sealed class` test fixtures.

## Cross-References
- [[F14 Ts08 RuntimeIntegrationTests]] — the main TS-08 integration test file that this supplements
- [[AGXIOMA.Security.ApiKey]] — security component used for API key authentication in tests
- [[AGXIOME.Tests]] — root test namespace containing all framework tests

## Source Files
- `AGXIOME.Tests/F14/F14_TS08_RuntimeIntegrationTests2.cs` (ingested 2026-05-14)