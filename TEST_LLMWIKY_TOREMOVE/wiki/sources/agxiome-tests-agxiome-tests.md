---
title: Agxiome Tests Agxiome Tests
category: source
tags: [testing, xunit, testcontainers, nsubstitute, fsharp]
source_count: 0
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Agxiome Tests Agxiome Tests

## Summary
The central test project for the AGXIOME agent framework, providing unit, integration, and property-based tests for all framework components. It uses xUnit as the test runner, NSubstitute for mocking, FsCheck for property-based testing, and Testcontainers for PostgreSQL and Redis integration tests, with code coverage via coverlet.

## Key Details
- Built with .NET 10.0 and SDK-style project format.
- References all major AGXIOME projects (11 project references).
- Test frameworks: xUnit (2.9.3), NSubstitute (5.3.0), FsCheck (3.3.3).
- Integration testing: Testcontainers.PostgreSql (4.11.0), Testcontainers.Redis (4.11.0), Microsoft.AspNetCore.Mvc.Testing (10.0.8).
- Code coverage: coverlet.collector (10.0.0) with build-time only collection.
- Uses `Microsoft.Graph` and `Kiota.Abstractions` for Microsoft Graph integration tests.
- Explicitly sets `<IsPackable>false</IsPackable>` as a test project.
- Global using of `Xunit` simplifies test file boilerplate.

## Cross-References
- [[AGXIOME.Shared]] — foundational shared types tested here
- [[AGXIOME.Core]] — core logic tests
- [[AGXIOME.LLMWIKY]] — LLM WIKI component tests
- [[AGXIOME.Storage]] — storage abstraction tests
- [[AGXIOME.XLink]] — cross-link tests
- [[AGXIOME.Realtime]] — realtime communication tests
- [[AGXIOME.Security]] — security module tests
- [[AGXIOME.Telemetry]] — telemetry and monitoring tests
- [[AGXIOME.Runtime]] — runtime execution tests
- [[AGXIOME.Toolkits]] — toolkit integration tests
- [[AGXIOME.CLI]] — CLI command tests
- [[AGXIOME.Orchestration]] — orchestration and workflow tests

## Source Files
- `AGXIOME.Tests/AGXIOME.Tests.csproj` (ingested 2026-05-14)