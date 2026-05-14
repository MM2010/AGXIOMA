---
title: Heavyswarmtests
category: entity
tags: [tests, swarm, heavy-swarm, grok-swarm, grok-heavy-swarm]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Heavyswarmtests

## Summary
This test suite validates the configuration, result structures, and construction of three swarm patterns in the AGXIOME framework: `HeavySwarm`, `GrokSwarm`, and `GrokHeavySwarm`. It ensures default values are correct, phase logging works, result objects store all fields, and that swarm constructors enforce necessary constraints (e.g., non-null captain, full specialist set). These tests collectively guarantee that core swarm orchestration components behave as expected.

## Key Details
- `HeavySwarmConfig` defaults: `QuestionModel = "gpt-4o"`, `WorkerModel = "gpt-4o-mini"`, `SynthesisModel = "gpt-4o"`, `EnableParallelResearch = true`.
- `HeavySwarmPhaseLog` captures phase name, output string, and `TimeSpan` duration.
- `HeavySwarmResult` stores question, research findings list, analysis, alternatives list, synthesis, and phase timing dictionary.
- `GrokSwarmConfig` defaults `MaxLoops = 1`.
- `GrokSwarmResult` holds task and outputs from three agent roles (Harper, Benjamin, Lucas) plus captain synthesis.
- `GrokSwarm` constructor throws `ArgumentNullException` when captain agent is `null`; succeeds with four valid agents.
- `SwarmFactory.GrokSwarm` creates a `GrokSwarm` instance with given agents and configurable `maxLoops`.
- `GrokHeavySwarmConfig` defaults `MaxLoops = 1`.
- `GrokHeavySwarm.SpecialistNames` contains exactly 15 entries; `SpecialistDescriptions` covers every name.
- `GrokHeavySwarm` constructor requires 15 specialist agents; throws `ArgumentException` if fewer provided.
- `GrokHeavySwarmResult` stores task, captain synthesis, dictionary of specialist outputs, and phase timing.

## Cross-References
- [[HeavySwarm]] — core swarm pattern tested by this suite
- [[GrokSwarm]] — another swarm pattern with three specialist roles
- [[GrokHeavySwarm]] — swarm pattern with 15 specialists and captain agent
- [[IAgxiomeAgent]] — agent interface used in all swarm constructors
- [[SwarmFactory]] — factory method used to create `GrokSwarm` instances

## Source Files
- `AGXIOME.Tests/HeavySwarmTests.cs` (ingested 2026-05-14)