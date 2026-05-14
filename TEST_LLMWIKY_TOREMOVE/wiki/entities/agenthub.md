---
title: AgentHub
category: entity
tags: [real-time, SignalR, hub, streaming, orchestration]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AgentHub

## Summary
The `AgentHub`, `NotificationHub`, and `PresenceHub` classes provide the SignalR real-time communication layer for the AGXIOMA agent framework. `AgentHub` streams execution chunks, status changes, and human-in-the-loop (HITL) pause events to connected clients, and supports subscribing to orchestration plan lifecycle events (P19). `NotificationHub` delivers agent‑specific notifications and workflow updates, while `PresenceHub` tracks agent online/offline state. Together they enable live monitoring, cancellation, and interactive control of agent runs.

## Key Details
- **`IAgentClient` interface** defines callbacks: `OnChunk`, `OnComplete`, `OnError`, `OnStatusChange`, `OnPaused`, plus plan‑lifecycle methods (`OnPlanStarted`, `OnPlanDecomposed`, `OnTaskSpawned`, `OnStepAdvanced`, `OnPlanWaitingForHITL`, `OnPlanCompleted`, `OnPlanFailed`).
- **`AgentHub` methods**: `StreamAgentRun` (joins `agent:{agentId}` group and initiates streaming), `CancelAgentRun` (raises error to caller), `JoinAgentSession`/`LeaveAgentSession`, and `JoinPlan`/`LeavePlan` for orchestration plan subscriptions.
- Uses SignalR groups (`agent:`, `session:`, `plan:`) for targeted message delivery.
- **`NotificationHub`** provides `SubscribeToAgent` to receive `OnNotification` and `OnWorkflowUpdate` callbacks.
- **`PresenceHub`** raises `OnAgentOnline`/`OnAgentOffline` to all connected presence listeners when a client connects or disconnects.
- All hubs are located under the `AGXIOME.Realtime.Hubs` namespace.

## Cross-References
- [[AgentChunk]] — data structure streamed via `OnChunk`
- [[AgentResponse]] — result sent via `OnComplete`
- [[HITLRequirement]] — requirements sent during pause events
- [[Orchestration Plan]] — lifecycle events subscribed via `JoinPlan`/`LeavePlan`
- [[WorkflowStatus]] — status updates notified through `OnWorkflowUpdate`

## Source Files
- `AGXIOME.Realtime/Hubs/AgentHub.cs` (ingested 2026-05-14)