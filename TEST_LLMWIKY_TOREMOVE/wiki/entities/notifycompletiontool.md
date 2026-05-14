---
title: NotifyCompletionTool
category: entity
tags: [orchestration, subagent, tool, completion, ICompletionNotifier, IHotStore]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# NotifyCompletionTool

## Summary
`NotifyCompletionTool` is the second of two sub‑agent tools (following `SaveResultTool`). It reads a previously saved `TaskResult` from the hot store (via `IHotStore`) and publishes it to the master agent through `ICompletionNotifier`. This separation enforces single‑responsibility: saving the result and notifying the master are independent concerns.

## Key Details
- **Class**: `AGXIOME.Orchestration.Tools.SubAgent.NotifyCompletionTool`
- **Interface**: `IAgxiomeExecutableTool`
- **Tool Name**: `"NotifyCompletion"`
- **Dependencies**: `ICompletionNotifier`, `IHotStore` (injected via constructor)
- **Required Parameters**:
  - `taskId` (Guid) — identifies the task result to retrieve
  - `planId` (Guid) — identifies the plan (used in output, not in logic)
- **Execution Flow**:
  1. Validates required parameters.
  2. Reads the saved JSON from hot store key `HotKeys.TaskResult(taskId)`.
  3. Deserializes JSON to `TaskResult`.
  4. Calls `_notifier.PublishAsync(result, ct)` to notify the master.
  5. Updates the status key `HotKeys.TaskStatus(taskId)` to `"Done"` or `"Failed"` based on `result.Success`.
- **Returns**: `ToolResult` with `notified: true`, `taskId`, `planId`, and `success` on success; failure message on error.
- **Failure Cases**:
  - Missing or invalid `taskId` / `planId`.
  - No saved result found (must call `SaveResultTool` first).
  - Deserialization failure.
- **JSON Schema** (`ToJsonSchema`): object with required string properties `taskId` and `planId`.

## Cross-References
- [[SaveResultTool]] — the companion tool that saves the result before notification
- [[ICompletionNotifier]] — abstraction for publishing completion events to the master
- [[IHotStore]] — transient storage abstraction for task results and statuses
- [[TaskResult]] — model containing execution outcome data
- [[OrchestratorTaskStatus]] — enum for status values (`Done`, `Failed`, etc.)

## Source Files
- `AGXIOME.Orchestration/Tools/SubAgent/NotifyCompletionTool.cs` (ingested 2026-05-14)