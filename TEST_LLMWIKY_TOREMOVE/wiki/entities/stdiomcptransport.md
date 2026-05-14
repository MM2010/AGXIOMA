---
title: StdioMcpTransport
category: entity
tags: [transport, mcp, stdio]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# StdioMcpTransport

## Summary
The `StdioMcpTransport` class implements `IMcpTransport` to communicate over standard input/output streams (stdin/stdout). It is designed for local process-based MCP communication, where a client and server exchange JSON-RPC messages via the console streams. The transport handles the JSON-RPC over stdin/stdout protocol by prepending a `Content-Length` header before each message and reading the header to reconstruct received messages.

## Key Details
- Implements `IMcpTransport` and is sealed.
- Constructor accepts optional `TextReader` and `TextWriter`; defaults to `Console.In` and `Console.Out`.
- `ConnectAsync` and `DisconnectAsync` return completed tasks (no actual connection management).
- `SendAsync` writes a `Content-Length` header (UTF-8 byte count) followed by the message and flushes the output.
- `ReceiveAsync` reads the `Content-Length` header line, skips the blank line, then reads exactly the specified number of characters.
- Returns `null` from `ReceiveAsync` when the input stream ends or header is malformed.
- Implements `IAsyncDisposable` via `DisposeAsync`, which calls `DisconnectAsync` once.

## Cross-References
- [[IMcpTransport]] — the interface this transport implements.
- [[McpTransport]] — abstract base or conceptual parent for all MCP transports.

## Source Files
- `AGXIOME.Interfaces/Mcp/Transports/StdioMcpTransport.cs` (ingested 2026-05-14)