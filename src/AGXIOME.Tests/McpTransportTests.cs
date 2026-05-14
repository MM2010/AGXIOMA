using System.Text.Json;
using AGXIOME.Core.Agent;
using AGXIOME.Interfaces.Mcp;
using AGXIOME.Interfaces.Mcp.Transports;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Tests;

public class McpTransportTests
{
    [Fact]
    public void StdioMcpTransport_Create_DoesNotThrow()
    {
        var transport = new StdioMcpTransport();
        Assert.NotNull(transport);
    }

    [Fact]
    public void SseHttpMcpTransport_Create_DoesNotThrow()
    {
        var transport = new SseHttpMcpTransport("http://localhost:5100/mcp");
        Assert.NotNull(transport);
    }

    [Fact]
    public void StreamableHttpMcpTransport_Create_DoesNotThrow()
    {
        var transport = new StreamableHttpMcpTransport("http://localhost:5100/mcp");
        Assert.NotNull(transport);
        Assert.NotNull(transport.SessionId);
    }

    [Fact]
    public void StreamableHttpMcpTransport_SessionId_IsNotEmpty()
    {
        var transport = new StreamableHttpMcpTransport("http://localhost:5100/mcp");
        Assert.False(string.IsNullOrEmpty(transport.SessionId));
    }

    [Fact]
    public void McpServer_Create_DoesNotThrow()
    {
        var agent = AgentFactory.CreateTestAgent("test");
        var server = new McpServer(agent);
        Assert.NotNull(server);
    }

    [Fact]
    public void McpServer_AddResource_Works()
    {
        var agent = AgentFactory.CreateTestAgent("test");
        var server = new McpServer(agent);
        server.AddResource(new McpResource("/memory/latest", "Latest Memory", "Recent agent memory"));
        // No exception = success
    }

    [Fact]
    public async Task McpServer_Initialize_ReturnsCapabilities()
    {
        var agent = AgentFactory.CreateTestAgent("test");
        var server = new McpServer(agent, "test-server", "1.0.0");
        var request = @"{""jsonrpc"":""2.0"",""method"":""initialize"",""id"":1}";

        var response = await server.ProcessRequestAsync(request);

        using var doc = JsonDocument.Parse(response);
        Assert.Equal("1", doc.RootElement.GetProperty("id").ToString());
        Assert.True(doc.RootElement.TryGetProperty("result", out var result));
        Assert.Equal("test-server", result.GetProperty("serverInfo").GetProperty("name").GetString());
    }

    [Fact]
    public async Task McpServer_ToolsList_ReturnsTools()
    {
        var agent = AgentFactory.CreateTestAgent("test");
        var server = new McpServer(agent);
        var request = @"{""jsonrpc"":""2.0"",""method"":""tools/list"",""id"":1}";

        var response = await server.ProcessRequestAsync(request);

        using var doc = JsonDocument.Parse(response);
        var tools = doc.RootElement.GetProperty("result").GetProperty("tools");
        Assert.True(tools.GetArrayLength() >= 1);
        Assert.Equal("run_agent", tools[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task McpServer_ResourcesList_ReturnsResources()
    {
        var agent = AgentFactory.CreateTestAgent("test");
        var server = new McpServer(agent);
        server.AddResource(new McpResource("memory://latest", "Latest", "Content", "# Memory"));

        var request = @"{""jsonrpc"":""2.0"",""method"":""resources/list"",""id"":1}";
        var response = await server.ProcessRequestAsync(request);

        using var doc = JsonDocument.Parse(response);
        var resources = doc.RootElement.GetProperty("result").GetProperty("resources");
        Assert.Equal(1, resources.GetArrayLength());
        Assert.Equal("memory://latest", resources[0].GetProperty("uri").GetString());
    }

    [Fact]
    public async Task McpServer_ResourcesRead_ReturnsContent()
    {
        var agent = AgentFactory.CreateTestAgent("test");
        var server = new McpServer(agent);
        server.AddResource(new McpResource("memory://latest", "Latest", "Content", "# Hello World", "text/markdown"));

        var request = @"{""jsonrpc"":""2.0"",""method"":""resources/read"",""params"":{""uri"":""memory://latest""},""id"":1}";
        var response = await server.ProcessRequestAsync(request);

        using var doc = JsonDocument.Parse(response);
        var contents = doc.RootElement.GetProperty("result").GetProperty("contents");
        Assert.Equal("# Hello World", contents[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task McpServer_UnknownMethod_ReturnsError()
    {
        var agent = AgentFactory.CreateTestAgent("test");
        var server = new McpServer(agent);

        var request = @"{""jsonrpc"":""2.0"",""method"":""unknown"",""id"":1}";
        var response = await server.ProcessRequestAsync(request);

        using var doc = JsonDocument.Parse(response);
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
        Assert.Equal(-32601, doc.RootElement.GetProperty("error").GetProperty("code").GetInt32());
    }
}
