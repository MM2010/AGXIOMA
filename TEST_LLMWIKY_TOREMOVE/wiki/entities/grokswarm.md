---
title: GrokSwarm
category: entity
tags: [pattern, swarm, debate, analysis, agents, AGXIOME, Core]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# GrokSwarm

## Summary
GrokSwarm is a structured multi-agent pattern for debate-style analysis within the AGXIOME framework. A Captain agent decomposes a task into three focused questions, which are answered in parallel by three specialist agents (Harper for research/facts, Benjamin for logic/analysis, Lucas for creative alternatives). The Captain then synthesizes the specialist outputs into a final answer with explicit conflict resolution. The pattern can run iteratively (up to `MaxLoops` times) to refine answers based on previous syntheses, and it maintains a full audit trail of all phases. This pattern mirrors the Python Swarms library's `HeavySwarm(use_grok_agents=True)` behavior.

## Key Details
- **Four fixed roles**: Captain (orchestrator), Harper (research), Benjamin (logic), Lucas (contrarian/creative).
- **Pipeline per loop**: Decompose → Parallel specialist execution → Captain synthesis; repeat up to `MaxLoops` times.
- **Decomposition**: Captain outputs a JSON with fields `research`, `logic`, `alternatives` — each a targeted question for the respective specialist.
- **Parallel execution**: Specialists run concurrently via `Task.WhenAll` for efficiency.
- **Conflict resolution**: Captain explicitly addresses disagreements among specialists in the synthesis phase.
- **Audit trail**: A list of `HeavySwarmPhaseLog` records each phase's input and elapsed time, accessible via `AuditTrail`.
- **Configurable**: Uses `GrokSwarmConfig` (e.g., `MaxLoops`); agents must implement `IAgxiomeAgent`.
- **Return type**: `GrokSwarmResult` containing all specialist outputs, final synthesis, and phase timing.
- **Source location**: `AGXIOME.Core/Patterns/GrokSwarm.cs`

## Cross-References
- [[HeavySwarm]] — Python counterpart in the Swarms library; GrokSwarm is functionally equivalent when `use_grok_agents=True`.
- [[GrokSwarmConfig]] — configuration options controlling loop behavior.
- [[GrokSwarmResult]] — data structure holding the output of a GrokSwarm run.
- [[IAgxiomeAgent]] — agent interface used by all roles in the pattern.

## Source Files
- `AGXIOME.Core/Patterns/GrokSwarm.cs` (ingested 2026-05-14)