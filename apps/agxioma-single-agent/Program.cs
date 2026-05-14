using AGXIOME.Core;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using AGXIOME.Shared.Config;
using AgxiomaSingleAgent;
using System.Text;
using System.Text.Json;

// ── Bootstrap ────────────────────────────────────────────────────────────────
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IAgxiomeAgent>(sp =>
{
    var cfg = builder.Configuration
        .GetSection("Agent")
        .Get<AgentSettings>() ?? new AgentSettings();

    var apiKey = builder.Configuration["LLMProvider:OpenAI:ApiKey"];
    if (string.IsNullOrEmpty(apiKey))
        apiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");

    return AgxiomeBuilder.Agent(cfg.Id)
        .UseDeepSeek(apiKey, cfg.Model ?? "deepseek-chat")
        .WithSystemPrompt(cfg.SystemPrompt)
        .WithAutoLoops(upperBound: cfg.MaxLoops)
        .Build();
});

builder.Services.AddSingleton<ConversationStore>();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// ── Endpoints ────────────────────────────────────────────────────────────────

// Health check
app.MapGet("/health", (IConfiguration cfg) => Results.Ok(new
{
    status = "ok",
    agent  = cfg["Agent:Id"] ?? "single-agent",
    utc    = DateTime.UtcNow
}));

// Single-turn chat (request / response)
app.MapPost("/chat", async (
    ChatRequest req,
    IAgxiomeAgent agent,
    ConversationStore store,
    CancellationToken ct) =>
{
    var history = store.Get(req.SessionId);

    var request = new AgentRequest(
        req.Prompt,
        History: history.Count > 0 ? history : null
    );

    var response = await agent.RunAsync(request, ct);

    store.Append(req.SessionId, req.Prompt, response.Content);

    return Results.Ok(new ChatResponse(
        response.Content,
        req.SessionId,
        history.Count + 1
    ));
});

// Multi-turn streaming (Server-Sent Events)
app.MapPost("/stream", async (
    ChatRequest req,
    IAgxiomeAgent agent,
    ConversationStore store,
    HttpContext http,
    CancellationToken ct) =>
{
    http.Response.ContentType = "text/event-stream";
    http.Response.Headers.CacheControl = "no-cache";
    http.Response.Headers.Connection   = "keep-alive";

    var history = store.Get(req.SessionId);
    var request = new AgentRequest(req.Prompt, History: history.Count > 0 ? history : null);

    var fullResponse = new StringBuilder();

    await foreach (var chunk in agent.RunStreamAsync(request, ct))
    {
        fullResponse.Append(chunk.Content);

        var payload = JsonSerializer.Serialize(new { delta = chunk.Content, done = chunk.IsFinal });
        await http.Response.WriteAsync($"data: {payload}\n\n", ct);
        await http.Response.Body.FlushAsync(ct);

        if (chunk.IsFinal) break;
    }

    store.Append(req.SessionId, req.Prompt, fullResponse.ToString());
});

// Clear session history
app.MapDelete("/session/{sessionId}", (string sessionId, ConversationStore store) =>
{
    store.Clear(sessionId);
    return Results.Ok(new { cleared = sessionId });
});

app.Run();
