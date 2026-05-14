---
title: IAgxiomeWorkflow
category: entity
tags: [workflow, interface, agent]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# IAgxiomeWorkflow

## Summary
`IAgxiomeWorkflow` defines the contract for building and executing sequential or conditional workflows within the AGXIOME framework. It enables developers to add steps (standard or agent-based), define transitions between steps with optional conditions, and run the entire workflow asynchronously with cancellation support. The companion interface `IWorkflowStep` represents an individual step in the workflow pipeline.

## Key Details
- **Property `Id`** (`string`): Unique identifier for the workflow instance.
- **Property `Status`** (`WorkflowStatus`): Current execution status of the workflow.
- **Method `AddStep`**: Adds a standard workflow step given a name and a handler function (`Func<WorkflowContext, Task>`). Returns an `IWorkflowStep`.
- **Method `AddAgentStep`**: Adds a step associated with an `IAgxiomeAgent` that defines its own behavior. Returns an `IWorkflowStep`.
- **Method `AddTransition`**: Defines a directed transition from one step (`from`) to another (`to`), with an optional condition predicate (`Func<WorkflowContext, bool>?`).
- **Method `ExecuteAsync`**: Runs the entire workflow using a provided `WorkflowContext` and optional `CancellationToken`. Returns a `WorkflowResult`.
- **`IWorkflowStep` interface**: Contains `Name` (string), `Type` (StepType), and `ExecuteAsync(WorkflowContext, CancellationToken)` returning `StepResult`.

## Cross-References
- [[IAgxiomeAgent]] — Agent-based steps reference an `IAgxiomeAgent` instance.
- [[WorkflowContext]] — Execution context passed to steps and transitions.
- [[WorkflowResult]] — Result produced by workflow execution.
- [[StepResult]] — Result produced by an individual step execution.

## Source Files
- `AGXIOME.Shared/Interfaces/IAgxiomeWorkflow.cs` (ingested 2026-05-14)