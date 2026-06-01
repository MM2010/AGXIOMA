# AGXIOME — Agentic Orchestration Framework for .NET 10

**Build AI agents, teams, and workflows with 350+ ready-to-use providers.**

---

## Installation

```bash
dotnet add package AGXIOME.Core --prerelease
```

This pulls in `AGXIOME.Shared`. Add specific packages for extra features:

```bash
dotnet add package AGXIOME.LLMWIKY --prerelease      # 50+ LLM providers
dotnet add package AGXIOME.Storage --prerelease       # CQRS + 30 vector DBs
dotnet add package AGXIOME.Orchestration --prerelease # Multi-agent orchestrator
dotnet add package AGXIOME.Toolkits --prerelease      # 161 prebuilt integrations
```

---

## Quick Start

```csharp
using AGXIOME.Core;
using AGXIOME.Shared.Models;

// DeepSeek (cheapest, excellent quality)
var agent = AgxiomeBuilder.Agent("assistant")
    .UseDeepSeek("sk-your-key", "deepseek-chat")
    .WithSystemPrompt("You are a helpful assistant.")
    .Build();

var response = await agent.RunAsync(new AgentRequest("Capital of France?"));
Console.WriteLine(response.Content); // Paris
```

### Ollama (local, free, no API key)

```bash
ollama pull llama3.2
```

```csharp
var agent = AgxiomeBuilder.Agent("assistant").UseOllama("llama3.2").Build();
```

### OpenAI

```csharp
var agent = AgxiomeBuilder.Agent("assistant").UseOpenAI("sk-...", "gpt-4o-mini").Build();
```

### Anthropic Claude

```csharp
var agent = AgxiomeBuilder.Agent("assistant").UseAnthropic("sk-ant-...", "claude-sonnet-4-20250514").Build();
```

---

## Streaming

```csharp
await foreach (var chunk in agent.RunStreamAsync(new AgentRequest("Explain quantum computing")))
    Console.Write(chunk.Content);
```

---

## Team — ForestSwarm (Domain-Based Routing)

```csharp
using AGXIOME.Core.Patterns;

builder.Services.AddAgxiome()
    .UseProvider("deepseek", "deepseek-chat")
    .AddForestSwarm("support", forest =>
    {
        forest.AddTree("billing")
            .AddAgent("billing-specialist", "You handle payments.")
            .WithKeywords("invoice", "payment", "refund");

        forest.AddTree("technical")
            .AddAgent("tech-specialist", "You handle API errors.")
            .WithKeywords("error", "api", "crash", "timeout");
    });
```

---

## Key Features

- **50+ LLM providers**: OpenAI, Anthropic, DeepSeek, Ollama, Gemini, Groq, Mistral
- **25 DB providers**: PostgreSQL, SQLite, SQL Server, Redis, MongoDB, DynamoDB, CosmosDB
- **30 vector DBs**: Pinecone, Qdrant, Milvus, pgvector, Chroma, Weaviate, Vespa
- **25 embedders**: OpenAI, Cohere, ONNX (local, zero API key), Ollama
- **28 document readers**: PDF, DOCX, XLSX, Markdown, HTML, EPUB
- **161 prebuilt toolkits**: Slack, GitHub, Stripe, Jira, Salesforce, Google Calendar
- **Realtime**: SignalR hubs with Redis backplane + reconnect
- **Security**: ECDSA, HMAC, RBAC, audit trail, guardrails, HITL
- **CQRS-first**: Command/Query separation with Unit of Work
- **Zero Trust Client**: Server-authoritative, client is display-only

---

## Links

- Website: https://agxiome.com
- Documentation: https://github.com/AGXIOME/AGXIOMA/tree/main/docs
- Cookbook: https://github.com/AGXIOME/AGXIOMA/tree/main/cookbook
- Demo Apps: https://github.com/AGXIOME/AGXIOMA/tree/main/apps

---

Version 0.0.2-beta — Proprietary license.
