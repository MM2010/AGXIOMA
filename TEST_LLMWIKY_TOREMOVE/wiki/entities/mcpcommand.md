---
title: McpCommand
category: entity
tags: [cli, mcp, command]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# McpCommand

## Summary
`McpCommand` is the CLI command implementation for exposing the AGXIOMA .NET agent framework's MCP server capabilities via the `agxiome mcp` subcommand. It parses command-line arguments to configure and start an MCP server using one of three transport modes (stdio, SSE, or streamable HTTP), then delegates to the core `McpServer` to run.

## Key Details
- **Name**: `"mcp"`
- **Description**: `"MCP server commands (serve)"`
- **Subcommand**: Only `serve` is supported; other arguments produce a usage error.
- **Transport selection**: `--transport` flag accepts `stdio` (default), `sse`, or `streamable-http`.
- **Endpoint**: `--endpoint` flag (only relevant for SSE and streamable HTTP, default `http://localhost:5100/mcp`).
- **Port**: `--port` flag (default `5100`); parsed as integer.
- Executes asynchronously via `ExecuteAsync`, running `McpServer.RunAsync` with the configured transport.
- Uses `AgentFactory.CreateTestAgent` to create an agent named `"mcp-agent"` for the server.
- Errors on unknown or missing `serve` argument, printing usage instructions to standard error.

## Cross-References
- [[McpServer]] — Core server class instantiated and run by this command.
- [[IMcpTransport]] — Interface implemented by all transport types used here.
- [[StdioMcpTransport]] — Default transport implementation (stdio).
- [[SseHttpMcpTransport]] — SSE transport implementation.
- [[StreamableHttpMcpTransport]] — Streamable HTTP transport implementation.
- [[AgentFactory]] — Factory method `CreateTestAgent` used to create the agent.

## Source Files
- `AGXIOME.CLI/Commands/McpCommand.cs` (ingested 2026-05-14)