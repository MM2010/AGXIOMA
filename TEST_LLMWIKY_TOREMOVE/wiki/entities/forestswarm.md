---
title: ForestSwarm
category: entity
tags: [agent routing, tree-based, keyword scoring, pattern]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# ForestSwarm

## Summary
ForestSwarm is a tree-based agent routing pattern from the AGXIOME .NET framework. It organizes agents into named trees and routes tasks to the most semantically relevant tree and agent using keyword-overlap scoring, entirely without LLM calls. This provides a lightweight, deterministic routing strategy for multi-agent systems. The design mirrors Swarms Python's `ForestSwarm(trees=[Tree(agents=[TreeAgent(...)])])`.

## Key Details
- **Class**: `ForestSwarm` in `AGXIOME.Core.Patterns`
- **Routing strategy**: For each tree, score each agent by counting case‑insensitive keyword overlaps between the task and the agent's keyword set. The tree/agent with the highest score is selected; ties broken by agent order. If no overlap, falls back to the first agent of the first tree.
- **Tokenization**: Task text is split into tokens (letters/digits only). Single‑ and two‑character tokens are ignored as noise words.
- **`RunAsync(task, ct)`**: Uses `FindBestMatch` to identify the best agent, then executes the agent via `IAgxiomeAgent.RunAsync`. Returns a `ForestSwarmResult` containing the matched tree name, agent ID, response content, matched keywords, and elapsed time.
- **Supporting types**:
  - `ForestTree` – contains a tree name and a list of `ForestAgentEntry`.
  - `ForestAgentEntry` – wraps an agent ID, description, routing keywords, and an `IAgxiomeAgent` instance.
  - `ForestSwarmConfig` – configurable `MinMatchScore` (default 0).
- **Fallback behavior**: If no keyword overlap exists, the task is routed to the first agent of the first tree, ensuring every task is handled.
- **Configuration**: The `ForestSwarmConfig` record allows tuning the minimum match score.

## Cross-References
- [[IAgxiomeAgent]] — the agent interface used by `ForestAgentEntry`
- [[AgentRequest]] — the request object passed to agents in `RunAsync`
- [[ForestSwarmResult]] — the result type returned by `RunAsync`
- [[Swarms Python parity]] — the pattern mirrors the Python `ForestSwarm` implementation

## Source Files
- `AGXIOME.Core/Patterns/ForestSwarm.cs` (ingested 2026-05-14)