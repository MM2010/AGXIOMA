---
title: Agxiome
category: entity
tags: [agxiome, ai, agents, swarm, meta-package]
source_count: 5
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Agxiome

## Summary
Agxiome is the top-level meta-package (umbrella) of the AGXIOMA .NET agent framework. It bundles all core AGXIOMA packages — Shared, LLMWIKY, Core, DynamicRuntime, and Storage — into a single dependency for convenient consumption. By referencing `Agxiome`, consumers automatically acquire the entire framework stack.

## Key Details
- **Type**: Meta-package (no build output of its own; only project references)
- **Target Framework**: `net10.0`
- **Package Tags**: `agxiome`, `ai`, `agents`, `swarm`
- Includes five sub-packages: Shared, LLMWIKY, Core, DynamicRuntime, Storage
- Designed to simplify onboarding by providing a single NuGet dependency
- All sub-packages are referenced as `<ProjectReference>` in the source, not as NuGet packages (development-time aggregation)
- Intended for use in agent orchestration and swarm coordination scenarios

## Cross-References
- [[AGXIOME.Shared]] — shared abstractions and utilities
- [[AGXIOME.LLMWIKY]] — LLM integration layer
- [[AGXIOME.Core]] — core agent runtime and primitives
- [[AGXIOME.DynamicRuntime]] — dynamic execution engine
- [[AGXIOME.Storage]] — persistence and state management

## Source Files
- `AGXIOME/AGXIOME.csproj` (ingested 2026-05-14)