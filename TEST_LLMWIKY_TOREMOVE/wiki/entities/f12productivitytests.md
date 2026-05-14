---
title: F12 Productivity Tests
category: entity
tags: [tests, productivity, google, onedrive, notion, confluence, jira]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# F12 Productivity Tests

## Summary
This source file contains unit tests for all productivity toolkits in the AGXIOMA framework. Each toolkit is validated for its identifier, display name, category assignment (`ToolkitCategory.Productivity`), correct behavior when unconfigured (returns `Skipped`), and successful execution with a mock configuration.

## Key Details
- Tests cover eight productivity toolkits: Google Calendar, Google Drive, Google Docs, Google Sheets, OneDrive, Notion, Confluence, and Jira.
- Every toolkit test class verifies `Id`, `Name`, and `Category` properties.
- Each toolkit's unconfigured `ExecuteAsync` call returns `ToolkitResultStatus.Skipped`.
- Configured execution tests use a `ToolkitConfig` with a dummy `api_key` and standard parameters to assert `ToolkitResultStatus.Ok`.
- Tests reside in the `AGXIOME.Tests.F12` namespace and are marked with `[Fact]`.

## Cross-References
- [[ToolkitCategory]] — defines the `Productivity` category used by all tested toolkits
- [[ToolkitConfig]] — configuration class used by tests to simulate setup
- [[F12]] — namespace containing these and related toolkit tests

## Source Files
- `AGXIOME.Tests/F12/F12ProductivityTests.cs` (ingested 2026-05-14)