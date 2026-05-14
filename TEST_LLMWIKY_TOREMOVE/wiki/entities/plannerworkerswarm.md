---
title: PlannerWorkerSwarm
category: entity
tags: [planner, swarm, pattern, workflow]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# PlannerWorkerSwarm

## Summary
PlannerWorkerSwarm is a structured multi‑agent pattern in the AGXIOMA framework that decomposes a user’s goal into a typed task plan, distributes tasks to worker agents in dependency order, and optionally validates results with an evaluator. It distinguishes itself from simpler swarm patterns by supporting schema‑validated JSON task plans, topological dependency execution, automatic retry of failed tasks, a shared workspace for inter‑task data, human‑in‑the‑loop approval checkpoints, and a callback for full audit data.

## Key Details
- **Planner agent** generates a JSON array of `WorkerTask` objects, each with an `id`, `description`, `dependencies` list, optional `worker_hint`, and `priority`.
- **Worker agents** execute tasks in batches according to a topological sort of the dependency graph; tasks in the same batch can run in parallel if `ParallelWorkers` is enabled.
- **Optional Evaluator agent** reviews each completed task output and marks it as `PASS` or `FAIL`; a `FAIL` is treated as a non‑approved result.
- **Shared workspace** (`Dictionary<string, string>`) allows workers to access outputs from completed dependent tasks and to store their own results for downstream tasks.
- **Human‑In‑The‑Loop (HITL)** is prepared for: tasks marked `requires_approval` can pause execution (not fully implemented in the provided snippet but the hook exists).
- **`OnTaskComplete` callback** fires for every task with a `WorkerTaskResult` containing the task ID, description, worker ID, output, approval status, and success flag.
- **Fallback plan** if JSON parsing fails: a single fallback task is created using the original goal.
- **Final synthesis** step uses the planner agent to combine all successful task outputs into the final comprehensive result.
- **Configuration** via `PlannerWorkerConfig` controls parallel execution and other settings.

## Cross-References
- [[IAgxiomeAgent]] — abstract agent interface used for planner, workers, and evaluator.
- [[PlannerWorkerConfig]] — configuration class for the swarm (e.g., `ParallelWorkers`).
- [[WorkerTaskResult]] — data object produced for each executed task.
- [[WorkerTask]] — internal data model for a planned task.
- [[PlannerWorkerResult]] — final result containing goal, output, task results, duration, and counts.

## Source Files
- `AGXIOME.Core/Patterns/PlannerWorkerSwarm.cs` (ingested 2026-05-14)