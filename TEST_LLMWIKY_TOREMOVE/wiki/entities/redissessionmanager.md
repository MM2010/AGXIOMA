---  
title: RedisSessionManager  
category: entity  
tags: [Redis, session manager, storage, ISessionManager]  
source_count: 1  
created_at: 2026-05-14  
updated_at: 2026-05-14  
---  

# RedisSessionManager  

## Summary  
A full implementation of [[ISessionManager]] backed by Redis. It manages the complete session lifecycle—creation, retrieval, listing, typed state, message history, and summarization—using a structured key layout under a configurable prefix. Designed for production, it uses server-generated (Guid.CreateVersion7) session IDs to prevent client injection and applies TTL-based expiration automatically. Distinct from the generic entity store [[RedisSessionStore]] (which implements ICommandStore).  

## Key Details  
- **Key layout** (all under `{prefix}`):  
  - `s:{sessionId}` → Session JSON (TTL = SessionTtl)  
  - `m:{sessionId}` → Redis List of serialized AgentMessage  
  - `st:{sessionId}:{T}` → Typed state JSON keyed by type name  
  - `sum:{sessionId}` → Summary plain string  
  - `u:{userId}` → Sorted Set (score = creation timestamp, member = sessionId)  
- **Options** via `RedisSessionManagerOptions` record: `KeyPrefix` (default `"agxioma:sm"`), `SessionTtl` (24h), `MaxHistoryMessages` (1000).  
- Two constructors: production (connection string, owns multiplexer) and DI/testing (injects `IConnectionMultiplexer`).  
- **Session creation**: generates a new `Session` object, persists it, adds to user index sorted set, sets TTL on the index key.  
- **Session retrieval**: `GetSessionAsync` deserializes from string; `ListSessionsAsync` reads user index sorted set (newest first) and returns matching sessions, optionally filtered by `SessionType`.  
- **Typed state**: stored/retrieved per type name via `UpdateSessionStateAsync<T>` / `GetSessionStateAsync<T>`; refreshes session’s `UpdatedAt` timestamp.  
- **Message history**: `AddMessageAsync` pushes to list, trims to `MaxHistoryMessages`, sets TTL; `GetHistoryAsync` reads last N or all.  
- **Summary**: stored as plain string; `SetSummaryAsync` / `GetSummaryAsync` (inferred from truncated source).  
- Implementations handle `CancellationToken`, and the class implements `IDisposable` to cleanly dispose an owned multiplexer.  
- Serialization uses `JsonSerializerOptions` with `DefaultIgnoreCondition.WhenWritingNull`.  

## Cross-References  
- [[ISessionManager]] — the interface this class implements  
- [[RedisSessionStore]] — related Redis-backed store for generic entity persistence (ICommandStore)  
- [[Session]] — the session model used for creation and retrieval  
- [[AgentMessage]] — the message type stored in history  

## Source Files  
- `AGXIOME.Storage/Session/RedisSessionManager.cs` (ingested 2026-05-14)