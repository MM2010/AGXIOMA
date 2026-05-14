---
title: AgUiHost
category: entity
tags: [agui, chat, session, streaming, agent, web]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AgUiHost

## Summary
`AgUiHost` provides a ChatGPT-like web interface for AGXIOME agents, managing session lifecycle, message handling, and streaming responses. It is designed for embedding inside ASP.NET Minimal APIs or SignalR hubs, offering a simple in-memory session store and both synchronous and asynchronous streaming APIs. It acts as the primary orchestration point for building interactive agent UIs.

## Key Details
- Uses a `ConcurrentDictionary` to store `AgUiSession` instances keyed by session ID (12-character hex string).
- `CreateSession(userId, agent, title?)` creates a new session linked to a specific `IAgxiomeAgent`.
- `SendMessageAsync(sessionId, text)` blocks until the agent completes and returns the full `AgUiMessage`.
- `SendMessageStreamAsync(sessionId, text)` yields content chunks as `IAsyncEnumerable<string>`, appending the final response to the session history.
- Both message methods automatically append the user message and final assistant message to the session's `Messages` list.
- `GetHistoryJson(sessionId)` serializes the session message list to pretty-printed JSON.
- Sessions are updated (`Touch()`) on each message exchange, with `UpdatedAt` timestamp tracking.
- Thread-safe for concurrent access; each session holds a reference to an agent and a list of `AgUiMessage` records.

## Cross-References
- [[IAgxiomeAgent]] — the agent interface used by each session to run requests.
- [[AgentRequest]] — the input object passed to the agent for processing.
- [[AgUiMessage]] — the record type representing individual chat messages (role, content, timestamp).

## Source Files
- `AGXIOME.Interfaces/AgUi/AgUiHost.cs` (ingested 2026-05-14)