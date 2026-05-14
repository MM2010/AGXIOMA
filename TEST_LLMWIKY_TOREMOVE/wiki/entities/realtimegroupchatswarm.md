---
title: RealtimeGroupChatSwarm
category: entity
tags: [realtime, signalr, groupchat, swarm, streaming]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# RealtimeGroupChatSwarm

## Summary
`RealtimeGroupChatSwarm` extends the core [[GroupChatSwarm]] pattern with true real-time token streaming via SignalR WebSockets. Unlike single-process Python swarms, AGXIOMA streams every token from every agent directly to the browser or SignalR client. It manages turn-based conversation among multiple agents, supports `@mention` routing, configurable speaker policies (RoundRobin, Random), and broadcasts `OnChunk`, `OnComplete`, and `OnStatusChange` events to a SignalR group (`chat:{Id}`). The class is designed for interactive, live-updating group chat experiences in ASP.NET Core applications.

## Key Details
- **Architecture:** Uses `IHubContext<AgentHub, IAgentClient>` to push real-time updates; clients join group `"chat:{Id}"` to receive streams.
- **Streaming vs Non-Streaming:** When `UseStreaming` is enabled, agent output is emitted token-by-token via `OnChunk`; otherwise the full response is sent at once.
- **Event Flow:** `OnStatusChange → (OnChunk)* → OnComplete` per turn; `OnError` on exception; optional `OnTurn` server-side callback for logging/audit.
- **Agent Selection:** `SelectSpeaker` method handles `@mention` routing, `MaxConsecutiveSpeaker` limits, and `SpeakerPolicy` (RoundRobin, Random, others from enum).
- **Stopping Conditions:** Configurable `StopCondition` delegate and `MaxLoops` limit; optional summariser agent (`SummariserAgentId`) appends a summary turn.
- **Dependencies:** `RealtimeGroupChatConfig` for all configuration (seed, streaming flag, speaker policy, etc.).
- **Client Integration:** JavaScript/TypeScript clients use `connection.on("OnChunk", ...)` and `connection.invoke("JoinAgentSession", chatId)`.
- **Thread Safety:** Uses `ConcurrentDictionary` internally for consecutive tracking (incomplete in snippet, but implied).

## Cross-References
- [[AgentHub]] — SignalR hub that brokers real-time events between swarm and clients.
- [[GroupChatSwarm]] — base non-realtime counterpart; same speaker logic without streaming.
- [[RealtimeGroupChatConfig]] — configuration object for seed, streaming, speaker policy, etc.
- [[IAgentClient]] — client interface defining SignalR callbacks (`OnChunk`, `OnComplete`, `OnStatusChange`).

## Source Files
- `AGXIOME.Realtime/RealtimeGroupChatSwarm.cs` (ingested 2026-05-14)