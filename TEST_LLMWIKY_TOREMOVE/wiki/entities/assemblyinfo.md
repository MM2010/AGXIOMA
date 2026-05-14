---
title: AssemblyInfo
category: entity
tags: [assembly, internals-visible-to, testing]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AssemblyInfo

## Summary
The `AssemblyInfo.cs` file in the AGXIOME.Orchestration project uses the `InternalsVisibleTo` attribute to expose internal types and members to the AGXIOME.Tests project, enabling unit testing of internal code without making it public.

## Key Details
- Declares `[assembly: InternalsVisibleTo("AGXIOME.Tests")]` at the assembly level.
- Allows the test project to access `internal` classes, methods, and properties from the orchestration assembly.
- Does not expose any types—only grants test visibility.
- Follows common .NET testing patterns to keep implementation details hidden while enabling white-box testing.

## Cross-References
- [[AGXIOME.Tests]] — the test assembly granted internal access

## Source Files
- `AGXIOME.Orchestration/AssemblyInfo.cs` (ingested 2026-05-14)