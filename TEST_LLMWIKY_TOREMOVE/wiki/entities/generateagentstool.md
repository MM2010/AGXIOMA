---
title: GenerateAgentsTool
category: entity
tags: [orchestration, master, sub-agent, configuration, tool]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# GenerateAgentsTool

## Summary
The `GenerateAgentsTool` is a master orchestration tool (step 3 of 6) that determines the optimal sub-agent configuration—including tool allowlists, system prompts, temperature, and preferred LLM provider—for each task in an orchestration step. It leverages an LLM to select these parameters per task type, producing a list of `AgentConfiguration` records. This tool does **not** spawn agents; that responsibility belongs to [[SpawnAgentTool]].

## Key Details
- **Interface**: Implements `IAgxiomeExecutableTool`.
- **Name**: `"GenerateAgents"`
- **Description**: Determines optimal sub-agent configurations (tools, temperature, system prompt) for each task.
- **Version**: `"1.0"`
- **Categories**: `["orchestration", "master"]`
- **Dependencies**: `ILLMProvider` (injected via constructor).
- **Execution Parameters (in `ToolContext.Parameters`)**:
  - `"tasks"` (required): `IReadOnlyList<TaskSpec>` – tasks to configure agents for.
  - `"availableTools"` (optional): `IReadOnlyList<string>` – list of tool names the LLM may assign.
  - `"llmProviders"` (optional): `IReadOnlyList<string>` – available LLM providers.
- **Output**: `ToolResult` with `agentConfigurations` containing a list of [[AgentConfiguration]] records.
- **LLM Prompt**: Asks the LLM to respond with a JSON array of configurations, adhering to temperature guidelines (factual: 0.1–0.3, analytical: 0.4–0.6, creative: 0.7–0.9) and restricting tools to the provided allowlist.
- **Fallback**: Any task missing a configuration in the LLM response receives a default configuration using its own `AllowedToolNames` and `Temperature`.
- **JSON Schema**: Exposed via `ToJsonSchema()` – declares `"tasks"` as required array.
- **Design Note**: Part of a 6-step master pipeline; does not spawn agents—only configures them.

## Cross-References
- [[SpawnAgentTool]] – the tool that actually creates sub-agents based on configurations produced by this tool.
- [[AgentConfiguration]] – the record type returned by this tool, containing per-task agent setup.
- [[TaskSpec]] – input task definition used as source data for configuration decisions.
- [[ILLMProvider]] – required dependency for LLM interaction.

## Source Files
- `AGXIOME.Orchestration/Tools/Master/GenerateAgentsTool.cs` (ingested 2026-05-14)