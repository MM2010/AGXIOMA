using AGXIOME.Core.Agent;
using AGXIOME.Interfaces.Mcp;

namespace AGXIOME.CLI.Commands;

/// <summary>agxiome mcp — MCP server commands.</summary>
public sealed class McpCommand : ICliCommand
{
    public string Name => "mcp";
    public string Description => "MCP server commands (serve)";

    public ValueTask<CliResult> ExecuteAsync(string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0 || args[0] != "serve")
            return ValueTask.FromResult(CliResult.Fail("Usage: agxiome mcp serve [--transport stdio|sse|streamable-http] [--port 5100]"));

        return ExecuteServeAsync(args[1..], ct);
    }

    private static async ValueTask<CliResult> ExecuteServeAsync(string[] args, CancellationToken ct)
    {
        var transport = "stdio";
        var endpoint = "http://localhost:5100/mcp";
        var port = 5100;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--transport" && i + 1 < args.Length) transport = args[i + 1];
            if (args[i] == "--endpoint" && i + 1 < args.Length) endpoint = args[i + 1];
            if (args[i] == "--port" && i + 1 < args.Length) int.TryParse(args[i + 1], out port);
        }

        Console.Error.WriteLine($"Starting MCP server (transport={transport})...");

        var agent = AgentFactory.CreateTestAgent("mcp-agent");
        var server = new McpServer(agent, "agxiome-mcp", "1.0.0");

        IMcpTransport mcpTransport = transport.ToLowerInvariant() switch
        {
            "sse" => new AGXIOME.Interfaces.Mcp.Transports.SseHttpMcpTransport(endpoint),
            "streamable-http" => new AGXIOME.Interfaces.Mcp.Transports.StreamableHttpMcpTransport(endpoint),
            _ => new AGXIOME.Interfaces.Mcp.Transports.StdioMcpTransport()
        };

        await server.RunAsync(mcpTransport, ct);
        return CliResult.Ok("MCP server stopped.");
    }
}
