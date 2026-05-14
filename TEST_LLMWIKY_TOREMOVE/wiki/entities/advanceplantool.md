---
title: AdvancePlanTool
category: entity
tags: [orchestration, master, tool, advanceplan, llm]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AdvancePlanTool

## Summary
The `AdvancePlanTool` is the sixth and final step of the master orchestration sequence in the AGXIOME framework. It evaluates the aggregated results of completed tasks for the current step, uses an LLM to determine the next plan action, and then coordinates with persistence and agent infrastructure to either **Advance** to the next step, **Complete** the plan, **Fail** all active jobs, or **Retry** failed tasks. This tool acts as the central decision gateway that controls the lifecycle of an orchestration plan.

## Key Details
- Implements `IAgxiomeExecutableTool` and is registered with the name `"AdvancePlan"` and version `"1.0"`.
- Requires input parameters: `planId` (Guid), `completedResults` (list of `TaskResult`), and optionally `isLastStep` and `currentStep`.
- Uses an LLM (via `ILLMProvider`) to generate a JSON decision from a synthesis of task result summaries.
- Four possible LLM decisions: **Advance**, **Complete**, **Fail**, **Retry**.
  - **Advance**: increments the step, persists the new step number and running status to both cold and hot stores.
  - **Complete**: persists the final summary, marks the plan as `Completed`.
  - **Fail**: marks the plan as `Failed`, cancels all active jobs for that plan via `IAgentSpawner.CancelAsync`.
  - **Retry**: keeps the plan in `Running` state (orchestrator re-invokes the task spawning step).
- Interacts with `IOrchestrationStore` (cold, durable) and `IHotStore` (fast, ephemeral with 72-hour TTL) for plan state and task–job mapping.
- The `ParseResponse` method cleans LLM output (strips code fences), then deserializes to an `AdvanceResponse` record containing decision, synthesized context, final summary, and retry task IDs.
- Returns a `ToolResult` with structured metadata including the decision, next step, synthesized context, and retry IDs.

## Cross-References
- [[OrchestratorPlan]] — the plan object whose lifecycle this tool controls.
- [[SpawnAgentTool]] — typically invoked in the advance flow to spawn agents for the next step.
- [[TaskResult]] — the result type that feeds into the LLM summary.
- [[HotStore]] / [[OrchestrationStore]] — persistence layers used to read/write plan state.
- [[LLMProvider]] — the abstraction used to generate the advance decision.
- [[AgentSpawner]] — used to cancel active jobs on failure.

## Source Files
- `AGXIOME.Orchestration/Tools/Master/AdvancePlanTool.cs` (ingested 2026-05-14)