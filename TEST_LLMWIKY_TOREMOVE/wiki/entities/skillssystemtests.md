---
title: SkillsSystemTests
category: entity
tags: [testing, skills, system-tests]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# SkillsSystemTests

## Summary
Integration and unit tests that validate the AGXIOMA skills system end-to-end: building skills via `AgxiomeSkillBuilder`, managing them with `SkillManager`, binding skills to agents, composing skills via dependencies, and parsing YAML frontmatter from skill definition files. These tests ensure the core skill lifecycle works correctly, from creation to agent application.

## Key Details
- Covers `AgxiomeSkillBuilder` for creating skills with ID, name, description, category, tools, and required dependencies.
- Validates `SkillManager` registration, retrieval by ID, listing by category, and unregistration.
- `ApplySkillToAgent` registers skill tools on an agent; throws `KeyNotFoundException` for unknown skills.
- Skill composition via `Requires` aggregates tools from prerequisite skills.
- `LocalSkillsLoader.ParseFrontmatter` correctly extracts YAML frontmatter from file content and handles missing or empty content.
- Uses a `MockCodeReviewTool` (extending `AgxiomeFunction`) as a test dummy tool.
- Tests confirm proper tool type name registration via `AddTool<T>()`.
- `AgentFactory.CreateTestAgent` used to simulate agent instances for binding tests.

## Cross-References
- [[AgxiomeSkillBuilder]] — the builder class tested for skill creation.
- [[SkillManager]] — the manager class tested for registration, retrieval, and agent binding.
- [[LocalSkillsLoader]] — the loader whose frontmatter parsing is tested.
- [[AgxiomeFunction]] — base class for the mock tool used in tests.
- [[AgentFactory]] — used to create test agents.

## Source Files
- `AGXIOME.Tests/SkillsSystemTests.cs` (ingested 2026-05-14)