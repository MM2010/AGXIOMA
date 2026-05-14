---
title: McpTransportTests
category: entity
tags: [mcp, transport, testing, unit-test, dotnet]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# McpTransportTests

## Summary
The `McpTransportTests` class is a unit test suite that validates the creation and basic functionality of MCP transport implementations (`StdioMcpTransport`, `SseHttpMcpTransport`, `StreamableHttpMcpTransport`) and the `McpServer` component within the AGXIOMA framework. It ensures transports can be instantiated without exceptions, that the Streamable HTTP transport generates a session ID, and that the `McpServer` correctly handles JSON-RPC requests for initialization, tool listing, resource management, and unknown method errors.

## Key Details
- Contains 11 test methods covering transport creation, server setup, and JSON-RPC request processing.
- Tests verify `StdioMcpTransport`, `SseHttpMcpTransport`, `StreamableHttpMcpTransport` constructors do not throw.
- `StreamableHttpMcpTransport` tests confirm `SessionId` is generated and non-empty.
- `McpServer` is created using `AgentFactory.CreateTestAgent`.
- `McpServer.Initialize` returns server info including name and version.
- `McpServer.ToolsList` returns a list of tools with at least `"run_agent"`.
- `McpServer` supports adding and listing resources via `AddResource`.
- `McpServer.ResourcesRead` returns the text content of a resource.
- Unknown methods return JSON-RPC error code -32601 (Method not found).
- Tests use xUnit and `System.Text.Json` for response parsing.
- Source file: `AGXIOME.Tests/McpTransportTests.cs`.

## Cross-References
- [[StdioMcpTransport]] — implementation tested for creation.
- [[SseHttpMcpTransport]] — implementation tested for creation.
- [[StreamableHttpMcpTransport]] — implementation tested for creation and session ID.
- [[McpServer]] — central server class tested for request handling.
- [[AgentFactory]] — used to create test agents for server tests.

## Source Files
- `AGXIOME.Tests/McpTransportTests.cs` (ingested 2026-05-14)