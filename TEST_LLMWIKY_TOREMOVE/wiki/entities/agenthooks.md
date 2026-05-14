---
title: Agent Hooks
category: entity
tags: [hooks, extensibility, agent]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Agent Hooks

## Summary
Agent Hooks are extensibility points in the AGXIOMA framework that allow custom logic to be injected at specific moments in an agent's lifecycle. The `DelegateHook` implementation enables creating lightweight, delegate‑based hooks without requiring a full class implementation, supporting ordered execution, background running, and cancellation.

## Key Details
- Defined by the `IAgentHook` interface.
- `DelegateHook` wraps a `Func<HookContext, CancellationToken, Task>` as a hook handler.
- Properties: `Name` (string), `Point` (HookPoint), `Order` (int, default 0), `RunInBackground` (bool, default false).
- Execution: `ExecuteAsync(HookContext context, CancellationToken ct)` calls the delegate.
- Hooks are attached to a `HookPoint` (e.g., before/after agent steps, message processing).
- Order determines execution sequence among hooks at the same point.
- Background hooks run asynchronously without blocking the main pipeline.

## Cross-References
- [[IAgentHook]] — interface that all hooks must implement
- [[HookContext]] — data passed to hook execution
- [[HookPoint]] — enumeration of lifecycle points where hooks can execute
- [[CancellationToken]] — used for cooperative cancellation

## Source Files
- `AGXIOME.Core/Hooks/AgentHooks.cs` (ingested 2026-05-14)