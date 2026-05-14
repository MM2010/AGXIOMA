---
title: F13 CLI & Multi-Framework Tests
category: entity
tags: [CLI, tests, multi-framework, run command, list command, session command, admin command, config command, control plane, approval UI, LangChain, Semantic Kernel, Claude]
source_count: 1
created_at: 2025-05-14
updated_at: 2025-05-14
---

# F13 CLI & Multi-Framework Tests

## Summary
This source file contains 43 unit tests that validate the AGXIOME CLI commands and their integration with multiple external frameworks. Organized across 10 requirement modules (MU-01 through MU-10), it verifies command execution, argument validation, server connectivity, and correct API calls for `RunCommand`, `ListCommand`, `SessionCommand`, `AdminCommand`, `ConfigCommand`, control plane, approval UI, LangChain, Semantic Kernel, and Claude bridge. The tests use **NSubstitute** for mocking and a consistent `Build` helper pattern to create test fixtures.

## Key Details
- **Total tests**: 43, covering modules MU-01 through MU-10.
- **Modules and test counts**:
  - MU-01 RunCommand (5 tests)
  - MU-02 ListCommand (4 tests)
  - MU-03 SessionCommand (4 tests)
  - MU-04 AdminCommand (4 tests)
  - MU-05 ConfigCommand (4 tests)
  - MU-06 ControlPlane (5 tests)
  - MU-07 ApprovalUI (5 tests)
  - MU-08 LangChain (4 tests)
  - MU-09 SemanticKernel (4 tests)
  - MU-10 Claude (4 tests)
- Each test class uses a private `Build` method that substitutes `IAgxiomeApiClient` and `StringCliOutput`, controlling reachability and mock responses.
- Tests verify both success and failure paths: missing required arguments (`--id`, `--type`), server unreachable (exit code 2), and correct endpoint calls per action.
- For CLI commands, tests check that the correct API endpoints are invoked (e.g., `api/agents`, `api/teams`, `api/admin/health`).
- For multi-framework modules (LangChain, Semantic Kernel, Claude), tests validate that the bridge methods are called with expected parameters and return appropriate results.
- The control plane tests ensure proper delegation to `IControlPlane` methods and handle cancellation/error states.
- Approval UI tests verify the `RequestApproval` / `ConfirmApproval` flow with mocked UI components.
- All tests are `async Task` and use `xUnit` `[Fact]` attributes.

## Cross-References
- [[F02clicommands]] — Defines the CLI commands tested in this file.
- [[F03runtimeui]] — The `StringCliOutput` and UI components used for output capture.
- [[X01langchainbridge]] — The LangChain bridge module (MU-08).
- [[X02semantickernelbridge]] — The Semantic Kernel bridge module (MU-09).
- [[X03claudebridge]] — The Claude bridge module (MU-10).
- [[F12controlplane]] — The control plane logic tested in MU-06.
- [[F14approvalui]] — The approval UI components tested in MU-07.

## Source Files
- `AGXIOME.Tests/F13/F13CliMultiFrameworkTests.cs` (ingested 2025-05-14)