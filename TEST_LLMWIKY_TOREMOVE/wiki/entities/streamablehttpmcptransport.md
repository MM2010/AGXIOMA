---
title: StreamableHttpMcpTransport
category: entity
tags: [mcp, transport, http, sse, streaming]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# StreamableHttpMcpTransport

## Summary
`StreamableHttpMcpTransport` is a sealed implementation of [[IMcpTransport]] that uses HTTP with Server-Sent Events (SSE) for streaming responses. It enables lightweight, connectionless communication over standard HTTP by posting JSON messages and receiving event-stream data, making it suitable for environments where persistent connections are undesirable.

## Key Details
- Implements `IMcpTransport` with HTTP POST for sending and HTTP GET with SSE for receiving.
- Constructor accepts `endpoint`, optional `HttpClient`, and optional `sessionId` (defaults to a new GUID).
- `ConnectAsync` no‑op: streamable HTTP has no explicit connection phase.
- `SendAsync` sends a POST with `MCP-Session-Id` header and JSON content; ensures success status.
- `ReceiveAsync` sends a GET to `{endpoint}?sessionId={sessionId}` with `Accept: text/event-stream`, reads the SSE stream, and returns the first `data:` line’s content.
- `DisconnectAsync` no‑op: no server‑side disconnect required.
- `DisposeAsync` disposes the internal `HttpClient`.
- Thread‑safe cancellation via `CancellationToken` in all async methods.

## Cross-References
- [[IMcpTransport]] — the interface this transport implements.
- [[SseStreamTransport]] — alternative transport using raw SSE streams.
- [[McpProtocol]] — context for the protocol layer using this transport.

## Source Files
- `AGXIOME.Interfaces/Mcp/Transports/StreamableHttpMcpTransport.cs` (ingested 2026-05-14)