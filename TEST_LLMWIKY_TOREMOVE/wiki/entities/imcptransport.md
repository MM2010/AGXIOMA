---
title: Imcptransport
category: entity
tags: [MCP, transport, interface, JSON-RPC]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Imcptransport

## Summary
The `IMcpTransport` interface defines the core abstraction for MCP (Model Context Protocol) transport mechanisms within the AGXIOMA framework. It provides asynchronous connect, send, receive, and disconnect operations for JSON-RPC messages, enabling support for multiple transport protocols such as Stdio, SSE, and Streamable HTTP. All implementations are required to implement `IAsyncDisposable` for resource cleanup.

## Key Details
- Defined in namespace `AGXIOME.Interfaces.Mcp`
- Implements `IAsyncDisposable`
- Core methods:
  - `ConnectAsync(CancellationToken ct)` — initializes the transport connection
  - `SendAsync(string message, CancellationToken ct)` — sends a JSON-RPC message
  - `ReceiveAsync(CancellationToken ct)` — receives a JSON-RPC message; returns `null` on clean close
  - `DisconnectAsync(CancellationToken ct)` — gracefully terminates the transport
- Transport agnostic: concrete implementations support Stdio, SSE, and Streamable HTTP
- All methods are cancellable via `CancellationToken`

## Cross-References
- [[StdioTransport]] — concrete implementation using standard input/output
- [[SseTransport]] — concrete implementation using Server-Sent Events
- [[StreamableHttpTransport]] — concrete implementation using streaming HTTP
- [[JsonRpc]] — JSON-RPC message format used by the transport

## Source Files
- `AGXIOME.Interfaces/Mcp/IMcpTransport.cs` (ingested 2026-05-14)