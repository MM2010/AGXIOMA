---
title: Hubs
category: entity
tags: [hubs, signalr, realtime, monitoring, eventing]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Hubs

## Summary
The AGXIOMA framework defines three SignalR hubs—`AgentHub`, `LogHub`, and `AdminHub`—that provide real-time event streaming for agent runs, log entries, and admin metrics. Each hub uses strongly-typed groups for fan-out isolation, attaches monotonic sequence numbers to all events, and supports a reconnect mechanism where clients can request the last N events to restore their local state. The hubs collectively enable responsive, live-updating UIs and monitoring dashboards.

## Key Details
- **AgentHub (RT-15)**: Groups `"run:{runId}"` and `"agent:{agentId}"`. Methods: `StartRun` (subscribes caller to both groups), `CancelRun`, `ResumeRun`, `RequestReplay` (stub for replay). Events: `RunStarted`, `RunCancelled`, `RunResumed`, and `ReplayStart`.
- **LogHub (RT-16)**: Groups `"logs:agent:{agentId}"` and `"logs:module:{module}"`. Methods: `SubscribeAgent`, `SubscribeModule`, `UnsubscribeAgent`, `SetLogLevel` (client-side filter hint). Internal `BroadcastLog` pushes `LogEvent` to relevant groups.
- **AdminHub (RT-17)**: Groups `"admin:metrics"` and `"admin:errors"`. Requires `AdminMetrics` permission for connection. Methods: `Subscribe`, `Unsubscribe`. Internal `BroadcastMetric` pushes `AdminMetricEvent` to the metrics group. Event: `AdminConnected`.
- All events are wrapped in sealed records: `AgentRunEvent`, `LogEvent`, `AdminMetricEvent`—each containing a `Seq` (monotonic increment via `Interlocked.Increment`), `Timestamp`, and typed payload.
- Reconnect: `RequestReplay(runId, lastN)` is a stub; production is expected to use a Redis sorted set to replay buffered events.
- Group membership cleanup is handled automatically by SignalR on disconnect (`OnDisconnectedAsync` is overridden but only calls base).

## Cross-References
- [[AgentHub]] — detailed reference of the AgentHub API
- [[LogHub]] — detailed reference of the LogHub API
- [[AdminHub]] — detailed reference of the AdminHub API
- [[Run Lifecycle]] — events streamed through AgentHub
- [[Logging Pipeline]] — events streamed through LogHub
- [[Admin Metrics]] — events streamed through AdminHub
- [[Reconnection Strategy]] — how clients restore state after disconnect

## Source Files
- `AGXIOME.Runtime/Hubs/Hubs.cs` (ingested 2026-05-14)