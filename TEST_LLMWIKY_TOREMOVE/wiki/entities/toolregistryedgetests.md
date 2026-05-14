---
title: ToolRegistryEdgeTests
category: entity
tags: [tests, tool-registry, edge-cases, duplicate, json-schema, agent-tools]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# ToolRegistryEdgeTests

## Summary
This test class validates edge‑case behaviors of `ToolRegistry`, including duplicate tool name resolution (last registration wins), lookup of unknown tools, empty category filter results, JSON schema generation for tools with and without parameters, and the isolation of agent‑specific tool registrations when unregistering from one agent.

## Key Details
- `RegisterDuplicateName_LastWins` – Verifies that when two tools share the same name (`"test_tool"`), the last registered type (`TestToolB`) is returned by `GetTool`.
- `GetTool_UnknownName_ReturnsNull` – Confirms that `GetTool` returns `null` for a name that has not been registered.
- `FindByCategory_NoMatch_EmptyList` – Ensures `FindByCategory` returns an empty list when no tool matches the given category.
- `ToJsonSchema_NoParams_ValidSchema` – Validates that `NoParamTool`'s JSON schema contains the `name` property with the correct value and has no `parameters` object (or at least no `required` array).
- `ToJsonSchema_WithParams_HasRequired` – For `TestToolA` (which has a required parameter), the generated schema includes a `parameters` object containing a `required` array.
- `UnregisterAgentTool_DoesNotAffectOtherAgents` – After registering the same tool for two agents and unregistering from one, the other agent’s tools remain unaffected; `GetAgentTools("agent-a")` returns empty, while `GetAgentTools("agent-b")` still contains one tool.
- The test fixtures `TestToolA`, `TestToolB`, and `NoParamTool` demonstrate minimal tool implementations that extend `AgxiomeFunction`.

## Cross-References
- [[ToolRegistry]] – The class that is being tested for edge cases.
- [[AgxiomeFunction]] – Base class for all tool implementations used in the tests.
- [[ToolRegistryAgentTests]] – Likely related tests focused on agent‑specific behavior.

## Source Files
- `AGXIOME.Tests/ToolRegistryEdgeTests.cs` (ingested 2026-05-14)