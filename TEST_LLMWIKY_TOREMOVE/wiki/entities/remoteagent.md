---
title: RemoteAgent
category: entity
tags: [transport, client, remote, runtime]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# RemoteAgent

## Summary
RemoteAgent is an HTTP transport adapter that delegates agent execution to a remote AGXIOME Runtime instance via REST. It does not re-implement any local agent logic—the remote host is authoritative for all tool calls and state mutations. It supports both synchronous `RunAsync` and SSE-based streaming `StreamAsync` operations, with optional bearer token authentication and configurable timeout.

## Key Details
- **`RemoteAgentOptions`** – Configuration record with `BaseUrl` (required), `Timeout` (default 60s), and optional `BearerToken`.
- **`IRemoteAgent`** – Lean contract exposing `RunAsync`, `StreamAsync`, and `RemoteBaseUrl`.
- **`RemoteAgent`** – Sealed class built on `HttpClient`. Sets `Authorization` header when a token is provided. Never log the token.
- **`RunAsync`** – POSTs `AgentRequest` to `api/agent/run` and deserializes the `AgentResponse`.
- **`StreamAsync`** – POSTs to `api/agent/stream`, reads SSE events (`data:` lines), and yields each token as a string.
- **`RemoteAgentFactory.Create`** – Convenience static method to instantiate `RemoteAgent` without DI container access.
- **Important security note:** Bearer token is set on the default request headers and must never be logged or exposed.

## Cross-References
- [[AgentRequest]] – Input request object used by remote endpoints.
- [[AgentResponse]] – Output response object returned by remote endpoints.
- [[IAgxiomeAgent]] – Local agent contract; `IRemoteAgent` is a smaller transport-only surface.

## Source Files
- `AGXIOME.Core/Agent/RemoteAgent.cs` (ingested 2026-05-14)