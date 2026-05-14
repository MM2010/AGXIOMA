---
title: AgxiomeAgentAttribute
category: entity
tags: [attribute, agent, metadata, dynamic runtime]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AgxiomeAgentAttribute

## Summary
The `AgxiomeAgentAttribute` is a custom .NET attribute used to mark a class as an AGXIOMA dynamic agent and to specify its identity (unique ID and human-readable description). It enables metadata extraction via Roslyn SyntaxTree analysis without requiring class instantiation, supporting compile-time discovery of agents.

## Key Details
- Sealed attribute class inheriting from `System.Attribute`.
- Usage: `[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]` – applies only to classes, once per class, not inherited.
- Constructor requires two non-null, non-whitespace strings: `id` and `description`.
- Property `Id` (string): unique agent identifier, expected in `snake_case` or `kebab-case`.
- Property `Description` (string): human-readable description exposed in the agent catalog.
- Throws `ArgumentException` if `id` or `description` is null or whitespace.
- Designed for metadata extraction via Roslyn SyntaxTree, enabling agent discovery without loading assemblies or instantiating classes.

## Cross-References
- (None yet – related entities such as [[DynamicAgent]] or [[AgentCatalog]] may be added as the wiki expands.)

## Source Files
- `AGXIOME.DynamicRuntime/Attributes/AgxiomeAgentAttribute.cs` (ingested 2026-05-14)