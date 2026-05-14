---
title: SseHttpMcpTransport
category: entity
tags: [sse, mcp, transport, http]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# SseHttpMcpTransport

## Summary
`SseHttpMcpTransport` is a concrete implementation of the [[IMcpTransport]] interface that uses Server-Sent Events (SSE) over HTTP for MCP transport. It establishes a long-lived GET connection for receiving messages (SSE stream) and sends messages via POST requests to a specified endpoint. The transport lazily initializes the SSE reader on the first `ReceiveAsync` call and supports cancellation via `CancellationToken`.

## Key Details
- Implements `IMcpTransport` for the SSE/HTTP transport pattern.
- Constructor accepts an `endpoint` URL and an optional `HttpClient` (defaults to a new instance).
- `ConnectAsync` is a no-op; the SSE connection is established lazily on first `ReceiveAsync`.
- `SendAsync` posts a JSON string (UTF-8) to the endpoint using `POST` and ensures success.
- `ReceiveAsync` initiates a GET request with `ResponseHeadersRead` to begin streaming SSE events, then reads lines until a `"data: "` prefix is found and returns the payload.
- `DisconnectAsync` disposes the underlying `StreamReader` and sets it to `null`.
- Implements `IAsyncDisposable` to clean up both the reader and the `HttpClient`.
- Thread safety: not guaranteed; designed for single-consumer usage.

## Cross-References
- [[IMcpTransport]] — the interface contract that this class implements.
- [[McpTransport]] — may link to other transport implementations (e.g., Stdio, WebSocket) for comparison.

## Source Files
- `AGXIOME.Interfaces/Mcp/Transports/SseHttpMcpTransport.cs` (ingested 2026-05-14)