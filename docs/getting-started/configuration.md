# Configuration

AGXIOME uses standard .NET configuration patterns: `appsettings.json`, environment variables, and dependency injection.

## appsettings.json

```json
{
  "Agent": {
    "Id": "my-agent",
    "Provider": "deepseek",
    "Model": "deepseek-chat",
    "SystemPrompt": "You are a helpful AI assistant.",
    "MaxLoops": 50,
    "Temperature": 0.7
  },
  "LLMProvider": {
    "OpenAI": {
      "ApiKey": "sk-..."
    },
    "DeepSeek": {
      "ApiKey": "sk-..."
    },
    "Anthropic": {
      "ApiKey": "sk-ant-..."
    }
  },
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Database=agxiome;Username=postgres;Password=...",
    "Redis": "localhost:6379,abortConnect=false"
  }
}
```

## Environment Variables

```bash
# LLM API keys
export DEEPSEEK_API_KEY="sk-..."
export OPENAI_API_KEY="sk-..."
export ANTHROPIC_API_KEY="sk-ant-..."
export GROQ_API_KEY="gsk_..."

# Database
export AGXIOME_POSTGRES_TEST_CONNECTION="Host=localhost;..."

# Redis
export AGXIOME_REDIS_CONNECTION="localhost:6379"
```

## Configure in Code

```csharp
var agent = AgxiomeBuilder.Agent("agent")
    .UseDeepSeek(
        apiKey: Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY"),
        model: "deepseek-chat")
    .WithSystemPrompt("You are a helpful assistant.")
    .WithAutoLoops(upperBound: 50)
    .Build();
```

## Learning Configuration

```csharp
var learningConfig = new LearningConfig
{
    UserMemoryMode = LearningMode.Always,      // auto-save user preferences
    UserProfileMode = LearningMode.Always,     // auto-save structured profile
    EntityMemoryMode = LearningMode.Agentic,   // agent decides when to save
    SessionContextMode = LearningMode.Always,  // auto-summarize sessions
    EnableOptimization = true,
    MaxMemoriesBeforeOptimization = 50
};
```

## Guardrails Configuration

```csharp
var govConfig = new GovernanceConfig
{
    EnableHITL = true,           // Human-in-the-Loop
    EnableAuditHooks = true,     // Log every agent run
    EnableBackgroundEvals = false // Background quality evaluation
};
```

## Next

- [Agent Lifecycle](../core-concepts/agent-lifecycle.md)
- [AgxiomeBuilder](../core-concepts/agxiome-builder.md)
