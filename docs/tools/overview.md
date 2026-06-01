# Tool System · # Structured Output · # Workflows · # Skills · # ASP.NET Core Host

> **Status**: Core documentation pages created. Remaining pages reference existing content in the cookbook, API reference, and demo apps.

## Tools

[Cookbook: Tool Agent](../../cookbook/02-tools/) · [API: IAgxiomeTool](../API.md)

```csharp
// Built-in tool
agent.RegisterTool(new WebSearchTool("brave-key"));

// Typed function (auto JSON Schema)
agent.RegisterFunction<GetWeatherFunction>();

// Custom function
public class CalculatorFunction : AgxiomeFunction
{
    public override string Name => "calculator";
    public override string Description => "Performs arithmetic";

    [ToolParameter(Required = true, Description = "Expression")]
    public string Expression { get; set; } = "";

    public override async Task<ToolResult> ExecuteAsync(ToolContext ctx, CancellationToken ct)
        => new ToolResult(true, Evaluate(Expression));
}
```

## Structured Output

[API: IOutputParser, IResponseModel](../API.md)

```csharp
agent.SetOutputSchema<StockQuote>();
var quote = await agent.RunAsync<StockQuote>(new AgentRequest("AAPL price?"));
// quote = { Symbol: "AAPL", Price: 198.50, Change: 2.3, Currency: "USD" }
```

## Workflows

[Demo: incident-pipeline](../../apps/agxioma-incident-pipeline/) · [Cookbook: Domains](../../cookbook/05-domains/)

```csharp
var workflow = new AgxiomeWorkflow("incident-response");
workflow.AddStep("detect", async ctx => { /* ... */ });
workflow.AddStep("classify", async ctx => { /* ... */ });
workflow.AddAgentStep("resolve", supportAgent);
workflow.AddTransition("detect", "classify");
workflow.AddTransition("classify", "resolve", ctx => ctx["severity"] == "critical");

await workflow.ExecuteAsync(new WorkflowContext());
```

## Skills

[API: ISkillManager, IAgxiomeSkill](../API.md) · [Demo: dynamic-skill-lab](../../apps/agxioma-dynamic-skill-lab/)

```csharp
var skillManager = new SkillManager();
skillManager.RegisterFromPackage(new SkillPackage(
    "code-review", "Code Review", "Reviews pull requests",
    "1.0.0", "development", "AGXIOME Team",
    "/skills/code-review/SKILL.md", null, null,
    new[] { "code" }, Array.Empty<string>()));

skillManager.ApplySkillToAgent(agent, "code-review");
```

## ASP.NET Core Host

[Demo: single-agent](../../apps/agxioma-single-agent/) · [API: Runtime Controllers](../API.md)

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAgxiome()
    .UseProvider("deepseek", "deepseek-chat")
    .AddForestSwarm("support", forest => { /* ... */ });

var app = builder.Build();

app.MapPost("/chat", async (ForestSwarm team, ChatRequest req) =>
{
    var result = await team.RunAsync(req.Message);
    return Results.Ok(new { response = result.Response });
});

app.Run();
```

## Next

- [Full Documentation Index](../INDEX.md)
- [Cookbook](../../cookbook/README.md)
- [API Reference](../API.md)
