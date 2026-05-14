---
title: HeavySwarm
category: entity
tags: [heavy-swarm, pattern, analysis, agent-pattern]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# HeavySwarm

## Summary
HeavySwarm is a 5‑phase deep‑analysis pattern inspired by the “Grok” style. It processes an input through sequential stages: Question Generation, Research (parallel workers), Analysis, Alternatives, and Synthesis. Each phase can leverage different models and tools via the [[IAgxiomeAgent]] interface. The pattern provides full audit trails and timing for every phase, making it suitable for tasks requiring thorough, multi‑perspective reasoning.

## Key Details
- **5‑phase pipeline**: `QuestionGen → Research → Analysis → Alternatives → Synthesis`
- **Constructor** takes a `HeavySwarmConfig`, a question agent (`IAgxiomeAgent`), a list of worker agents, and a synthesizer agent.
- `RunAsync(string input, CancellationToken)` executes all phases sequentially and returns a [[HeavySwarmResult]] containing outputs and per‑phase timing.
- **Phase 1** (QuestionGen) uses the question agent to produce a focused research question from the input.
- **Phase 2** (Research) runs all workers in parallel, each given the same research question.
- **Phase 3** (Analysis) combines research findings and uses the synthesizer to produce a structured analysis (patterns, contradictions, insights).
- **Phase 4** (Alternatives) generates 3–5 alternative approaches or perspectives from the analysis.
- **Phase 5** (Synthesis) produces a comprehensive final answer incorporating the question, analysis, and alternatives.
- Audit trail (`AuditTrail`) exposes `HeavySwarmPhaseLog` records with phase name, output text, and duration.
- `HeavySwarmConfig` allows setting model names for each role, worker tools, and parallel research enablement.
- The pattern mirrors the signature `HeavySwarm(question, workers, synthesizer)` found in the [[Swarm]] pattern.

## Cross-References
- [[HeavySwarmResult]] — the output object containing all phase results and timing.
- [[HeavySwarmConfig]] — configuration record for model assignment and tooling.
- [[HeavySwarmPhaseLog]] — audit trail entry for each phase.
- [[IAgxiomeAgent]] — the agent interface used by all participants.
- [[Swarm]] — lighter parallel pattern with similar construction.
- [[AgentRequest]] — the request model used in agent calls.

## Source Files
- `AGXIOME.Core/Patterns/HeavySwarm.cs` (ingested 2026-05-14)