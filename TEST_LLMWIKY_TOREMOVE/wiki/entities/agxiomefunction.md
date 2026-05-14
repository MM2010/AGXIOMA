---
title: AgxiomeFunction
category: entity
tags: [abstract class, tool interface, JSON schema, reflection, attribute]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AgxiomeFunction

## Summary
`AgxiomeFunction` is an abstract base class that provides a standard template for typed tools in the AGXIOME framework. It implements both `IAgxiomeTool` and `IAgxiomeExecutableTool` and automates JSON schema generation for tool parameters via reflection on properties decorated with `[ToolParameter]` attributes. Derived types only need to define `Name`, `Description`, and `ExecuteAsync`; the schema is derived automatically.

## Key Details
- Abstract class; must be inherited to create a concrete tool.
- Implements `IAgxiomeTool` and `IAgxiomeExecutableTool`.
- Provides a default `Version` of `"1.0.0"` (virtual, can be overridden).
- Provides a virtual `Categories` property returning an empty list.
- Exposes `ToJsonSchema()` which builds a JSON object including `name`, `description`, and a `parameters` object.
- Parameter schema generation is driven by properties on the derived class that are annotated with `[ToolParameter]` attribute.
- `[ToolParameter]` supports `Required`, `Description`, `DefaultValue`, `Type` (override), `MinValue`, `MaxValue`.
- Type mapping from C# types to JSON types: `string` → "string", `int`/`long` → "integer", `double`/`float` → "number", `bool` → "boolean", default "string".
- Property names are automatically converted to snake_case for the schema (e.g., `MaxTokens` → `max_tokens`).
- `ExecuteAsync` is abstract and must be implemented; returns `ToolResult` and accepts `ToolContext` and `CancellationToken`.

## Cross-References
- [[IAgxiomeTool]] — interface this class implements
- [[IAgxiomeExecutableTool]] — executable tool interface
- [[ToolParameterAttribute]] — custom attribute used to annotate properties
- [[ToolResult]] — return type of `ExecuteAsync`
- [[ToolContext]] — context parameter for execution

## Source Files
- `AGXIOME.Shared/Interfaces/AgxiomeFunction.cs` (ingested 2026-05-14)