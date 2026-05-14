---
title: AgentCodeTemplate
category: entity
tags: [agent, code-generation, template, dynamic-runtime]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AgentCodeTemplate

## Summary
`AgentCodeTemplate` is a static class in the `AGXIOME.DynamicRuntime.Templates` namespace providing the C# source template used to generate minimal dynamic agents. The template contains placeholders (e.g., `{{AGENT_ID}}`, `{{LOGIC_BODY}}`) that are replaced by `AgentCodeGenerator` before compilation, enabling LLM-generated logic to be injected into a fully functional agent class.

## Key Details
- Defined in `AGXIOME.DynamicRuntime.Templates.AgentCodeTemplate`.
- Exposes a public constant `Source` containing the template string.
- Template placeholders:
  - `{{AGENT_ID}}` — unique agent identifier (e.g., `"sentiment-analyzer"`)
  - `{{AGENT_CLASSNAME}}` — PascalCase class name derived from `AGENT_ID`
  - `{{DESCRIPTION}}` — human‑readable agent description
  - `{{LOGIC_BODY}}` — code body for the `RunAsync` method
- Public method `Fill(agentId, description, logicBody)` replaces all placeholders and returns the complete source code.
- Private helper `ToPascalCase(id)` converts hyphens/underscores to PascalCase (e.g., `"sentiment-analyzer"` → `"SentimentAnalyzer"`).
- Generated class implements `ISimpleDynamicAgent` and is decorated with `[AgxiomeAgent]`.

## Cross-References
- [[AgentCodeGenerator]] — consumes this template to produce compilable agent code
- [[ISimpleDynamicAgent]] — interface that generated agents must implement
- [[AgxiomeAgentAttribute]] — attribute applied to generated agent classes

## Source Files
- `AGXIOME.DynamicRuntime/Templates/AgentCodeTemplate.cs` (ingested 2026-05-14)