using AGXIOME.Core.Agent;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using System.Text.Json;

namespace AGXIOME.Adapters.A2A;

/// <summary>
/// Agent-to-Agent (A2A) protocol implementation.
/// Compatible with Google ADK A2A standard.
/// Allows AGXIOME agents to communicate with any A2A-compatible agent server.
/// </summary>
public sealed class A2AClient
{
    private readonly HttpClient _http;

    public A2AClient() => _http = new HttpClient();

    /// <summary>Send a message to an A2A-compatible agent and get response.</summary>
    public async Task<A2AResponse> SendMessageAsync(Uri agentUrl, string message, string? sessionId = null, CancellationToken ct = default)
    {
        var payload = new A2ARequest
        {
            Jsonrpc = "2.0",
            Method = "tasks/send",
            Params = new A2AParams
            {
                Id = Guid.NewGuid().ToString(),
                SessionId = sessionId,
                Message = new A2AMessage { Role = "user", Parts = [new() { Text = message }] }
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var response = await _http.PostAsync(agentUrl,
            new StringContent(json, System.Text.Encoding.UTF8, "application/json"), ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<A2AResponse>(body) ?? throw new InvalidOperationException("Invalid A2A response");
        return result;
    }

    /// <summary>Create a local A2A server handler that wraps an AGXIOME agent.</summary>
    public static A2AServerHandler CreateServer(IAgxiomeAgent agent) => new(agent);
}

/// <summary>A2A server handler: wraps an AGXIOME agent to be callable via A2A protocol.</summary>
public sealed class A2AServerHandler
{
    private readonly IAgxiomeAgent _agent;
    internal A2AServerHandler(IAgxiomeAgent agent) => _agent = agent;

    /// <summary>Process an incoming A2A JSON-RPC request and return response.</summary>
    public async Task<string> ProcessAsync(string requestBody, CancellationToken ct = default)
    {
        var request = JsonSerializer.Deserialize<A2ARequest>(requestBody);
        if (request?.Params?.Message == null)
            return ErrorResponse(request?.Id ?? "", -32600, "Invalid request");

        var text = request.Params.Message.Parts?.FirstOrDefault()?.Text ?? "";
        var result = await _agent.RunAsync(new AgentRequest(text), ct);

        var response = new A2AResponse
        {
            Jsonrpc = "2.0",
            Id = request.Id,
            Result = new A2AResult
            {
                Id = Guid.NewGuid().ToString(),
                Status = "completed",
                Message = new A2AMessage { Role = "agent", Parts = [new() { Text = result.Content }] }
            }
        };

        return JsonSerializer.Serialize(response);
    }

    private static string ErrorResponse(string id, int code, string message)
        => JsonSerializer.Serialize(new { jsonrpc = "2.0", id, error = new { code, message } });
}

// ── A2A Protocol Types ──

public record A2ARequest
{
    public string Jsonrpc { get; init; } = "2.0";
    public string Method { get; init; } = "";
    public A2AParams? Params { get; init; }
    public string Id { get; init; } = "";
}

public record A2AParams
{
    public string Id { get; init; } = "";
    public string? SessionId { get; init; }
    public A2AMessage? Message { get; init; }
}

public record A2AResponse
{
    public string Jsonrpc { get; init; } = "2.0";
    public string Id { get; init; } = "";
    public A2AResult? Result { get; init; }
}

public record A2AResult
{
    public string Id { get; init; } = "";
    public string Status { get; init; } = "";
    public A2AMessage? Message { get; init; }
}

public record A2AMessage
{
    public string Role { get; init; } = "";
    public List<A2APart> Parts { get; init; } = new();
}

public record A2APart
{
    public string? Text { get; init; }
    public string? Type { get; init; }
}
