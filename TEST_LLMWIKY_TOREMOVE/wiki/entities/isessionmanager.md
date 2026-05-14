---
title: ISessionManager
category: entity
tags: [interface, session-management, state, messaging]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# ISessionManager

## Summary
`ISessionManager` defines the core contract for managing conversational sessions within the AGXIOME framework. It provides lifecycle operations (create, get, list, delete), generic state persistence, message history, and summary capabilities. The companion `ISessionState` interface enables typed key-value state storage within a session. Together they abstract session storage and retrieval, allowing different backends to be plugged in.

## Key Details
- `CreateSessionAsync` — creates a new session with a user, component, type, and optional options (cancellation supported).
- `GetSessionAsync` — retrieves a single session by ID; returns `null` if not found.
- `ListSessionsAsync` — lists sessions for a user, optionally filtered by `SessionType` and limited (default 50).
- `UpdateSessionStateAsync<T>` — stores or replaces a generic state object for a session (serialization handled internally).
- `GetSessionStateAsync<T>` — retrieves the typed state object; returns `null` if none set.
- `AddMessageAsync` — appends an `AgentMessage` to the session's history.
- `GetHistoryAsync` — returns the message history; optional `lastN` parameter for most recent N messages.
- `GetSessionSummaryAsync` / `UpdateSessionSummaryAsync` — get/set a textual summary for the session.
- `DeleteSessionAsync` — removes a session and all its data.
- All methods accept an optional `CancellationToken` for async operation.
- `ISessionState` — simple key-value store with `Get<T>`, `Set<T>`, `ContainsKey`, and `ToDictionary` (for export/serialization).

## Cross-References
- [[Session]] — the data model returned by `GetSessionAsync` and used in creation.
- [[SessionType]] — enumeration for classifying sessions (e.g., chat, workflow).
- [[SessionOptions]] — optional configuration parameter for session creation.
- [[AgentMessage]] — the message type used in `AddMessageAsync` and `GetHistoryAsync`.

## Source Files
- `AGXIOME.Shared/Interfaces/ISessionManager.cs` (ingested 2026-05-14)