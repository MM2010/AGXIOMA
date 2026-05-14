---
title: InMemoryCompletionNotifier
category: entity
tags: [completion-notifier, in-memory, testing, hitl]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# InMemoryCompletionNotifier

## Summary
In-memory, thread-safe implementation of [[ICompletionNotifier]] and [[IHitlCompletionSource]] that uses per-plan unbounded channels to relay `TaskResult` messages from sub-agents to the master orchestrator. Designed for local development and test scenarios, it eliminates external infrastructure while faithfully simulating asynchronous completion flows, including HITL (human-in-the-loop) pause/resume cycles.

## Key Details
- Backed by `ConcurrentDictionary<Guid, Channel<TaskResult>>` — one unbounded channel per plan.
- `PublishAsync()` writes a `TaskResult` into the plan's channel.
- `SubscribeAsync()` returns `IAsyncEnumerable<TaskResult>` that yields results as they arrive.
- `NotifyResolvedAsync()` (from `IHitlCompletionSource`) synthesises a `TaskResult` on HITL approval or rejection, then publishes it so the plan's `PollResultsTool` can continue.
- `Complete(Guid planId)` closes the channel, signalling end-of-stream to all subscribers.
- `Clear()` completes all channels and removes them — essential for test isolation.
- All public methods are thread-safe; intended for single-process use.

## Cross-References
- [[ICompletionNotifier]] — the interface this class implements for agent-to-master completion signalling.
- [[IHitlCompletionSource]] — the interface that enables HITLManager to resume paused tasks.
- [[TaskResult]] — the message model published and subscribed to.
- [[PollResultsTool]] — the subscriber that consumes these messages to drive the orchestrator loop.
- [[HITLManager]] — calls `NotifyResolvedAsync()` when a human resolves a paused task.

## Source Files
- `AGXIOME.Orchestration/Infrastructure/InMemoryCompletionNotifier.cs` (ingested 2026-05-14)