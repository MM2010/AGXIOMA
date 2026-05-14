---
title: SaveResultTool
category: entity
tags: [orchestration, subagent, tool, persistence, HMAC]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# SaveResultTool

## Summary

SaveResultTool is a sub-agent tool responsible for persisting sub-agent task output to both the hot store (fast path, 48-hour TTL) and the orchestration store (canonical audit). It is step 1 of a two-step completion sequence and explicitly does **not** send notifications — that is the responsibility of [[NotifyCompletionTool]]. The tool supports an HITL (human-in-the-loop) pause mode via the `isHitlPaused` flag, and optionally computes a tamper‑evident HMAC over the result payload when an `IAgxiomeCrypto` service is registered.

## Key Details

- **Interface**: `IAgxiomeExecutableTool`; name `"SaveResult"`, description `"Saves the sub-agent task output to hot store and orchestration store"`, version `"1.0"`.
- **Categories**: `["orchestration", "subagent"]`.
- **Required Parameters**: `taskId` (Guid), `planId` (Guid), `output` (string). Optional: `summary`, `success` (bool, default true), `tokensUsed` (int), `errorMessage`, `isHitlPaused` (bool), `hitlRequirementId`, `data` (dictionary), `elapsedMs`.
- **Persistence**: First writes to `IHotStore` with a 48-hour TTL using key `HotKeys.TaskResult(taskId)`, then to `IOrchestrationStore.SaveResultAsync`.
- **HITL Support (P15)**: If `isHitlPaused` is `true`, the task is not marked as complete; the result is stored with `IsHitlPaused = true` and an optional `HitlRequirementId`. Notification is deferred to the HITL resolution flow.
- **Tamper Evidence (P20)**: If an `IAgxiomeCrypto` instance is injected, a HMAC is computed over the canonical string `$"{taskId}:{planId}:{output}:{success}"` using a static key (`"agxiome-result-hmac-key-v1"`). The HMAC is stored as `ResultHmac`.
- **Result Model**: Returns a `TaskResult` object with all fields populated, including `CompletedAt`, `ElapsedTime`, and optional `Data` dictionary.
- **Error Handling**: Returns a `ToolResult` with failure if required parameters are missing or invalid.
- **JSON Schema**: Exposes a full `JsonObject` schema specifying `taskId`, `planId`, `output` as required, with optional properties for all other parameters.

## Cross-References

- [[entities/notifycompletiontool]] — counterpart tool responsible for sending notifications after save.
- [[entities/hotstore]] — `IHotStore` interface used for fast temporary storage.
- [[entities/orchestrationstore]] — `IOrchestrationStore` interface used for canonical audit storage.
- [[entities/agxiomecrypto]] — `IAgxiomeCrypto` interface used for HMAC computation.
- [[entities/taskresult]] — the `TaskResult` model persisted by this tool.
- [[entities/toolcontext]] — the `ToolContext` passed to `ExecuteAsync`.

## Source Files

- `AGXIOME.Orchestration/Tools/SubAgent/SaveResultTool.cs` (ingested 2026-05-14)