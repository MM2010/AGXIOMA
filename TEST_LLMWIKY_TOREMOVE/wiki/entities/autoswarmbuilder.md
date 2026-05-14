---
title: AutoSwarmBuilder
category: entity
tags: [swarm, LLM, autonomous, goal-analysis, team-generation]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AutoSwarmBuilder

## Summary
`AutoSwarmBuilder` is an LLM-driven factory that analyzes a textual goal to autonomously decide the optimal swarm topology, generate agent specifications, and instantiate the corresponding team. It eliminates manual swarm configuration by delegating architectural decisions to an LLM, implementing parity with the `Swarms.AutoSwarm(goal, agents)` pattern. The builder supports optional seeding with pre-built agents and limits the generated agent count to a configurable maximum (clamped between 1 and 16).

## Key Details
- **Constructor**: `AutoSwarmBuilder(ILLMWIKYEngine engine, string goal, int maxAgents = 8)` — requires an LLM engine and goal text; `maxAgents` is clamped to [1, 16].
- **`WithAgents(IReadOnlyList<IAgxiomeAgent>)`** — optionally seeds the builder with pre-built agents, skipping LLM generation.
- **`BuildAsync(CancellationToken)`** — returns a constructed swarm object (pattern-specific) after:
  1. LLM analysis → `AutoSwarmPlan` (pattern, agent count, agent specs, reasoning)
  2. Agent generation (unless custom agents provided)
  3. Swarm instantiation via `SwarmFactory` (e.g., `MixtureOfAgents`, `GroupChat`, `Hierarchical`, `MajorityVoting`, `PlannerWorker`, or fallback `AutoSwarm`)
- **LLM prompt** demands valid JSON only, with pattern, agentCount, agents array (name, role, systemPrompt), and reasoning; temperature 0.3, max tokens 2048.
- **Fallback plan** on parse failure: sequential single-agent with a generic assistant.
- **Agent generation** uses `Agent.AgentFactory.CreateTestAgent` (placeholder; production expects caller-configured LLM on each agent).
- **`AutoSwarmPlan`** record: `string Pattern`, `int AgentCount`, `List<AgentSpec> Agents`, `string? Reasoning`.
- **`AgentSpec`** record: `string Name`, `string Role`, `string SystemPrompt`.

## Cross-References
- [[swarmfactory]] — provides the concrete swarm instantiation methods invoked by `InstantiateSwarm`.
- [[illmwikengine]] — the LLM interface used for goal analysis.
- [[agentfactory]] — used to create test agents; in production, agents should be configured with proper LLM backend.

## Source Files
- `AGXIOME.Core/Patterns/AutoSwarmBuilder.cs` (ingested 2026-05-14)