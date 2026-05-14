---
title: SessionManager
category: entity
tags: [entity, session, in-memory, manager, ISessionManager, ConcurrentDictionary, AGXIOME.Core]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# SessionManager

## Summary
The `SessionManager` class provides an in-memory, thread-safe implementation of `ISessionManager` using `ConcurrentDictionary`. It is designed for development and testing; production deployments should use `PostgresSessionManager`. It stores sessions and their message histories entirely in memory and supports all standard session lifecycle operations.

## Key Details
- Implements `ISessionManager` interface.
- Uses `ConcurrentDictionary<string, Session>` for session storage and `ConcurrentDictionary<string, List<AgentMessage>>` for message history.
- Session IDs are generated using `Guid.CreateVersion7()` (time-ordered UUID).
- `CreateSessionAsync` accepts `userId`, `componentId`, `SessionType`, optional `SessionOptions`, and optional cancellation token.
- `ListSessionsAsync` filters by `userId` and optional `SessionType`, returns last `limit` sessions.
- Session state is stored as a generic object and cast when retrieved via `GetSessionStateAsync<T>`.
- `AddMessageAsync` appends messages to the session's history; `GetHistoryAsync` optionally returns last N messages.
- Session summary (`Name` property) is used as a lightweight summary; updated via `UpdateSessionSummaryAsync`.
- `DeleteSessionAsync` removes both session and its message list.
- All methods return `Task` or `Task<T>`; note that in-memory operations are essentially synchronous but wrapped for interface compliance.

## Cross-References
- [[ISessionManager]] — the interface this class implements.
- [[PostgresSessionManager]] — recommended production replacement.

## Source Files
- `AGXIOME.Core/Session/SessionManager.cs` (ingested 2026-05-14)