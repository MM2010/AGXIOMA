# AGXIOME — Agentic Orchestration Framework for .NET 10

<p align="center">
  <img src="https://raw.githubusercontent.com/AGXIOME/AGXIOMA/main/docs/agxiome-logo.png" alt="AGXIOME" width="400"/>
</p>

**Version 0.0.2-beta** · [agxiome.com](https://agxiome.com) · [Documentation](https://github.com/AGXIOME/AGXIOMA/tree/main/docs)

[![NuGet](https://img.shields.io/nuget/vpre/AGXIOME.Core)](https://www.nuget.org/packages/AGXIOME.Core)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple)](https://dotnet.microsoft.com/)
[![Website](https://img.shields.io/badge/website-agxiome.com-blue)](https://agxiome.com)

---

## Installation

```bash
dotnet add package AGXIOME.Core --prerelease
```

This package pulls in `AGXIOME.Shared` automatically. For specific features, add individual packages:

```bash
dotnet add package AGXIOME.LLMWIKY --prerelease     # LLM inference engine (50+ providers)
dotnet add package AGXIOME.Storage --prerelease      # CQRS stores + vector DBs + chunkers
dotnet add package AGXIOME.Orchestration --prerelease # Multi-agent orchestrator
dotnet add package AGXIOME.Toolkits --prerelease     # 161 prebuilt toolkits
dotnet add package AGXIOME.Security --prerelease     # Crypto, RBAC, audit, guardrails
dotnet add package AGXIOME.Realtime --prerelease     # SignalR hubs + backplane
dotnet add package AGXIOME.Telemetry --prerelease    # OpenTelemetry metrics
dotnet add package AGXIOME.Embedders --prerelease    # 25 embedding providers
dotnet add package AGXIOME.Readers --prerelease      # 28 document readers
dotnet add package AGXIOME.XLink --prerelease        # Typed transport protocol
```

---

## Quick Start — Single Agent

```csharp
using AGXIOME.Core;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

// Create an agent with DeepSeek
var agent = AgxiomeBuilder.Agent("my-agent")
    .UseDeepSeek("sk-your-api-key", "deepseek-chat")
    .WithSystemPrompt("You are a helpful assistant.")
    .Build();

var response = await agent.RunAsync(new AgentRequest("What is the capital of France?"));
Console.WriteLine(response.Content); // "Paris"
```

## Streaming

```csharp
await foreach (var chunk in agent.RunStreamAsync(new AgentRequest("Explain quantum computing")))
    Console.Write(chunk.Content);
```

## Structured Output

```csharp
agent.SetOutputSchema<StockQuote>();
var quote = await agent.RunAsync<StockQuote>(new AgentRequest("AAPL price?"));
Console.WriteLine($"{quote.Symbol}: ${quote.Price}");

public record StockQuote(string Symbol, decimal Price, decimal Change, string Currency);
```

## Team — ForestSwarm (Domain-Based Routing)

```csharp
using AGXIOME.Core.Patterns;

builder.Services.AddAgxiome()
    .UseProvider("deepseek", "deepseek-chat")
    .AddForestSwarm("support", forest =>
    {
        forest.AddTree("billing")
            .AddAgent("billing-specialist", "You handle invoice and payment questions.")
            .WithKeywords("invoice", "payment", "refund", "billing");

        forest.AddTree("technical")
            .AddAgent("tech-specialist", "You diagnose API errors and performance issues.")
            .WithKeywords("error", "api", "auth", "timeout", "crash");
    });
```

## Code Review — GrokSwarm (Captain + Specialists)

```csharp
var captain = AgxiomeBuilder.Agent("captain")
    .UseModel(LLMProviderType.Anthropic, "claude-sonnet-4-20250514")
    .WithSystemPrompt("You are the Lead Code Review Architect.")
    .Build();

var bugs = AgxiomeBuilder.Agent("hunter")
    .WithSystemPrompt("Find bugs, null-refs, race conditions, security vulnerabilities.")
    .Build();

var perf = AgxiomeBuilder.Agent("optimizer")
    .WithSystemPrompt("Find O(n2) patterns, memory leaks, N+1 queries.")
    .Build();
```

## Guardrails & Security

```csharp
agent.AddGuardrail(new PiiDetectionGuardrail());
agent.AddGuardrail(new PromptInjectionGuardrail());
agent.AddHook(new AuditHook());  // logs every run
```

## Learning Machine

```csharp
agent.EnableLearning(new LearningConfig
{
    UserMemoryMode = LearningMode.Always,
    UserProfileMode = LearningMode.Always,
    EnableOptimization = true
});

// Agent auto-learns user preferences across sessions
```

## Key Features

- **50+ LLM providers**: OpenAI, Anthropic, DeepSeek, Ollama, Gemini, Groq, Mistral, and 40+ more — unified interface
- **25 DB providers**: PostgreSQL, SQLite, SQL Server, Redis, MongoDB, DynamoDB, CosmosDB, and 20+ more — CQRS pattern
- **30 vector DBs**: Pinecone, Qdrant, Milvus, pgvector, Chroma, Weaviate, Vespa, and 25+ more
- **25 embedders**: OpenAI, Cohere, ONNX (local, zero API key), Ollama, and 20+ more
- **28 document readers**: PDF, DOCX, XLSX, Markdown, HTML, EPUB, YouTube transcripts, and 20+ more
- **161 prebuilt toolkits**: Slack, GitHub, Stripe, Jira, Salesforce, Google Calendar, and 155+ more
- **Realtime**: SignalR hubs with Redis backplane, reconnect, replay
- **Security**: ECDSA P-256, HMAC-SHA256, RBAC, audit trail, guardrails, HITL
- **CQRS-first**: Command/Query separation with Unit of Work, outbox pattern
- **Zero Trust Client**: Server-authoritative — client is display-only

## More Info

- [Full Documentation](https://github.com/your-org/AGXIOMA/tree/main/docs)
- [Getting Started Guide](https://github.com/your-org/AGXIOMA/blob/main/docs/GETTING_STARTED.md)
- [API Reference](https://github.com/your-org/AGXIOMA/blob/main/docs/API.md)
- [Provider Audit](https://github.com/your-org/AGXIOMA/blob/main/docs/PROVIDER_TOOLKIT_AUDIT.md)

## License

**Proprietary — All Rights Reserved.** This software may be used as a NuGet package dependency. Copying, modifying, redistributing, or creating derivative works of the source code is prohibited. See [LICENSE.md](LICENSE.md) for full terms.

© 2026 AGXIOME Team. All intellectual property rights reserved.
