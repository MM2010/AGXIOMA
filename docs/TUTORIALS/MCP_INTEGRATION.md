# MCP Integration — Model Context Protocol

AGXIOME supports the Model Context Protocol (MCP) for integration with Claude, VS Code, and other MCP clients.

## Server Setup

```csharp
using AGXIOME.Interfaces.Mcp;
using AGXIOME.Interfaces.Mcp.Transports;

var agent = AgxiomeBuilder.Agent("mcp-agent").UseDeepSeek().Build();
var server = new McpServer(agent, "my-agent", "1.0.0");

// Add resources
server.AddResource(new McpResource(
    "memory://recent",
    "Recent Memory",
    "Recent agent interactions",
    "# Memory Content",
    "text/markdown"
));

// Run via Stdio transport
await server.RunAsync(new StdioMcpTransport());
```

## Transport Options

```csharp
// Stdio (default) — communicates over stdin/stdout
var stdio = new StdioMcpTransport();

// SSE HTTP
var sse = new SseHttpMcpTransport("http://localhost:5100/mcp");

// Streamable HTTP
var streamable = new StreamableHttpMcpTransport("http://localhost:5100/mcp");
```

## CLI Server

```bash
# Stdio transport (default)
agxiome mcp serve

# SSE HTTP transport
agxiome mcp serve --transport sse --port 5100

# Streamable HTTP
agxiome mcp serve --transport streamable-http --endpoint http://localhost:5100/mcp
```

## MCP Methods

| Method | Description |
|--------|-------------|
| `initialize` | Protocol handshake, returns capabilities |
| `tools/list` | Lists available tools |
| `tools/call` | Executes a tool |
| `resources/list` | Lists available resources |
| `resources/read` | Reads a resource's content |
