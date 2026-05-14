---
title: ToolCallLimiter
category: entity
tags: [tool, limits, rate-limiting, concurrency, run-state]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# ToolCallLimiter

## Summary
The `ToolCallLimiter` is a thread-safe, per-run guard that enforces hard limits on tool usage within the AGXIOMA agent framework. It prevents runaway tool calls, excessive consecutive errors, and per-tool abuse by tracking counters and rejecting calls that exceed configured caps. This ensures agent runs stay within resource boundaries and can fail gracefully when limits are reached.

## Key Details
- **Enforces three configurable limits**: total calls per run, consecutive error streak, and per-tool call count (a value of `0` means unlimited).
- **Enum `ToolCallLimitViolation`** reports the specific reason for a blocked call: `TotalCallsExceeded`, `ConsecutiveErrorsExceeded`, or `PerToolCallsExceeded`.
- **Stateful per‑run tracking** using a `ConcurrentDictionary<string, RunCallState>`; `RunCallState` holds counters and a per-tool dictionary via `ConcurrentDictionary<string, int>`.
- **Workflow**: call `Check` before executing a tool → execute → call `RecordSuccess` or `RecordError` to update counters.
- **`RecordSuccess`** resets the consecutive‑error counter; `RecordError` increments both consecutive errors and total errors.
- **`GetStats`** returns a snapshot (`ToolCallRunStats`) including calls per tool; `Reset` zeroes all counters; `Remove` frees memory for a completed run.
- **Thread safety** achieved through `ConcurrentDictionary` and `Interlocked` operations; the implementation is designed for concurrent use by multiple agents or tools.

## Cross-References
- [[Tool]] — the abstract concept of a tool whose calls are limited by this class.
- [[Run]] — each run gets its own isolated state tracked by `ToolCallLimiter`.
- [[ToolExecutor]] — likely consumer that calls `Check` before invoking a tool and reports results.

## Source Files
- `AGXIOME.Core/Tools/ToolCallLimiter.cs` (ingested 2026-05-14)