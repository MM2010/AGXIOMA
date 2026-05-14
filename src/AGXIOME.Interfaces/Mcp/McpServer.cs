using System.Text.Json;
using System.Text.Json.Nodes;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Interfaces.Mcp;

/// <summary>
/// Model Context Protocol (MCP) server for AGXIOME.
/// Exposes agents as MCP tools consumable by Claude, VS Code, and MCP clients.
/// Supports Stdio, SSE, and Streamable HTTP transports.
/// </summary>
public sealed class McpServer
{
    private readonly IAgxiomeAgent _agent;
    private readonly string _serverName;
    private readonly string _serverVersion;
    private readonly List<JsonObject> _additionalTools = new();
    private readonly List<McpResource> _resources = new();

    public McpServer(IAgxiomeAgent agent, string? name = null, string? version = null)
    {
        _agent = agent;
        _serverName = name ?? "agxiome-agent";
        _serverVersion = version ?? "1.0.0";
    }

    /// <summary>Register an additional tool schema exposed via tools/list.</summary>
    public void AddTool(JsonObject schema) => _additionalTools.Add(schema);

    /// <summary>Register a resource exposed via resources/list.</summary>
    public void AddResource(McpResource resource) => _resources.Add(resource);

    /// <summary>Process an MCP JSON-RPC request → response.</summary>
    public async Task<string> ProcessRequestAsync(string requestBody, CancellationToken ct = default)
    {
        using var doc = JsonDocument.Parse(requestBody);
        var root = doc.RootElement;
        var method = root.GetProperty("method").GetString() ?? "";
        var id = root.TryGetProperty("id", out var idEl) ? idEl.GetRawText() : null;

        return method switch
        {
            "initialize" => Ok(id, new
            {
                protocolVersion = "0.1.0",
                capabilities = new
                {
                    tools = new JsonObject(),
                    resources = new JsonObject(),
                    streaming = true
                },
                serverInfo = new { name = _serverName, version = _serverVersion }
            }),
            "tools/list" => Ok(id, new { tools = GetAllToolSchemas() }),
            "tools/call" => await HandleToolCallAsync(root, id, ct),
            "resources/list" => Ok(id, new { resources = _resources.Select(r => new
            {
                uri = r.Uri,
                name = r.Name,
                description = r.Description,
                mimeType = r.MimeType ?? "text/markdown"
            }).ToList() }),
            "resources/read" => await HandleResourceReadAsync(root, id, ct),
            _ => Err(id, -32601, $"Unknown method: {method}")
        };
    }

    /// <summary>Run the MCP server over a given transport.</summary>
    public async Task RunAsync(IMcpTransport transport, CancellationToken ct = default)
    {
        await transport.ConnectAsync(ct);

        while (!ct.IsCancellationRequested)
        {
            var request = await transport.ReceiveAsync(ct);
            if (request == null) break;

            var response = await ProcessRequestAsync(request, ct);
            await transport.SendAsync(response, ct);
        }

        await transport.DisconnectAsync(ct);
    }

    private JsonArray GetAllToolSchemas()
    {
        var schemas = new JsonArray { McpToolSchema };
        foreach (var t in _additionalTools)
            schemas.Add(t.DeepClone());
        return schemas;
    }

    private async Task<string> HandleToolCallAsync(JsonElement root, string? id, CancellationToken ct)
    {
        var args = root.GetProperty("params").GetProperty("arguments");
        var prompt = args.GetProperty("prompt").GetString() ?? "";
        var result = await _agent.RunAsync(new AgentRequest(prompt), ct);
        var content = new JsonArray { new JsonObject { ["type"] = "text", ["text"] = result.Content } };
        return Ok(id, new { content });
    }

    private Task<string> HandleResourceReadAsync(JsonElement root, string? id, CancellationToken ct)
    {
        var uri = root.GetProperty("params").GetProperty("uri").GetString() ?? "";
        var resource = _resources.FirstOrDefault(r => r.Uri == uri);
        if (resource == null)
            return Task.FromResult(Err(id, -32602, $"Resource not found: {uri}"));

        return Task.FromResult(Ok(id, new
        {
            contents = new JsonArray
            {
                new JsonObject
                {
                    ["uri"] = resource.Uri,
                    ["mimeType"] = resource.MimeType ?? "text/markdown",
                    ["text"] = resource.Content ?? ""
                }
            }
        }));
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

/// <summary>MCP resource definition.</summary>
public record McpResource(string Uri, string Name, string? Description = null, string? Content = null, string? MimeType = null);
