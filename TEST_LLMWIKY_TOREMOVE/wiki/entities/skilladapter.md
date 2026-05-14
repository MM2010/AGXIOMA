---
title: SkillAdapter
category: entity
tags: [adapter, skill, runtime, compilation]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# SkillAdapter

## Summary
The `SkillAdapter` transforms a `SkillDefinition` into an `IAgxiomeExecutableTool` based on its type. For HTTP API skills it creates a thin HTTP wrapper; for C# functions it compiles embedded source code using Roslyn via `AgentCompiler`; and for prompt-type skills it currently throws a `NotSupportedException`. This adapter is the central factory for converting declarative skill definitions into executable tools within the AGXIOME runtime.

## Key Details
- Accepts a `SkillDefinition` and returns an `IAgxiomeExecutableTool` asynchronously via `AdaptAsync`.
- Handles three skill types: `HttpApi`, `CSharpFunction`, and `Prompt` (not yet supported).
- `HttpApiSkillTool` sends a POST with JSON payload to the endpoint defined in the skill and returns `ToolResult`.
- `DynamicAgentSkillTool` wraps a compiled `ISimpleDynamicAgent` instance and invokes `RunAsync` with string input.
- Uses `AgentCompiler` for Roslyn compilation of C# source code; compilation failures raise `InvalidOperationException`.
- If `SkillType.Prompt`, throws `NotSupportedException`; other types throw `ArgumentOutOfRangeException`.
- Internal file-scoped classes `HttpApiSkillTool` and `DynamicAgentSkillTool` implement `IAgxiomeExecutableTool`.
- The `ToJsonSchema()` method returns a basic JSON schema with an `input` string property for both wrapper tools.

## Cross-References
- [[SkillDefinition]] — the input model that defines a skill's type, source code, and endpoint.
- [[AgentCompiler]] — used to compile C# functions embedded in skills.
- [[IAgxiomeExecutableTool]] — the interface that adapted skills implement for execution.
- [[ToolResult]] — the return type of tool execution.

## Source Files
- `AGXIOME.DynamicRuntime/Skills/SkillAdapter.cs` (ingested 2026-05-14)