---
title: AgxiomeDagWorkflow
category: entity
tags: [dag, workflow, graph, execution-engine]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AgxiomeDagWorkflow

## Summary
The `AgxiomeDagWorkflow` is an explicit Directed Acyclic Graph (DAG) execution engine in the AGXIOME framework. It supports advanced workflow patterns including parallel fan-out, conditional branching, barrier (fan-in) synchronization, and lifecycle callbacks. It improves upon the simpler `AgxiomeWorkflow` by providing topological sort with cycle detection (Kahn’s algorithm), a `NodeType` enum, and dedicated methods for parallel branches and conditional edges.

## Key Details
- **Node types**: `Agent`, `Parallel`, `Conditional`, `Barrier`, `Transform`, `Sink`.
- **`AddParallelBranch`**: Executes multiple agents concurrently via `Task.WhenAll`, merging results into context under keys like `parallel:{groupId}:{agentName}`.
- **`AddConditionalEdge`**: Adds an edge that is only traversed when a predicate on `WorkflowContext` returns `true`, enabling branching logic.
- **`AddBarrier`**: Creates a fan-in node that waits for all specified predecessors before continuing to a single successor.
- **Callbacks**: `OnNodeStart` and `OnNodeComplete` expose `DagNodeExecution` for auditing, streaming, or logging.
- **Execution**: Uses topological layering (Kahn’s algorithm) to run nodes in parallel within each layer; handles cancellation via `CancellationToken`.
- **Backward compatible**: All methods from `AgxiomeWorkflow` are preserved for seamless migration.
- **Result**: Returns `DagWorkflowResult` with status, per-node results (`DagNodeResult`), and elapsed time.

## Cross-References
- [[AgxiomeWorkflow]] — Simpler predecessor; `AgxiomeDagWorkflow` extends its capabilities.
- [[WorkflowContext]] — Shared state passed through the DAG; used in conditional predicates.
- [[DagNode]] — Internal node representation used by the engine.
- [[DagEdge]] — Edge definition including optional condition.
- [[DagWorkflowResult]] — Output structure containing overall status and node results.
- [[DagNodeResult]] — Individual node execution result with status and duration.

## Source Files
- `AGXIOME.Core/Workflows/AgxiomeDagWorkflow.cs` (ingested 2026-05-14)