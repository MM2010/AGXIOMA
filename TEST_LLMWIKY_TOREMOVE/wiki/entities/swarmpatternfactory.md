---
title: SwarmPatternFactory
category: entity
tags: [factory, swarm, pattern, auto-swarm]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# SwarmPatternFactory

## Summary
The `SwarmFactory` static class is the zero-boilerplate entry point for creating all AGXIOMA swarm architectures. It provides type-safe factory methods for each supported pattern (Mixture of Agents, Group Chat, Hierarchical, Majority Voting, Planner/Worker, Model Router, and specialty patterns like HeavySwarm, GrokSwarm, and GrokHeavySwarm). It also includes an `AutoSwarm` method that selects the most appropriate architecture based on a goal string and agent list, and a `HeavySwarm`/`GrokSwarm`/`GrokHeavySwarm` set for deep analysis and debate scenarios.

## Key Details
- Static class in `AGXIOME.Core.Patterns` namespace.
- Methods return concrete swarm types: `MixtureOfAgentsSwarm`, `GroupChatSwarm`, `HierarchicalSwarm`, `MajorityVotingSwarm`, `PlannerWorkerSwarm`, `AgxiomeModelRouter`, `HeavySwarm`, `GrokSwarm`, `GrokHeavySwarm`.
- Each method accepts a configuration object or inline parameters (e.g., `MoAConfig`, `GroupChatConfig`).
- `AutoSwarm` uses keyword matching on the goal string to pick a pattern (vote → MajorityVoting, debate → GroupChat, plan → PlannerWorker, synthe → MixtureOfAgents, default → Hierarchical or MixtureOfAgents with 1 layer).
- `HeavySwarm` performs 5-phase analysis: QuestionGen → Research → Analysis → Alternatives → Synthesis.
- `GrokSwarm` uses a 4-agent debate pattern (Captain + 3 agents).
- `GrokHeavySwarm` uses a 16-agent domain specialist mode (Captain + 15 domain agents).
- All methods are designed to eliminate boilerplate; users need only provide agents and minimal configuration.

## Cross-References
- [[MixtureOfAgentsSwarm]] — produced by `MixtureOfAgents()`
- [[GroupChatSwarm]] — produced by `GroupChat()`
- [[HierarchicalSwarm]] — produced by `Hierarchical()`
- [[MajorityVotingSwarm]] — produced by `MajorityVoting()`
- [[PlannerWorkerSwarm]] — produced by `PlannerWorker()`
- [[AgxiomeModelRouter]] — produced by `ModelRouter()`
- [[HeavySwarm]] — produced by `HeavySwarm()`
- [[GrokSwarm]] — produced by `GrokSwarm()`
- [[GrokHeavySwarm]] — produced by `GrokHeavySwarm()`
- [[IAgxiomeAgent]] — interface used for all agents
- [[RouterEntry]] — input for `ModelRouter()`
- [[HeavySwarmConfig]] — configuration for `HeavySwarm()`

## Source Files
- `AGXIOME.Core/Patterns/SwarmPatternFactory.cs` (ingested 2026-05-14)