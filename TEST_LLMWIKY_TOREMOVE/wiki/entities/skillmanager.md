---
title: SkillManager
category: entity
tags: [skill-manager, lifecycle, orchestration, agent-binding]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# SkillManager

## Summary
SkillManager is the central orchestrator for skill lifecycle management in AGXIOMA, handling discovery, registration, agent binding, and remote registry access. It supports multiple skill sources—local filesystem (SKILL.md format), remote registries (skills.sh style), and programmatic C# skills—with progressive loading for efficiency. The built-in `LocalSkillsLoader` parses YAML frontmatter from SKILL.md files to create skill packages.

## Key Details
- Manages skill registration, unregistration, and lookup via `ConcurrentDictionary<string, IAgxiomeSkill>`.
- Supports agent binding with dependency resolution and tool registration (`ApplySkillToAgent` / `RemoveSkillFromAgent`).
- Remote registry operations: `ListRemoteAsync` and `DownloadSkillAsync` for skills.sh-style indexes.
- Uses pluggable loaders (`ISkillLoader`) for discoverable skill sources; `LocalSkillsLoader` parses SKILL.md with YAML frontmatter.
- Per‑agent skill tracking stored in `_agentSkills` (`ConcurrentDictionary<string, HashSet<string>>`).
- `PackageBackedSkill` bridges `SkillPackage` data model to `IAgxiomeSkill` interface.
- Supports progressive discovery: `DiscoverFromLoadersAsync` aggregates packages from all registered loaders.

## Cross-References
- [[ISkillManager]] — interface implemented by SkillManager
- [[IAgxiomeSkill]] — skill interface used for registration
- [[LocalSkillsLoader]] — built-in filesystem loader included in the same source file
- [[SkillPackage]] — data model for skill packages (manifest, scripts, references)

## Source Files
- `AGXIOME.Core/Skills/SkillManager.cs` (ingested 2026-05-14)