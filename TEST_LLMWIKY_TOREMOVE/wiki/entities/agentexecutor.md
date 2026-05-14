---
title: AgentExecutor
category: entity
tags: [agent, execution, streaming]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AgentExecutor

## Summary
AgentExecutor is a sealed convenience class that centralizes the different execution modes for an AGXIOMA agent. It provides synchronous (single response), streaming (chunked), event-stream, and background+resume invocation patterns by delegating to the corresponding methods on `IAgxiomeAgent`. This allows callers to switch between execution styles without directly coupling to the agent interface.

## Key Details
- Sealed class in `AGXIOME.Core.Execution` namespace.
- Delegates all calls to the agent’s `RunAsync`, `RunStreamAsync`, and `RunEventStreamAsync` methods.
- `RunAsync(agent, request, ct)` → returns `Task<AgentResponse>` (standard single response).
- `RunStreamAsync(agent, request, ct)` → returns `IAsyncEnumerable<AgentChunk>` (streaming partial results).
- `RunEventStreamAsync(agent, request, ct)` → returns `IAsyncEnumerable<RunOutputEvent>` (event-based streaming).
- All methods accept a `CancellationToken` for cooperative cancellation.
- The class acts as a thin facade; actual execution logic lives in the agent implementation.

## Cross-References
- [[IAgxiomeAgent]] — Interface that defines the delegated execution methods.
- [[AgentRequest]] — Input model for all agent execution modes.
- [[AgentResponse]] — Output model for synchronous agent runs.
- [[AgentChunk]] — Output model for streaming chunk runs.
- [[RunOutputEvent]] — Output model for event-stream runs.

## Source Files
- `AGXIOME.Core/Execution/AgentExecutor.cs` (ingested 2026-05-14)