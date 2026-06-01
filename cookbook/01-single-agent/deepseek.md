# Single Agent — DeepSeek

> Full demo: [`apps/agxioma-single-agent/`](../../apps/agxioma-single-agent/)

## Install

```bash
dotnet add package AGXIOME.Core --prerelease
```

## Minimal Agent

```csharp
using AGXIOME.Core;
using AGXIOME.Shared.Models;

var agent = AgxiomeBuilder.Agent("my-agent")
    .UseDeepSeek("sk-your-api-key", "deepseek-chat")
    .WithSystemPrompt("You are a helpful assistant.")
    .Build();

var response = await agent.RunAsync(new AgentRequest("Capital of France?"));
Console.WriteLine(response.Content); // Paris
```

## Streaming (SSE)

```csharp
app.MapGet("/chat/stream", async (string prompt, IAgxiomeAgent agent, HttpResponse res) =>
{
    res.ContentType = "text/event-stream";
    await foreach (var chunk in agent.RunStreamAsync(new AgentRequest(prompt)))
        await res.WriteAsync($"data: {JsonSerializer.Serialize(new { chunk.Content })}\n\n");
});
```

## Multi-turn Chat with History

```csharp
var store = new ConversationStore(); // simple in-memory store

app.MapPost("/chat", async (ChatRequest req, IAgxiomeAgent agent, ConversationStore store) =>
{
    var history = store.Get(req.SessionId);
    var request = new AgentRequest(req.Prompt, History: history);
    var response = await agent.RunAsync(request);
    store.Append(req.SessionId, req.Prompt, response.Content);
    return Results.Ok(new { response = response.Content });
});
```

## Configuration

```json
{
  "Agent": {
    "Id": "single-agent",
    "Model": "deepseek-chat",
    "SystemPrompt": "You are a helpful AI assistant.",
    "MaxLoops": 50
  },
  "LLMProvider": {
    "OpenAI": { "ApiKey": "sk-..." }
  }
}
```

## Health Check

```csharp
app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    agent  = "single-agent",
    utc    = DateTime.UtcNow
}));
```

## Next Steps

- [Add tools to the agent](../02-tools/README.md)
- [Streaming with SSE](../02-tools/streaming.md)
- [Structured output](../02-tools/structured-output.md)
