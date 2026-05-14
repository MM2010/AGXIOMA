# Interfaces — AGXIOME

Exponi agenti AGXIOME su Slack, Telegram, WhatsApp, Discord, AG-UI, A2A, MCP.

---

## Provider supportati

| Interfaccia | Namespace | Classe | Backend |
|-----------|-----------|--------|---------|
| **AG-UI** | `AGXIOME.Interfaces.AgUi` | `AgUiHost` | Web (Minimal API / SignalR) |
| **Slack** | `AGXIOME.Interfaces.Slack` | `SlackBot` | Slack Events API |
| **Telegram** | `AGXIOME.Interfaces.Telegram` | `TelegramBot` | Telegram Bot API |
| **WhatsApp** | `AGXIOME.Interfaces.WhatsApp` | `WhatsAppBot` | WhatsApp Cloud API |
| **Discord** | `AGXIOME.Interfaces.Discord` | `DiscordBot` | Discord Interactions API |
| **A2A** | `AGXIOME.Interfaces.A2A` | `A2AClient` / `A2AServerHandler` | Agent-to-Agent JSON-RPC |
| **MCP** | `AGXIOME.Interfaces.Mcp` | `McpServer` | Model Context Protocol |

---

## AG-UI (Web Chat)

ChatGPT-like web interface per agenti AGXIOME.

```csharp
var host = new AgUiHost();
var session = host.CreateSession("user-1", agent);

// Bloccante
var response = await host.SendMessageAsync(session.Id, "Ciao!");
Console.WriteLine(response.Content);

// Streaming
await foreach (var chunk in host.SendMessageStreamAsync(session.Id, "Racconta una storia"))
    Console.Write(chunk);

// History
var history = host.GetHistoryJson(session.Id);
```

## Slack

```csharp
var slack = new SlackBot(
    botToken: "xoxb-...",
    signingSecret: "...",
    agent: agent
);
await slack.InitializeAsync();

// In ASP.NET endpoint:
app.MapPost("/slack/events", async (HttpContext ctx) =>
{
    using var reader = new StreamReader(ctx.Request.Body);
    var body = await reader.ReadToEndAsync();
    var result = await slack.ProcessEventAsync(body);
    return result != null ? Results.Ok(result) : Results.Ok();
});
```

## Telegram

```csharp
var tg = new TelegramBot("BOT_TOKEN", agent);
await tg.SetWebhookAsync("https://example.com/telegram/webhook");

// Webhook endpoint:
app.MapPost("/telegram/webhook", async (HttpContext ctx) =>
{
    using var reader = new StreamReader(ctx.Request.Body);
    var body = await reader.ReadToEndAsync();
    await tg.ProcessUpdateAsync(body);
    return Results.Ok();
});
```

## WhatsApp

```csharp
var wa = new WhatsAppBot("PHONE_NUMBER_ID", "ACCESS_TOKEN", agent);

// Webhook endpoint:
app.MapPost("/whatsapp/webhook", async (HttpContext ctx) =>
{
    using var reader = new StreamReader(ctx.Request.Body);
    var body = await reader.ReadToEndAsync();
    await wa.ProcessWebhookAsync(body);
    return Results.Ok();
});
```

## Discord

```csharp
var dc = new DiscordBot("APP_ID", "BOT_TOKEN", "PUBLIC_KEY", agent);
await dc.RegisterSlashCommandAsync("ask", "Ask the agent a question");

// Interactions endpoint:
app.MapPost("/discord/interactions", async (HttpContext ctx) =>
{
    using var reader = new StreamReader(ctx.Request.Body);
    var body = await reader.ReadToEndAsync();
    var result = await dc.ProcessInteractionAsync(body);
    return result != null ? Results.Ok(result) : Results.Ok();
});
```

## A2A (Agent-to-Agent)

### Client — chiama un altro agente A2A

```csharp
var a2a = new A2AClient();
var response = await a2a.SendMessageAsync(
    new Uri("http://localhost:5000/a2a"), "Ciao, analizza questi dati");
Console.WriteLine(response.Result?.Message?.Parts?.FirstOrDefault()?.Text);
```

### Server — esponi un agente come A2A

```csharp
var handler = new A2AServerHandler(agent);

app.MapPost("/a2a", async (HttpContext ctx) =>
{
    using var reader = new StreamReader(ctx.Request.Body);
    var body = await reader.ReadToEndAsync();
    var result = await handler.ProcessAsync(body);
    return Results.Ok(result);
});
```

## MCP (Model Context Protocol)

Espone un agente AGXIOME come MCP tool per Claude, VS Code, ecc.

```csharp
var mcp = new McpServer(agent);

// Endpoint MCP JSON-RPC:
app.MapPost("/mcp", async (HttpContext ctx) =>
{
    using var reader = new StreamReader(ctx.Request.Body);
    var body = await reader.ReadToEndAsync();
    var result = await mcp.ProcessRequestAsync(body);
    return Results.Ok(result);
});

// MCP tool schema esposto:
// {
//   "name": "run_agent",
//   "description": "Execute the AGXIOME agent with a prompt",
//   "inputSchema": { "type": "object", "properties": { "prompt": { "type": "string" } } }
// }
```
