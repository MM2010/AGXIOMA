---
title: IToolRegistry
category: entity
tags: [interface, tool-registry, dependency-injection]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# IToolRegistry

## Summary
`IToolRegistry` is the central contract for managing tool instances in the AGXIOME framework. It supports registration of tools by type or instance, per‑agent binding, schema generation, and lifecycle hooks, enabling dynamic tool composition for agent execution.

## Key Details
- Generic `Register<T>()` requires `T` to implement `IAgxiomeTool`; registers a new instance per type.
- Overload `Register(IAgxiomeTool instance)` allows registration of pre‑configured tool instances.
- `Unregister(string toolName)` removes a tool from the global registry.
- `RegisterForAgent(string agentId, string toolName)` associates a tool with a specific agent.
- `UnregisterFromAgent(string agentId, string toolName)` removes that association.
- `GetTool(string toolName)` retrieves a tool by its name; returns `null` if not found.
- `GetAgentTools(string agentId)` returns a read‑only list of tools bound to the given agent.
- `GetAllTools()` returns every registered tool.
- `FindByCategory(string category)` filters tools by their category metadata.
- `GetToolSchemas(string agentId)` generates a `JsonArray` of OpenAPI‑style schemas for all tools of the specified agent.
- `AddToolHook(IToolHook hook)` registers a hook to intercept tool execution events.

## Cross-References
- [[IAgxiomeTool]] — tools registered via this registry.
- [[IToolHook]] — hooks that can be added for tool execution lifecycle.
- [[IAgentRegistry]] — agents that consume tools from this registry.

## Source Files
- `AGXIOME.Shared/Interfaces/IToolRegistry.cs` (ingested 2026-05-14)