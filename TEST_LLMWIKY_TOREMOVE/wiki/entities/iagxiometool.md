---
title: IAgxiomeTool
category: entity
tags: [interface, tool, framework]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# IAgxiomeTool

## Summary
`IAgxiomeTool` is the core interface for all tools within the AGXIOME .NET agent framework. It defines essential metadata properties — name, description, version, and categories — that describe a tool. Its derived interface, `IAgxiomeExecutableTool`, extends this with an asynchronous execution method (`ExecuteAsync`) and a schema serialization method (`ToJsonSchema`), enabling tools to be invoked and documented dynamically.

## Key Details
- **`IAgxiomeTool`** requires implementing `Name` (string), `Description` (string), `Version` (nullable string), and `Categories` (read-only list of strings).
- **`IAgxiomeExecutableTool`** inherits `IAgxiomeTool` and adds:
  - `Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct)` — the execution contract for tool logic.
  - `JsonObject ToJsonSchema()` — returns a JSON schema describing the tool's input parameters.
- Both interfaces are defined in namespace `AGXIOME.Shared.Interfaces`.
- `ToolResult` and `ToolContext` are models used by executable tools but are not part of this interface definition.
- The pattern supports separation of metadata-only tools from executable ones, enabling flexible tool registration and discovery.

## Cross-References
- [[IAgxiomeExecutableTool]] — derived interface that adds execution and schema methods.
- [[ToolResult]] — return type for `ExecuteAsync` (defined in `AGXIOME.Shared.Models`).
- [[ToolContext]] — context parameter for `ExecuteAsync` (defined in `AGXIOME.Shared.Models`).

## Source Files
- `AGXIOME.Shared/Interfaces/IAgxiomeTool.cs` (ingested 2026-05-14)