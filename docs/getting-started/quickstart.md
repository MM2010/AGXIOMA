# Quick Start

> **Goal**: Create a working AI agent in under 5 minutes.

## Step 1: Install

```bash
dotnet new console -n MyFirstAgent
cd MyFirstAgent
dotnet add package AGXIOME.Core --prerelease
```

## Step 2: Get an API Key

| Provider | Free Tier | Get Key |
|----------|-----------|---------|
| **DeepSeek** | Yes (cheap) | [platform.deepseek.com](https://platform.deepseek.com) |
| **OpenAI** | Pay-as-you-go | [platform.openai.com](https://platform.openai.com) |
| **Ollama** | Free (local) | `ollama pull llama3.2` |
| **Groq** | Free tier | [console.groq.com](https://console.groq.com) |

## Step 3: Write Code

```csharp
using AGXIOME.Core;
using AGXIOME.Shared.Models;

// DeepSeek (cheapest, excellent quality)
var agent = AgxiomeBuilder.Agent("assistant")
    .UseDeepSeek("sk-your-key", "deepseek-chat")
    .WithSystemPrompt("You are a helpful AI assistant. Be concise.")
    .Build();

var response = await agent.RunAsync(new AgentRequest("What is the capital of France?"));
Console.WriteLine(response.Content); // Paris
```

### With Ollama (local, free)

```bash
ollama pull llama3.2
```

```csharp
var agent = AgxiomeBuilder.Agent("assistant")
    .UseOllama("llama3.2")  // defaults to http://localhost:11434
    .WithSystemPrompt("You are a helpful AI assistant.")
    .Build();
```

### With OpenAI

```csharp
var agent = AgxiomeBuilder.Agent("assistant")
    .UseOpenAI("sk-your-key", "gpt-4o-mini")
    .Build();
```

### With Anthropic Claude

```csharp
var agent = AgxiomeBuilder.Agent("assistant")
    .UseAnthropic("sk-ant-your-key", "claude-sonnet-4-20250514")
    .Build();
```

## Step 4: Run

```bash
dotnet run
```

## Step 5: Add Tools

```csharp
// Built-in tool — web search
agent.RegisterTool(new WebSearchTool("brave-api-key"));

// Custom function tool
public class CalculatorFunction : AgxiomeFunction
{
    public override string Name => "calculator";
    public override string Description => "Performs arithmetic";

    [ToolParameter(Required = true, Description = "Math expression")]
    public string Expression { get; set; } = "";

    public override async Task<ToolResult> ExecuteAsync(ToolContext ctx, CancellationToken ct)
    {
        var result = Evaluate(Expression);
        return new ToolResult(true, result);
    }
}

agent.RegisterFunction<CalculatorFunction>();
```

## Step 6: Go Deeper

- [Agent Lifecycle](../core-concepts/agent-lifecycle.md) — Full agent pipeline
- [Streaming Responses](../tools/streaming.md) — Real-time output
- [ForestSwarm Teams](../teams/forest-swarm.md) — Multi-agent routing
- [Cookbook](../../cookbook/README.md) — More recipes
