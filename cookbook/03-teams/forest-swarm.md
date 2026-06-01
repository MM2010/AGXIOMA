# ForestSwarm — Domain-Based Agent Routing

> Full demo: [`apps/agxioma-team/`](../../apps/agxioma-team/)  
> Complex demo: [`apps/agxioma-support-hub/`](../../apps/agxioma-support-hub/)

## Pattern

ForestSwarm routes each user request to the best-matching "tree" (domain), then to the best agent within that tree using keyword matching.

```
User message ───→ ForestSwarm
                    ├── Tree("billing")   → keyword match → billing agent
                    ├── Tree("technical") → keyword match → tech agent
                    ├── Tree("sales")     → keyword match → sales agent
                    └── Tree("general")   → fallback → general agent
```

## Basic Setup — 4 Domains

```csharp
using AGXIOME.Core.Patterns;

builder.Services.AddAgxiome()
    .UseProvider("deepseek", "deepseek-chat")
    .AddForestSwarm("team", forest =>
    {
        forest.AddTree("billing")
            .AddAgent("billing-specialist",
                "You handle invoice, payment, and refund questions.")
            .WithKeywords("invoice", "payment", "refund", "subscription", "billing");

        forest.AddTree("technical")
            .AddAgent("tech-specialist",
                "You diagnose API errors and performance issues.")
            .WithKeywords("error", "bug", "api", "auth", "timeout");

        forest.AddTree("sales")
            .AddAgent("sales-specialist",
                "You explain plans, pricing, and enterprise options.")
            .WithKeywords("plan", "pricing", "trial", "enterprise");

        forest.AddTree("general")
            .AddAgent("general-assistant",
                "You handle general questions that don't fit other categories.")
            .WithKeywords("help", "question", "explain", "what", "how");
    });
```

## With Tools

```csharp
forest.AddTree("billing")
    .AddAgent("billing-specialist",
        "Use process_refund tool when a customer asks for a refund.")
    .WithTool(new GetInvoiceStatusTool())
    .WithTool(new ProcessRefundTool())
    .WithKeywords("invoice", "payment", "refund");

forest.AddTree("technical")
    .AddAgent("tech-specialist",
        "Check system status first. Create ticket for critical issues.")
    .WithTool(new GetSystemStatusTool())
    .WithTool(new CreateTicketTool())
    .WithKeywords("error", "crash", "outage", "down");
```

## Response Includes Routing Metadata

```csharp
app.MapPost("/chat", async (ChatRequest req, ForestSwarm team) =>
{
    var result = await team.RunAsync(req.Message);
    return Results.Ok(new
    {
        routedTo  = result.SelectedAgentId,   // which agent handled it
        tree      = result.SelectedTreeName,  // which domain
        keywords  = result.MatchedKeywords,   // what triggered the routing
        response  = result.Response,
        duration  = result.Duration.TotalMilliseconds
    });
});
```

## Next

- [GrokSwarm — Captain + Specialists](grok-swarm.md)
- [HeavySwarm — Parallel Research](heavy-swarm.md)
