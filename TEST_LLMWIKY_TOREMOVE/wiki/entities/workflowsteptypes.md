---
title: Workflow Step Types
category: entity
tags: [workflow, step, conditional, parallel, loop, router, CEL]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Workflow Step Types

## Summary
The AGXIOME framework defines several built-in workflow step types that control execution flow beyond simple sequential steps. These include conditional branches (if/else), parallel execution of child steps, loops with configurable maximum iterations and exit conditions, and a router that dynamically selects the next step based on a context-driven selector. A lightweight CEL-style condition evaluator supports simple expression parsing for routing decisions.

## Key Details
- **ConditionalBranch** (`StepType.Condition`): Executes either an `ifTrue` or `ifElse` child step based on a `Func<WorkflowContext, bool>` evaluator. Returns a `StepResult` with the condition value or failure.
- **ParallelStep** (`StepType.Parallel`): Runs all child `IWorkflowStep` instances concurrently via `Task.WhenAll`. Results are concatenated into a single output; any exception causes a failed step result.
- **LoopStep** (`StepType.Loop`): Iterates a body step up to `maxIterations` times (default 10). A optional end condition (`Func<WorkflowContext, bool>`) can break early. Outputs from each iteration are collected.
- **RouterStep** (`StepType.Router`): Uses a `Func<WorkflowContext, string>` selector to pick a route from a dictionary. Executes the matched step or skips if the route key is missing.
- **CELConditionEvaluator**: A simple expression parser supporting `Contains("...")` on step output and `session_state.key` presence checks. Returns `false` for unmatched expressions.
- All step types implement [[IWorkflowStep]] and return [[StepResult]] via `ExecuteAsync`.
- Steps rely on [[WorkflowContext]] for shared state and cancellation tokens.

## Cross-References
- [[IWorkflowStep]] — Interface implemented by all step types
- [[WorkflowContext]] — Context object passed into each step execution
- [[StepResult]] — Result type returned by step execution
- [[StepType]] — Enum used to classify step variants
- [[CELConditionEvaluator]] — Detailed documentation for the simple expression evaluator

## Source Files
- `AGXIOME.Core/Workflows/WorkflowStepTypes.cs` (ingested 2026-05-14)