---
title: F7 Storage & Session Tests
category: entity
tags: [tests, storage, session, redis, agent, bridge]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# F7 Storage & Session Tests

## Summary
This test suite (SS-01 through SS-10) validates the storage and session enhancement features of AGXIOMA framework. The current implementation covers Redis session manager (SS-01) and agent storage bridge (SS-02) with mocked dependencies and planned live integration tests. The tests ensure correct construction, session creation/retrieval, disposal semantics, and round‑trip persistence for session and agent snapshot data.

## Key Details
- **RedisSessionManagerTests** cover null guards, option defaults, mock round‑trips for `CreateSession` and `GetSession`, and safe disposal of injected multiplexer.
- A live Redis test is marked `[Fact(Skip = "...")]` and requires a running Redis server.
- **AgentStorageBridgeTests** validate construction, `Save`/`Exists`/`Load` round‑trips, and null argument handling.
- All tests use NSubstitute to mock `IDatabase` and `ICommandStore` interfaces, isolating unit tests from external infrastructure.
- The session manager uses configurable `RedisSessionManagerOptions` with default key prefix `agxioma:sm`, TTL of 24 hours, and max 1000 history messages.
- The agent storage bridge wraps an `ICommandStore` to persist `AgentSnapshot` objects.
- Dispose semantics ensure an injected `IConnectionMultiplexer` is not disposed by the session manager (idempotent).
- The test file is located under `AGXIOME.Tests` and is part of the F7 workstream (7 tasks × 10 tests = 70 total target).

## Cross-References
- [[RedisSessionManager]] — the primary class under test in SS-01
- [[AgentStorageBridge]] — the class under test in SS-02
- [[AgentSnapshot]] — the data model persisted by the storage bridge
- [[ICommandStore]] — the abstraction mocked for bridge tests
- [[RedisSessionManagerOptions]] — configuration options for session manager

## Source Files
- `AGXIOME.Tests/F7StorageSessionTests.cs` (ingested 2026-05-14)