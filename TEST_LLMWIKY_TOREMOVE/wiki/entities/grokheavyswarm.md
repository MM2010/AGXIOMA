---
title: GrokHeavySwarm
category: entity
tags: [pattern, swarm, domain-specialist, analysis]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# GrokHeavySwarm

## Summary
A 16-agent domain-specialist analysis pattern where a Grok Captain orchestrates 15 parallel domain specialists, then synthesizes their outputs into a cross-domain conclusion. Parity with the Python HeavySwarm where `use_grok_heavy=True`.

## Key Details
- Pattern: Grok Captain → 15 parallel specialists → cross-domain synthesis.
- 3-phase pipeline per loop: question generation, parallel specialist execution, synthesis.
- Configuration via `GrokHeavySwarmConfig` (e.g., `MaxLoops`).
- Specialists cover domains from creative writing to ethics and systems thinking (all 15 named).
- Validates all 15 specialist slots at construction; case-insensitive key matching.
- Produces an audit trail (`HeavySwarmPhaseLog`) and timing metrics per phase.
- Main entry: `RunAsync(task, ct)` returns `GrokHeavySwarmResult`.
- Each specialist agent implements `IAgxiomeAgent`.

## Cross-References
- [[GrokCaptain]] — orchestrator agent; conceptually linked but distinct concrete class.
- [[GrokHeavySwarmConfig]] — configuration used by this pattern.
- [[HeavySwarmPhaseLog]] — audit trail record for each pipeline phase.
- [[GrokHeavySwarmResult]] — result container with specialist outputs and synthesis.

## Source Files
- `AGXIOME.Core/Patterns/GrokHeavySwarm.cs` (ingested 2026-05-14)