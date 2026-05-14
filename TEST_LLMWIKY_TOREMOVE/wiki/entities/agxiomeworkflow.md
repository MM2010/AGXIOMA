---
title: AgxiomeWorkflow
category: entity
tags: [workflow, step, execution, core]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AgxiomeWorkflow

## Summary
`AgxiomeWorkflow` is the central workflow engine in the AGXIOME .NET agent framework. It orchestrates a directed graph of steps (function-based or agent-based) and executes them in topological order, respecting conditional transitions. This class implements `IAgxiomeWorkflow` and provides the runtime for composing multi-step agent pipelines with branching, error handling, and cancellation support.

## Key Details
- Implements `IAgxiomeWorkflow` and manages steps via `Dictionary<string, IWorkflowStep>`.
- Provides `AddStep` for function steps and `AddAgentStep` for agent steps.
- Transitions between steps are added with `AddTransition(from, to, condition?)` where condition is a `Func<WorkflowContext, bool>`.
- Execution starts from entry steps (steps with no incoming transitions) and processes in BFS order, respecting conditions.
- Tracks `WorkflowStatus` (`Created`, `Running`, `Completed`, `Failed`).
- CancellationToken (`ct`) propagates to all step executions.
- Errors in a step are captured as `StepResult` with `StepStatus.Failed`; workflow status becomes `Failed`.
- `FunctionWorkflowStep` wraps a synchronous/asynchronous handler delegate.
- `AgentWorkflowStep` invokes an `IAgxiomeAgent` using a prompt from the shared state.

## Cross-References
- [[IAgxiomeWorkflow]] — interface contract implemented by this class
- [[IWorkflowStep]] — abstraction for individual steps
- [[WorkflowContext]] — shared context passed through execution
- [[WorkflowResult]] — final result produced by `ExecuteAsync`

## Source Files
- `AGXIOME.Core/Workflows/AgxiomeWorkflow.cs` (ingested 2026-05-14)