---
title: DecomposeRequestTool
category: entity
tags: [orchestration, master, tool]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# DecomposeRequestTool

## Summary
The `DecomposeRequestTool` is the first step in the six-step master orchestration pipeline. It takes a natural‑language user request and, using an LLM provider (`ILLMProvider`), decomposes it into an ordered list of atomic `TaskSpec` objects. These tasks are later persisted by `PersistPlanTool` and executed by sub‑agents. The tool does **not** persist the plan itself — it only generates the decomposition and returns it as part of the `ToolResult`.

## Key Details
- **Name**: `"DecomposeRequest"`  
- **Categories**: `["orchestration", "master"]`  
- **Dependency**: Requires an `ILLMProvider` injected via constructor.  
- **Required parameter**: `request` (string).  
- **Optional parameters**: `sessionId`, `planId`, `maxTasks` (default 8), `context` (string).  
- **Execution**: Calls the LLM with a system prompt enforcing JSON‑only output, parses the response into `TaskSpec` objects, and returns them in a `ToolResult`.  
- **Task schema produced**: Each task has `description`, `stepIndex`, `allowedToolNames`, and `temperature`.  
- **Validation**: Returns a failure `ToolResult` if the request is empty, parsing fails, or zero tasks are generated.  
- **Metadata**: Execution time and item count are recorded in `ToolMetadata`.  
- **JSON schema** (via `ToJsonSchema()`): defines `request` as required string, plus optional `sessionId`, `planId`, `maxTasks`, `context`.

## Cross-References
- [[PersistPlanTool]] — Step 2 of the master pipeline; persists the `TaskSpec` list produced by this tool.
- [[ILLMProvider]] — Interface used to invoke the LLM for decomposition.
- [[TaskSpec]] — The data model produced by this tool.
- [[MasterToolsOverview]] — Overview of the six‑step master orchestration sequence.

## Source Files
- `AGXIOME.Orchestration/Tools/Master/DecomposeRequestTool.cs` (ingested 2026-05-14)