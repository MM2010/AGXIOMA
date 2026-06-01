# AgxiomeBuilder — Fluent API

> The `AgxiomeBuilder` is the primary entry point for creating agents. Every agent starts here.

## Basic Usage

```csharp
var agent = AgxiomeBuilder.Agent("agent-id")
    .UseDeepSeek("sk-...", "deepseek-chat")
    .WithSystemPrompt("You are a helpful assistant.")
    .Build();
```

## Choosing a Provider

```csharp
// ── Major Cloud Providers ──
.UseOpenAI("sk-...", "gpt-4o")
.UseAnthropic("sk-ant-...", "claude-sonnet-4-20250514")
.UseGemini("key", "gemini-2.0-flash")
.UseDeepSeek("sk-...", "deepseek-chat")
.UseGroq("key", "llama-3.3-70b")
.UseMistral("key", "mistral-large")

// ── Local Providers (no API key needed) ──
.UseOllama("llama3.2")              // http://localhost:11434
.UseLlamaCpp("http://localhost:8080")
.UseLMStudio("http://localhost:1234")

// ── Generic ──
.UseModel(LLMProviderType.OpenAI, "gpt-4o-mini", "sk-...")
```

## Prompt Configuration

```csharp
var agent = AgxiomeBuilder.Agent("agent")
    .WithSystemPrompt("You are an expert software architect.")
    .WithInstructions(new[] {
        "Be concise — prefer bullet points.",
        "Include code examples when relevant.",
        "Always state caveats and limitations."
    })
    .WithFewShotExamples(new Dictionary<string, string> {
        ["What is SOLID?"] = "SOLID is an acronym for five design principles...",
        ["Explain DI"]    = "Dependency Injection is a pattern where..."
    })
    .Build();
```

## Tools & Functions

```csharp
var agent = AgxiomeBuilder.Agent("agent")
    // Pre-built tool instance
    .WithTool(new WebSearchTool("brave-key"))

    // Typed function (auto JSON Schema)
    .WithFunction<GetWeatherFunction>()
    .WithFunction<CalculatorFunction>()

    // Tool with confirmation required
    .WithFunction<DeleteFileFunction>()
    .RequireConfirmationFor<DeleteFileFunction>()
    .Build();
```

## Guardrails & Hooks

```csharp
var agent = AgxiomeBuilder.Agent("agent")
    .WithGuardrail(new PiiDetectionGuardrail())
    .WithGuardrail(new PromptInjectionGuardrail())
    .WithHook(new AuditHook())
    .WithHook(new MetricsHook())
    .Build();
```

## Learning

```csharp
var agent = AgxiomeBuilder.Agent("agent")
    .WithLearning(new PostgresLearningMachine(connectionString))
    .WithLearningConfig(new LearningConfig {
        UserMemoryMode = LearningMode.Always,
        EnableOptimization = true
    })
    .Build();
```

## Structured Output

```csharp
var agent = AgxiomeBuilder.Agent("agent")
    .WithOutputSchema<StockQuote>()
    .Build();

var quote = await agent.RunAsync<StockQuote>(
    new AgentRequest("What is the price of AAPL?"));

public record StockQuote(string Symbol, decimal Price, decimal Change, string Currency);
```

## ASP.NET Core DI

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAgxiome()
    .UseProvider("deepseek", "deepseek-chat")
    .AddForestSwarm("support", forest => { /* ... */ });

var app = builder.Build();

// Inject into controllers or minimal APIs
app.MapPost("/chat", async (ForestSwarm team, ChatRequest req) =>
{
    var result = await team.RunAsync(req.Message);
    return Results.Ok(new { response = result.Response });
});
```

## Advanced: Middleware

```csharp
var agent = AgxiomeBuilder.Agent("agent")
    .WithMiddleware(async (ctx, next) =>
    {
        var sw = Stopwatch.StartNew();
        await next();
        Console.WriteLine($"Agent run: {sw.ElapsedMilliseconds}ms");
    })
    .Build();
```

## Next

- [Agent Lifecycle](agent-lifecycle.md)
- [Teams & Swarms](../../teams/topologies.md)
