---
title: WorkflowEdgeTests
category: entity
tags: [workflow, testing, edge-cases, DAG, CEL, routing, parallelism, nested-workflows]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# WorkflowEdgeTests

## Summary
The `WorkflowEdgeTests` class validates critical edge-case behaviors of the AGXIOMA workflow engine, including cycle detection, error propagation, parallel execution, conditional routing, CEL condition evaluation, and multi-level nesting. These tests ensure robustness and correct handling of non-trivial workflows that extend beyond straightforward linear execution.

## Key Details
- Cyclic DAG detection: a transition from `a` to `b` and back to `a` is detected and does not cause a hang; the workflow completes with partial results.
- Step throws but workflow continues: a failing step does not block subsequent steps; the error is recorded in step results and the workflow proceeds to the next step.
- Parallel execution of 100 steps all complete successfully using a `ParallelStep`.
- Router with unknown route returns a completed status with output containing "not found" (step is skipped).
- CEL condition evaluation for basic string containment (`input.Contains("urgent")`) returns `true` when the input contains the substring.
- CEL condition evaluates to `false` when the string is absent.
- Nested workflows up to three levels (outer, mid, inner) execute and return a non-null result without crashing.
- All tests use `xUnit` and depend on core AGXIOMA workflow abstractions (e.g., `AgxiomeWorkflow`, `WorkflowContext`, `IWorkflowStep`, `RouterStep`, `ParallelStep`, `CELConditionEvaluator`).

## Cross-References
- [[AgxiomeWorkflow]] — the main workflow class under test
- [[WorkflowContext]] — context object used throughout these tests
- [[CELConditionEvaluator]] — condition evaluator tested in two scenarios
- [[RouterStep]] — routing step tested with unknown route
- [[ParallelStep]] — parallel step tested with 100 child steps

## Source Files
- `AGXIOME.Tests/WorkflowEdgeTests.cs` (ingested 2026-05-14)