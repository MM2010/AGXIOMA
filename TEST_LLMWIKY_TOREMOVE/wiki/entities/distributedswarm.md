---
title: DistributedSwarm
category: entity
tags: [distributed, agent, scalability, concurrency, channel]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# DistributedSwarm

## Summary
AGXIOMA’s `DistributedSwarm` is a horizontally scalable, multi‑process agent execution pattern that extends the core swarm concept across independent worker tasks. It uses in‑memory channels for work distribution, supports backpressure policies, runtime scale‑out, and exposes real‑time metrics and health monitoring. This pattern has no direct equivalent in single‑process systems like Swarms (Python), LangChain, AutoGen, or CrewAI.

## Key Details
- **Architecture**: Channel‑based work queue (default `BoundedChannel`) with each worker running an `IAgxiomeAgent` in its own `Task`.
- **Backpressure policies**: `Wait` (blocking) or `Drop` (drop oldest) controlled by `BackpressurePolicy` enum.
- **Configurable swarm settings**: initial workers, max queue depth, task timeout, retry on failure, completion callback (via `DistributedSwarmConfig`).
- **Lifecycle**: `StartAsync` launches initial workers; `DisposeAsync` cancels workers and completes the channel.
- **Task submission**:  
  - `SubmitBatchAsync` adds tasks and returns IDs.  
  - `SubmitAndWaitAsync` submits one task and polls for its result until timeout.
- **Runtime scale‑out**: `ScaleOut(int additionalWorkers)` adds more worker tasks to increase throughput without restarting.
- **Metrics**: `GetMetrics()` returns queue depth, submitted/completed counts, active/idle workers, throughput, and per‑worker utilisation.
- **Result collection**: All results stored in a `ConcurrentDictionary<string, DistributedTaskResult>` keyed by task ID.
- **Fault handling**: Failed tasks can be retried automatically if `RetryFailedTasks` is enabled in config.

## Cross-References
- [[IAgxiomeAgent]] — each worker slot runs an agent of this type
- [[DistributedSwarmConfig]] — configuration record for tuning behaviour
- [[BackpressurePolicy]] — enum defining queue overflow strategy

## Source Files
- `AGXIOME.Core/Patterns/DistributedSwarm.cs` (ingested 2026-05-14)