using System.Text.Json;
using System.Text.Json.Nodes;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Interfaces.Mcp;

/// <summary>
/// Model Context Protocol (MCP) server for AGXIOME.
/// Exposes agents as MCP tools consumable by Claude, VS Code, and MCP clients.
/// Supports Stdio and SSE transport.
/// </summary>
public sealed class McpServer
{
    private readonly IAgxiomeAgent _agent;
    private readonly string _serverName;
    private readonly string _serverVersion;

    public McpServer(IAgxiomeAgent agent, string? name = null, string? version = null)
    {
        _agent = agent;
        _serverName = name ?? "agxiome-agent";
        _serverVersion = version ?? "1.0.0";
    }

    /// <summary>Process an MCP JSON-RPC request → response.</summary>
    public async Task<string> ProcessRequestAsync(string requestBody, CancellationToken ct = default)
    {
        using var doc = JsonDocument.Parse(requestBody);
        var root = doc.RootElement;
        var method = root.GetProperty("method").GetString() ?? "";
        var id = root.TryGetProperty("id", out var idEl) ? idEl.GetRawText() : null;

        return method switch
        {
            "initialize" => Ok(id, new { protocolVersion = "0.1.0", capabilities = new { tools = new JsonObject() }, serverInfo = new { name = _serverName, version = _serverVersion } }),
            "tools/list" => Ok(id, new { tools = new JsonArray { McpToolSchema } }),
            "tools/call" => await HandleToolCallAsync(root, id, ct),
            _ => Err(id, -32601, $"Unknown method: {method}")
        };
    }

    private async Task<string> HandleToolCallAsync(JsonElement root, string? id, CancellationToken ct)
    {
        var args = root.GetProperty("params").GetProperty("arguments");
        var prompt = args.GetProperty("prompt").GetString() ?? "";
        var result = await _agent.RunAsync(new AgentRequest(prompt), ct);
        var content = new JsonArray { new JsonObject { ["type"] = "text", ["text"] = result.Content } };
        return Ok(id, new { content });
    }

    private static string Ok(string? id, object result)
        => JsonSerializer.Serialize(new { jsonrpc = "2.0", id, result });

    private static string Err(string? id, int code, string message)
        => JsonSerializer.Serialize(new { jsonrpc = "2.0", id, error = new { code, message } });

    private static JsonObject McpToolSchema => new()
    {
        ["name"] = "run_agent",
        ["description"] = "Execute the AGXIOME agent with a prompt",
        ["inputSchema"] = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject { ["prompt"] = new JsonObject { ["type"] = "string", ["description"] = "The prompt" } },
            ["required"] = new JsonArray { "prompt" }
        }
    };
}
