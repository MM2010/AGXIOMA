---
title: ICompletionNotifier
category: entity
tags: [interface, orchestration, notifications, completion-bus]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# ICompletionNotifier

## Summary
`ICompletionNotifier` defines an agnostic completion notification bus used to decouple sub-agents from the master during plan execution. Sub-agents call `PublishAsync` (typically via a tool) when they finish a task; the master subscribes via `SubscribeAsync` per `planId` to stream results as they arrive. This abstraction supports multiple transports (SignalR, Redis pub/sub, in-memory `Channel<T>`, webhooks, gRPC streams) and centralizes task-result delivery.

## Key Details
- Agnostic transport interface – implementations can use SignalR, Redis, in-memory channels, webhooks, or gRPC.
- **`PublishAsync(TaskResult, CancellationToken)`** – Called by the sub-agent (e.g., via `NotifyCompletionTool`) to deliver a `TaskResult` for a completed step.
- **`SubscribeAsync(Guid planId, CancellationToken)`** – Returns `IAsyncEnumerable<TaskResult>` that streams results for the specified plan; stream ends when the consumer cancels.
- Designed for the master agent to subscribe once per plan and receive all sub-agent completions in order.
- Each `TaskResult` likely references the plan and step it belongs to (via `planId` implicit or explicit in the model).
- Thread-safe and scalable – implementations should handle multiple concurrent producers and consumers.
- Key integration point between sub-agent tool execution and plan-level orchestration.

## Cross-References
- [[entities/taskresult]] — The data contract published and consumed by this notifier.
- [[entities/notifycompletiontool]] — The tool sub-agents call to publish a `TaskResult` through `PublishAsync`.
- [[concepts/plan-orchestration]] — How the master subscribes and reacts to completion events per plan.

## Source Files
- `AGXIOME.Orchestration/Abstractions/ICompletionNotifier.cs` (ingested 2026-05-14)