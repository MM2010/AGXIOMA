---
title: AgentFileScanner
category: entity
tags: [agent, roslsn, attribute, scanner, hotfolder]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AgentFileScanner

## Summary
`AgentFileScanner` is a static utility class that reads a `.cs` source file using Roslyn `SyntaxTree.ParseText()` (parse-only, no compilation) and extracts agent metadata from the `[AgxiomeAgent("id","description")]` attribute. It also computes a SHA256 hash of the source text for change detection. Returns an [[AgentFileMetadata]] record or `null` if the attribute is missing or invalid.

## Key Details
- The only public method is `ScanAsync(string filePath, CancellationToken ct)`, which returns a `Task<AgentFileMetadata?>`.
- Uses `CSharpSyntaxTree.ParseText()` to build a syntax tree without compiling the code.
- Searches for attribute nodes matching `AgxiomeAgent` or `AgxiomeAgentAttribute` (case‑sensitive ordinal comparison).
- Expects exactly two string literal arguments: agent ID and description.
- Returns `null` on `IOException` (e.g., file not found) or if the attribute cannot be parsed.
- Computes SHA256 hash of the raw source via `ComputeHash(string source)` (static helper, exposed public).
- The nested record `AgentFileMetadata` holds: `AgentId`, `Description`, `FilePath`, `SourceHash`.

## Cross-References
- [[AgentFileMetadata]] — the record returned by `ScanAsync`
- [[AgxiomeAgentAttribute]] — the custom attribute targeted by the scanner

## Source Files
- `AGXIOME.DynamicRuntime/HotFolder/AgentFileScanner.cs` (ingested 2026-05-14)