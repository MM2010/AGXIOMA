using AGXIOME.Core;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using AgxiomaToolAgent;
using AgxiomaToolAgent.Tools;
using System.Text;
using System.Text.Json;

// ── Bootstrap ────────────────────────────────────────────────────────────────
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient<WebSearchTool>();

builder.Services.AddSingleton<IAgxiomeAgent>(sp =>
{
    var cfg = builder.Configuration
        .GetSection("Agent")
        .Get<AgentSettings>() ?? new AgentSettings();

    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(WebSearchTool));

    return AgxiomeBuilder.Agent(cfg.Id)
        .UseOllama(model: cfg.Model ?? "llama3.1:8b")
        .WithSystemPrompt(cfg.SystemPrompt)
        .WithAutoLoops(upperBound: cfg.MaxLoops)
        .WithTool(new WebSearchTool())
        .WithTool(new ReadFileTool())
        .Build();
});

builder.Services.AddSingleton<ConversationStore>();

var app = builder.Build();

// ── Endpoints ────────────────────────────────────────────────────────────────

app.MapGet("/health", (IConfiguration cfg) => Results.Ok(new
{
    status  = "ok",
    agent   = cfg["Agent:Id"] ?? "tool-agent",
    tools   = new[] { "web_search", "read_file" },
    utc     = DateTime.UtcNow
}));

// Chat with tool calling enabled
app.MapPost("/chat", async (
    ChatRequest req,
    IAgxiomeAgent agent,
    ConversationStore store,
    CancellationToken ct) =>
{
    var history  = store.Get(req.SessionId);
    var response = await agent.RunAsync(
        new AgentRequest(req.Prompt, History: history.Count > 0 ? history : null), ct);

    store.Append(req.SessionId, req.Prompt, response.Content);

    return Results.Ok(new ChatResponse(
        response.Content,
        req.SessionId,
        response.ToolResults?.Select(t => new ToolCallSummary(t.ToolName, t.Success)).ToList()
            ?? new List<ToolCallSummary>()
    ));
});

// Streaming with tool call events
app.MapPost("/stream", async (
    ChatRequest req,
    IAgxiomeAgent agent,
    ConversationStore store,
    HttpContext http,
    CancellationToken ct) =>
{
    http.Response.ContentType = "text/event-stream";
    http.Response.Headers.CacheControl = "no-cache";

    var history  = store.Get(req.SessionId);
    var request  = new AgentRequest(req.Prompt, History: history.Count > 0 ? history : null);
    var full     = new StringBuilder();

    await foreach (var chunk in agent.RunStreamAsync(request, ct))
    {
        full.Append(chunk.Content);

        var type    = chunk.ToolCallId != null ? "tool" : "delta";
        var payload = JsonSerializer.Serialize(new
        {
            type,
            delta   = chunk.Content,
            toolId  = chunk.ToolCallId,
            done    = chunk.IsFinal
        });

        await http.Response.WriteAsync($"data: {payload}\n\n", ct);
        await http.Response.Body.FlushAsync(ct);
        if (chunk.IsFinal) break;
    }

    store.Append(req.SessionId, req.Prompt, full.ToString());
});

app.Run();
