---
title: F12 Communication Tests
category: entity
tags: [tests, communication, toolkits, F12]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# F12 Communication Tests

## Summary
This test file validates the core properties and execution behavior of all communication-oriented toolkits in the F12 release batch. Each toolkit (Slack, Discord, Telegram, WhatsApp, Email SMTP, Gmail, Microsoft Teams, Zoom, Google Chat, etc.) is verified for correct identity (`Id`, `Name`), category assignment (`ToolkitCategory.Communication`), and proper handling of configured vs. unconfigured states. The tests confirm that unconfigured toolkits return `Skipped` status and configured ones return `Ok` status when executing typical operations like sending a message or creating a meeting.

## Key Details
- Tests cover 9+ communication toolkits: Slack, Discord, Telegram, WhatsApp, Email SMTP, Gmail, Microsoft Teams, Zoom, Google Chat.
- Each toolkit has five standardized test methods: `Id_IsCorrect`, `Name_IsCorrect`, `Category_IsCorrect`, `NotConfigured_ReturnsSkipped`, `Execute_WithConfig_ReturnsOk`.
- All toolkits share the same pattern: they belong to `ToolkitCategory.Communication` and expose a unique string ID (e.g., `"slack"`, `"discord"`, `"gmail"`).
- Unconfigured toolkits (no `ToolkitConfig` provided) return `ToolkitResultStatus.Skipped` for any operation.
- Configured toolkits (with a minimal `ToolkitConfig` containing a dummy `api_key` or `token`) return `ToolkitResultStatus.Ok` for valid operation parameters.
- Operation-specific parameters vary per toolkit (e.g., `channel`/`message` for Slack, `to`/`subject`/`body` for email).
- Tests are asynchronous and use `xUnit` `[Fact]` attributes.
- Source file is located in `AGXIOME.Tests/F12/` namespace `AGXIOME.Tests.F12`.

## Cross-References
- [[CommunicationToolkits]] — Parent concept for all communication-based toolkits.
- [[F12]] — Release batch containing these toolkit tests.

## Source Files
- `AGXIOME.Tests/F12/F12CommunicationTests.cs` (ingested 2026-05-14)