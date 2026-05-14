---  
title: PollResultsTool  
category: entity  
tags: [orchestration, master, tool, poll, results, hitl]  
source_count: 1  
created_at: 2026-05-14  
updated_at: 2026-05-14  
---  

# PollResultsTool  

## Summary  
`PollResultsTool` is the fifth step (of six) in the master orchestration pipeline. It subscribes to a `ICompletionNotifier` bus to await task completions for a given plan, persisting each result to both the hot cache (`IHotStore`) and canonical store (`IOrchestrationStore`) on arrival. The tool supports a configurable timeout, returning partial results if the deadline expires. It also handles Human-in-the-Loop (HITL) pauses by marking the plan as `WaitingForHITL` and continuing to listen for resumed results.  

## Key Details  
- **Class**: `PollResultsTool` implements `IAgxiomeExecutableTool` in namespace `AGXIOME.Orchestration.Tools.Master`.  
- **Parameters**: `planId` (Guid, required), `expectedTaskIds` (IReadOnlyList\<Guid\>, required), `timeoutSeconds` (int, optional, default 300).  
- **Timeout behavior**: Uses a linked `CancellationTokenSource`; catches `OperationCanceledException` to capture timed-out task IDs.  
- **HITL pause**: If a task result has `IsHitlPaused == true`, the tool updates hot store and cold store statuses to `WaitingForHITL`, records the HITL context, and continues listening without removing the task from pending.  
- **Persistence**: Writes to hot store (with TTL of 48 hours for results, 72 hours for HITL context) and cold store (`SaveResultAsync`, `UpdateTaskStatusAsync`) immediately upon normal completion.  
- **Output**: `ToolResult` with `completedTasks`, `failedTasks`, `timedOutTasks`, and `allComplete` flag plus execution metadata.  

## Cross-References  
- [[ICompletionNotifier]] — the bus interface from which task results are streamed  
- [[IHotStore]] — fast cache for transient task and plan status  
- [[IOrchestrationStore]] — canonical/persistent storage for long-term record  
- [[ToolResult]] — the output contract returned by all Agxiome executable tools  
- [[ORCHESTRATION_PIPELINE_OVERVIEW]] — context for the six-step master tool sequence  

## Source Files  
- `AGXIOME.Orchestration/Tools/Master/PollResultsTool.cs` (ingested 2026-05-14)