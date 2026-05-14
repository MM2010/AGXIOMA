---
title: HierarchicalSwarm
category: entity
tags: [swarm, pattern, hierarchical, feedback-loop]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# HierarchicalSwarm

## Summary
A structured agent orchestration pattern that decomposes a complex objective into subtasks using a **Director** agent, executes them via parallel or sequential **Workers**, and optionally evaluates outputs with a **Evaluator** agent in a feedback loop (up to `MaxFeedbackRounds` revisions). The Director then synthesises approved worker outputs into a final answer. This pattern improves reliability through explicit critique and retry, making it suitable for multi-step tasks requiring quality control.

## Key Details
- **Flow**: Director → Workers (parallel or sequential with dependency awareness) → Evaluator (optional feedback loop) → Director synthesis.
- **Subtasks** are parsed from Director’s JSON output; each includes `id`, `description`, optional `preferred_role`, and `dependencies`.
- **Worker selection**: round‑robin by default, or by matching `preferred_role` keyword against agent `Id`.
- **Evaluator** returns `APPROVED: true/false` and `CRITIQUE: <feedback>`; if not approved, worker revises its output up to `MaxFeedbackRounds` times.
- **EvaluationMode** supports Pass/Fail, Scored, or LLMCritic (via `_evaluator` agent).
- `OnSubtaskComplete` callback allows external monitoring per subtask.
- `RunAsync` returns a `HierarchicalResult` containing final output, director plan, per‑subtask results, duration, and success rate.
- **Differenziatori** from simple swarms: built‑in feedback loop, typed `SubtaskPlan`, per‑subtask timeout/retry budget, and structured critique.

## Cross-References
- [[Agent]] — base agent interface used for Director, Workers, and Evaluator.
- [[HierarchicalConfig]] — configuration for parallelism, feedback rounds, and callbacks.
- [[HierarchicalResult]] — output type encapsulating all execution details.

## Source Files
- `AGXIOME.Core/Patterns/HierarchicalSwarm.cs` (ingested 2026-05-14)