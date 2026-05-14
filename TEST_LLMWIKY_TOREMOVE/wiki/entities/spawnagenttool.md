---
title: SpawnAgentTool
category: entity
tags: [orchestration, master, tool, agent]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# SpawnAgentTool

## Summary
`SpawnAgentTool` is the fourth step in the six-step master orchestration toolchain. It instantiates and starts sub-agents for the tasks defined in the current plan step. The tool supports four execution modes (Sequential, Parallel, RaceFirst, Quorum) and enriches each `TaskSpec` with optional agent configurations before spawning. It also applies skill bindings (P16) and HMAC-based task spec signing (P20) for tamper-evident dispatch.

## Key Details
- **Name**: `SpawnAgent`
- **Description**: Spawns sub-agents for the current step tasks using provided agent configurations.
- **Version**: `1.0`
- **Categories**: `orchestration`, `master`
- **Dependencies**: `IAgentSpawner`, `IHotStore`, `IOrchestrationStore` (required); `ISkillManager?` and `IAgxiomeCrypto?` (optional, null in dev/test)
- **Required parameter**: `tasks` (`IReadOnlyList<TaskSpec>`)
- **Optional parameters**: `agentConfigurations` (`IReadOnlyList<AgentConfiguration>`), `executionMode` (`StepExecutionMode`, default: `Parallel`)
- **Execution modes**: Sequential (one at a time), Parallel, RaceFirst, Quorum (all spawn concurrently; differentiation occurs in `PollResultsTool`)
- On spawn: applies skills from `TaskSpec.SkillIds` via `ISkillManager` (stored in hot store key `task:{taskId}:skillIds`)
- Signs each `TaskSpec` with HMAC using `IAgxiomeCrypto.ComputeHMAC` (key: `"agxiome-taskspec-hmac-key-v1"`) — signature covers task identity and description
- Updates hot store and orchestration store with `Running` status and job ID for each task
- Returns a `ToolResult` with a list of `(taskId, jobId)` pairs and execution time metadata
- Implements `IAgxiomeExecutableTool` and provides a JSON schema via `ToJsonSchema()`

## Cross-References
- [[TaskSpec]] — the unit of work spawned by this tool
- [[AgentConfiguration]] — optional per-task overrides applied during enrichment
- [[StepExecutionMode]] — the enumeration driving spawn behavior
- [[IAgentSpawner]] — interface for actual agent instantiation
- [[IHotStore]] — ephemeral storage for spawn-related state
- [[IOrchestrationStore]] — persistent store for orchestration task status
- [[ISkillManager]] — skill application (P16)
- [[IAgxiomeCrypto]] — HMAC signing (P20)
- [[PollResultsTool]] — the next step in the master pipeline that collects spawned results

## Source Files
- `AGXIOME.Orchestration/Tools/Master/SpawnAgentTool.cs` (ingested 2026-05-14)