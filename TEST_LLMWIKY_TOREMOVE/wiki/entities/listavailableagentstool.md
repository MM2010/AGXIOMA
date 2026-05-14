---
title: ListAvailableAgentsTool
category: entity
tags: [tool, registry, discovery, dynamic]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# ListAvailableAgentsTool

## Summary
The `ListAvailableAgentsTool` is an orchestrator tool that returns all dynamic agents and imported skills available for a given user. It serves as a central discovery endpoint in the AGXIOME framework, providing a unified view of an user's registered agents and skills along with their status and relevant metadata. This tool is essential for user-facing dashboards or runtime introspection.

## Key Details
- **Name**: `list_available_agents`
- **Description**: "Lists all dynamic agents and imported skills available for a user, including their status and descriptions."
- **Required parameter**: `userId` (string, must not be null or empty) passed via `ToolContext.Parameters`.
- **Dependencies**: `DynamicAgentRegistry` and `SkillsShRegistry` (injected via constructor).
- Returns a JSON object containing:
  - `UserId` (the provided user id)
  - `Agents` (list of agent objects with kind, agentId, description, status, discoveredAt)
  - `Skills` (list of skill objects with kind, skillId, name, description, type, status, importedAt)
  - `TotalAgents` and `TotalSkills` counts.
- On missing/invalid `userId`, returns a failed `ToolResult` with an error message.
- All runtime data is fetched from the underlying registries (synchronous for agents, async for skills).

## Cross-References
- [[DynamicAgentRegistry]] — source of dynamic agent data for a user.
- [[SkillsShRegistry]] — source of imported skills data for a user.
- [[ToolContext]] — provides the context and parameters for tool execution.
- [[ToolResult]] — the generic result type returned by all tools.
- [[AgxiomeFunction]] — base class for all tool implementations in the framework.

## Source Files
- `AGXIOME.Core/Tools/ListAvailableAgentsTool.cs` (ingested 2026-05-14)