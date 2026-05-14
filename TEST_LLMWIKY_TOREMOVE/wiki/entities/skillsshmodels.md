---
title: Skills.sh Models
category: entity
tags: [skills, models, enums, records, SkillType, SkillStatus]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Skills.sh Models

## Summary
This page defines the core model types used to represent skills imported from the Skills.sh community repository. It includes the `SkillType` enum that classifies a skill as an HTTP API, C# function, or LLM prompt; the `SkillDefinition` record that holds all metadata of an imported skill; the `SkillStatus` enum tracking trust level per user; and the `UserSkillEntry` record that persists an imported skill for a specific user.

## Key Details
- `SkillType` enum: `HttpApi` (HTTP endpoint, no compilation), `CSharpFunction` (embedded C# compiled via Roslyn), `Prompt` (pure LLM prompt, no compilation).
- `SkillDefinition` record: fields `Id`, `Name`, `Description`, `Type` (SkillType), optional `Endpoint`, `SourceCode`, and `Parameters` (dictionary).
- `SkillStatus` enum: `Trusted`, `Pending`, `Blocked` – used to control access to imported skills per user.
- `UserSkillEntry` record: links a user (`UserId`) to a `SkillDefinition` via `SkillId`, with `Status` and `ImportedAt` timestamp.
- All types are in the `AGXIOME.DynamicRuntime.Skills` namespace and are designed for serialization and storage.

## Cross-References
- [[SkillsRegistry]] — manages the lifecycle and state of user skill entries.
- [[DynamicRuntime]] — parent module containing the skills subsystem.

## Source Files
- `AGXIOME.DynamicRuntime/Skills/SkillsShModels.cs` (ingested 2026-05-14)