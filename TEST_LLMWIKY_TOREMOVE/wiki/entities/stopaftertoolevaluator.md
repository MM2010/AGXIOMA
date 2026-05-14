---
title: StopAfterToolEvaluator
category: entity
tags: [stop-after-tool, evaluator, tool-management]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# StopAfterToolEvaluator

## Summary
A thread-safe evaluator that manages "stop after tool" rules, allowing the agent framework to halt a run immediately after a tool call based on configurable conditions (any call, success, or error). It is essential for controlling execution flow, especially after irreversible side-effect tools, and is used by the Auto mode via a static global instance.

## Key Details
- Defines `StopAfterToolMode` enum: `Never`, `AfterAnyCall`, `AfterSuccess`, `AfterError`.
- `StopAfterToolRule` record associates a tool name with a mode and an optional exit message.
- `StopEvaluation` record encapsulates whether to stop, the triggering tool name, mode, and exit message.
- Implements `IStopAfterToolEvaluator` interface with `AddRule`, `RemoveRule`, `Evaluate`, `GetRules`, `Clear`.
- Uses `ConcurrentDictionary<string, StopAfterToolRule>` for thread-safe rule storage.
- Exposes a static `Global` singleton instance used by Auto mode.
- `Evaluate` returns `StopEvaluation.Continue` if no rule exists for the tool; otherwise determines stop based on the rule's mode and the tool's success.
- Rules are added or replaced by exact, case-sensitive tool name.

## Cross-References
- [[IStopAfterToolEvaluator]] — interface this class implements
- [[AutoMode]] — the orchestration mode that leverages the global singleton evaluator
- [[Tool]] — the abstraction for tools whose outcomes trigger stop rules

## Source Files
- `AGXIOME.Core/Tools/StopAfterToolEvaluator.cs` (ingested 2026-05-14)