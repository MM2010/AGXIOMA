---
title: McpServer
category: entity
tags: [mcp, server, protocol, agent, transport]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# McpServer

## Summary
The `McpServer` class implements a Model Context Protocol (MCP) server for the AGXIOME agent framework. It exposes agents as MCP tools consumable by clients such as Claude, VS Code, and any MCP‑compatible client. The server supports Stdio, SSE, and Streamable HTTP transports through a pluggable `IMcpTransport` interface. It handles JSON‑RPC methods for initialization, tool listing/calling, and resource listing/reading.

## Key Details
- Exposes a single agent (`IAgxiomeAgent`) as the default `run_agent` tool, accepting a `prompt` argument.
- Supports registration of additional tools via `AddTool(JsonObject schema)` and resources via `AddResource(McpResource resource)`.
- Processes JSON‑RPC requests via `ProcessRequestAsync(string requestBody, CancellationToken)`, delegating to internal handlers for `initialize`, `tools/list`, `tools/call`, `resources/list`, and `resources/read`.
- Runs indefinitely over a transport with `RunAsync(IMcpTransport, CancellationToken)`, calling connect/receive/process/send in a loop.
- Uses static helper methods `Ok(id, result)` and `Err(id, code, message)` to build standard JSON‑RPC 2.0 responses.
- The default tool schema (`McpToolSchema`) defines `run_agent` with input schema requiring a `prompt` string.
- `McpResource` is a simple record type for exposing read‑only resource endpoints.
- Server identity is configurable via constructor parameters `name` and `version` (defaults: `"agxiome-agent"`, `"1.0.0"`).

## Cross-References
- [[IAgxiomeAgent]] — the underlying agent logic executed when `tools/call` is invoked
- [[IMcpTransport]] — abstracts the transport layer (Stdio, SSE, Streamable HTTP)
- [[AgentRequest]] — the input model used to call the agent
- [[McpToolSchema]] — static property defining the default agent tool schema

## Source Files
- `AGXIOME.Interfaces/Mcp/McpServer.cs` (ingested 2026-05-14)