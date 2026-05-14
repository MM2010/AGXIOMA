---
title: F12 Web Social Tests
category: entity
tags: [tests, websocial, toolkit, twitter, reddit, linkedin, instagram, youtube, tiktok, medium, devto, hashnode]
source_count: 10
created_at: 2026-05-14
updated_at: 2026-05-14
---

# F12 Web Social Tests

## Summary
This source file contains unit tests for all WebSocial toolkits in the `AGXIOME.Toolkits.WebSocial` namespace under the F12 version. Each test class verifies the toolkit's identity (Id, Name, Category), that an unconfigured execution returns `Skipped`, and that a minimally configured execution returns `Ok`. These tests ensure consistent baseline behavior across all social platform adapters.

## Key Details
- Tested toolkits: Twitter, Reddit, LinkedIn, Instagram, YouTube, TikTok, Medium, Dev.to, Hashnode (and others truncated).
- All toolkits share `ToolkitCategory.WebSocial`.
- Common test pattern: `Id_IsCorrect`, `Name_IsCorrect`, `Category_IsCorrect`, `NotConfigured_ReturnsSkipped`, `Execute_WithConfig_ReturnsOk`.
- Configuration is provided via `ToolkitConfig` with a dictionary of keys (e.g., `"api_key"`).
- Execution uses dummy input parameters (e.g., `"text": "hello"` for Twitter) and returns `ToolkitResultStatus.Ok`.
- All tests are `[Fact]` and `async Task`.
- Source file located in `AGXIOME.Tests/F12/`.

## Cross-References
- [[f12toolkits]] — base toolkit infrastructure used by these adapters
- [[websocialtoolkits]] — overview of all web social toolkits in the framework
- [[toolkitcategory]] — definition of `ToolkitCategory.WebSocial`

## Source Files
- `AGXIOME.Tests/F12/F12WebSocialTests.cs` (ingested 2026-05-14)