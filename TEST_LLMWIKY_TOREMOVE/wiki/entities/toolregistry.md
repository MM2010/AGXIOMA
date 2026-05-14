---
title: ToolRegistry
category: entity
tags: [tools, registry, hooks]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# ToolRegistry

## Summary
Thread-safe registry managing both global tools (shared across all agents) and per-agent tool sets. Supports runtime registration, unregistration, tool hooks, and JSON schema generation for executable tools. The optional `RegisterCallable` extension enables dynamic factory-based tool creation per session context.

## Key Details
- `ConcurrentDictionary<string, IAgxiomeTool>` for global tools, keyed by tool name.
- `ConcurrentDictionary<string, HashSet<string>>` for per-agent tool name sets.
- `Register<T>()` creates instance via `Activator.CreateInstance<T>()`; `Register(IAgxiomeTool)` accepts an existing instance.
- `Unregister(string name)` removes from global registry only; does not affect per-agent assignments.
- `RegisterForAgent(string agentId, string toolName)` / `UnregisterFromAgent` manage agent-specific tool sets.
- `GetAgentTools(string agentId)` returns all tools assigned to that agent (name lookup in global dictionary).
- `FindByCategory(string category)` filters global tools by `Categories` property.
- `GetToolSchemas(string agentId)` produces a `JsonArray` of JSON schemas for `IAgxiomeExecutableTool` instances.
- `AddToolHook(IToolHook)` appends lifecycle hooks; hooks are stored in a plain `List<IToolHook>`.
- Extension method `RegisterCallable(Func<ToolContext, List<IAgxiomeTool>>)` intended for runtime-evaluated factories (implementation uses `MemoryCache` internally).

## Cross-References
- [[IToolRegistry]] — interface this class implements
- [[IAgxiomeTool]] — base tool interface managed by the registry
- [[IAgxiomeExecutableTool]] — executable tool type used for schema generation
- [[IToolHook]] — hook interface for tool lifecycle events
- [[ToolContext]] — context parameter used in callable factory registration

## Source Files
- `AGXIOME.Core/Tools/ToolRegistry.cs` (ingested 2026-05-14)