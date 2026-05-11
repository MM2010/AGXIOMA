# AGXIOME

**Agentic Generative C# X-Linked Integrated Orchestration Memory Architecture**

Framework .NET 10 per orchestrazione agentic AI.  
Ispirato ad [Agno](https://docs.agno.com/), esteso con Zero Trust security, CQRS persistence,
realtime SignalR, resilience enterprise-grade, e sistema errori strutturato.

> **AGXIOME è il corpo. LLMWIKY è la mente. .NET 10 è il sistema nervoso.**

---

## Indice

- [Quick Start](#quick-start)
- [Architettura](#architettura)
- [Feature](#feature)
- [Provider LLM](#provider-llm)
- [Errori e Resilience](#errori-e-resilience)
- [Logging](#logging)
- [Documentazione](#documentazione)
- [Build & Test](#build--test)

---

## Quick Start

### Con engine di test (nessuna API key)

```csharp
using AGXIOME.Core.Agent;
using AGXIOME.Shared.Models;

var agent = AgentFactory.CreateTestAgent("demo");
var result = await agent.RunAsync(new AgentRequest("Ciao!"));
Console.WriteLine(result.Content); // "Test response"
```

### Con LLM reale (OpenAI)

```csharp
using AGXIOME.Core.Agent;
using AGXIOME.Shared.Config;
using AGXIOME.Shared.Models;

var agent = AgentFactory.Create("assistente", cfg =>
{
    cfg.ReasoningEngine = new LLMWIKYConfig
    {
        Provider = "OpenAI",
        Model = "gpt-4o-mini",
        ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    };
});

var result = await agent.RunAsync(new AgentRequest("Qual è la capitale d'Italia?"));
Console.WriteLine(result.Content); // "Roma"
```

### Con Ollama (locale)

```csharp
cfg.ReasoningEngine = new LLMWIKYConfig
{
    Provider = "Ollama",
    Model = "llama3.2",
    Endpoint = "http://localhost:11434"
};
```

---

## Architettura

```
┌──────────────────────────────────────────────────────────┐
│                    APPLICATION LAYER                      │
│  (Console, Web, Blazor, API Gateway, SignalR Client)     │
├──────────────────────────────────────────────────────────┤
│                    AGXIOME CORE                           │
│  Agent │ Teams │ Workflows │ Memory │ Context │ Learning  │
├──────────────────────────────────────────────────────────┤
│              TOOL & FUNCTION REGISTRY                     │
├──────────────────────────────────────────────────────────┤
│              LLMWIKY REASONING ENGINE                     │
│  (OpenAI, Anthropic, DeepSeek, Ollama, Local, Azure,     │
│   Gemini, Custom)                                        │
├──────────────────────────────────────────────────────────┤
│  X-LINK TRANSPORT  │  STORAGE (CQRS)  │  REALTIME (WS)   │
├──────────────────────────────────────────────────────────┤
│  SECURITY  │  RESILIENCE  │  LOGGING  │  TELEMETRY        │
│  (Crypto,  │  (Retry,     │  (Seq,    │  (OpenTelemetry,  │
│   HMAC,    │   Circuit    │   NLog,   │   Prometheus,     │
│   JWT,     │   Breaker,   │   Graylog,│   Metrics)        │
│   Audit)   │   Fallback)  │   Serilog)│                    │
└──────────────────────────────────────────────────────────┘
```

---

## Feature

| Area | Feature | Stato |
|------|---------|-------|
| **Agent** | Run, Stream, EventStream, ContinueRun, HITL resume | ✅ |
| **Tools** | Registry (global + per-agent), CallableFactory, MCP adapter, Auto JSON Schema | ✅ |
| **LLM** | 8 provider con fallback chain, cost tracking | ✅ |
| **Teams** | 4 modalità: Coordinate, Route, Broadcast, Tasks | ✅ |
| **Workflows** | DAG engine, Condition, Loop, Parallel, Router, CEL expressions | ✅ |
| **Storage** | PostgreSQL (query + pgvector), Redis cache, CQRS, UoW, Outbox | ✅ |
| **RAG** | KnowledgeBase, 3 chunker, ANN search, metadata filtering | ✅ |
| **Session** | Lifecycle, mutable state, chat history, summary | ✅ |
| **Context** | System prompt, instructions, few-shot, dependency injection | ✅ |
| **Guardrails** | PII detection, prompt injection, moderation — input/output | ✅ |
| **Hooks** | Pre/Post/Background/Tool — sincroni e async | ✅ |
| **HITL** | Confirmation, user input, external execution, approval audit | ✅ |
| **Learning** | User memory, profile, entity, session context, learned knowledge, decision log | ✅ |
| **Skills** | Registry, derivazione, composizione, download remoto | ✅ |
| **Scheduler** | Cron scheduling, run history, pause/resume | ✅ |
| **X-Link** | Checksum HMAC, compressione adattiva, MCP integration | ✅ |
| **Realtime** | 3 SignalR hub + Redis backplane + reconnect replay | ✅ |
| **Security** | ECDsa P-256, HMAC-SHA256, JWT middleware, audit logger | ✅ |
| **Logging** | 5 provider, per-modulo log level con wildcard | ✅ |
| **Resilience** | Retry (429/500/502/503), exponential backoff, circuit breaker, fallback | ✅ |
| **Errors** | 70+ codici strutturati, stack trace mai esposto all'utente | ✅ |
| **Telemetry** | OpenTelemetry metrics, OpenTelemetry Protocol export | ✅ |
| **Testing** | 164 test, 0 falliti — unità, edge, concorrenza, resilience | ✅ |

---

## Provider LLM

AGXIOME supporta **8 provider LLM** con un'interfaccia unificata:

| Provider | Streaming | Function Calling | Embedding | Configurazione |
|----------|-----------|-----------------|-----------|---------------|
| **OpenAI** (GPT-4o, o1, o3, GPT-4.1) | ✅ | ✅ | ✅ | `Provider: "OpenAI"` |
| **Anthropic** (Claude 4, Sonnet, Haiku) | ✅ | ✅ (tool_use) | ❌ (fallback) | `Provider: "Anthropic"` |
| **DeepSeek** (V3, R1, Coder) | ✅ | ✅ | ✅ | `Provider: "DeepSeek"` |
| **Ollama** (locale) | ✅ | ✅ | ✅ | `Provider: "Ollama"` |
| **Local** (vLLM, TGI, llama.cpp) | ✅ | ✅ | ✅ | `Provider: "Local"` |
| **Azure OpenAI** | ✅ | ✅ | ✅ | `Provider: "AzureOpenAI"` |
| **Gemini** (2.0 Flash, Pro) | ✅ | ✅ | ✅ | `Provider: "Gemini"` |
| **Custom** (endpoint OpenAI-compat) | ✅ | ✅ | ✅ | `Provider: "Custom"` |

### Fallback chain

```csharp
cfg.ReasoningEngine = new LLMWIKYConfig
{
    Provider = "DeepSeek",
    Model = "deepseek-chat",
    ApiKey = "sk-...",
    FallbackProviders = new()
    {
        new() { Provider = "OpenAI", Model = "gpt-4o-mini", ApiKey = "sk-..." },
        new() { Provider = "Anthropic", Model = "claude-haiku-3-5", ApiKey = "sk-ant-..." }
    }
};
```

---

## Errori e Resilience

### Errori strutturati

Tutti gli errori AGXIOME usano `AgxiomeException` con codici categorizzati.
Lo **stack trace non è mai esposto all'utente** — solo loggato internamente.

```csharp
try { await agent.RunAsync(request); }
catch (AgxiomeException ex)
{
    Console.WriteLine(ex.UserMessage);   // "Servizio AI non disponibile."
    Console.WriteLine(ex.ErrorCode);     // LLM_ProviderUnavailable
    Log.Error(ex.TechnicalMessage);      // include stack trace
}
```

### Categorie errore

| Range | Categoria | Esempi |
|-------|-----------|--------|
| 100 | Agent Engine | RunFailed, MaxIterations, Disposed |
| 200 | LLM Provider | RateLimited(429), ServerError(500/502/503), AuthFailed |
| 300 | Tools | NotFound, ExecutionFailed, HITLRejected |
| 400 | Database | ConnectionFailed, Deadlock, CircuitBreakerOpen |
| 600 | X-Link | ChecksumMismatch, PayloadCorrupted |
| 700 | RAG | IngestFailed, EmbeddingFailed |
| 800 | Security | AuthFailed, SignatureFailed, RateLimitExceeded |
| 900 | Guardrails | InputBlocked, PIIDetected |
| 1200 | Network | BadGateway(502), ServiceUnavailable(503) |

### Resilience

- **Retry** con exponential backoff + jitter su 429/500/502/503/timeout
- **Circuit breaker** — dopo N fallimenti consecutivi, salta operazioni
- **Fallback chain** — provider secondario se primario non risponde
- **Tutto loggato** — ogni tentativo, delay, successo/fallimento

---

## Logging

Supporto nativo per i principali sistemi di logging .NET:

```
Console ──── colori per livello (default)
Seq     ──── structured JSON su HTTP
NLog    ──── configurazione NLog nativa
Graylog ──── GELF su HTTP
Serilog ──── Serilog sink
```

### Per-modulo log level

```csharp
var config = new LoggingConfig
{
    DefaultMinimumLevel = LogLevel.Error,
    ModuleOverrides = new()
    {
        new() { Module = "AGXIOME.Core.Agent.*", MinimumLevel = LogLevel.Debug },
        new() { Module = "AGXIOME.LLMWIKY*", MinimumLevel = LogLevel.Warning }
    }
};
factory.Configure(config);
```

---

## Documentazione

| Guida | Descrizione |
|-------|-------------|
| [Getting Started](docs/GETTING_STARTED.md) | Crea il primo agente, connetti LLM |
| [Core Concepts](docs/CORE_CONCEPTS.md) | Agenti, Tools, Teams, Workflows |
| [LLM Providers](docs/PROVIDERS.md) | Provider, configurazione, costi |
| [Error Handling](docs/ERROR_HANDLING.md) | Codici errore, resilience, circuit breaker |
| [Logging](docs/LOGGING.md) | Provider logger, per-modulo, configurazione |
| [Storage & RAG](docs/STORAGE.md) | PostgreSQL, CQRS, RAG, outbox |
| [Realtime](docs/REALTIME.md) | SignalR hub, backplane, reconnect |
| [Security](docs/SECURITY.md) | Crypto, HMAC, JWT, audit |
| [Testing](docs/TESTING.md) | Strategia, copertura, 164 test |

---

## Build & Test

```bash
cd src
dotnet restore
dotnet build          # 0 errors
dotnet test           # 164 passed, 0 failed
```

## Struttura progetto

```
src/
├── AGXIOME.Shared/      → Records, enum, interfacce, error codes, resilience
├── AGXIOME.Core/        → Agent, tools, teams, workflows, session, hooks, guardrails, HITL, learning
├── AGXIOME.LLMWIKY/     → Engine, factory, 8 provider, cost tracker
├── AGXIOME.Storage/     → PostgreSQL, Redis, CQRS, UoW, outbox, RAG, chunker, vector store
├── AGXIOME.XLink/       → Trasporto checksum, compressione, MCP adapter
├── AGXIOME.Realtime/    → SignalR hub (Agent, Notification, Presence)
├── AGXIOME.Security/    → ECDsa, HMAC, audit logger
├── AGXIOME.Telemetry/   → OpenTelemetry metrics, health checks
├── AGXIOME.CLI/         → Entry point applicazione
└── AGXIOME.Tests/       → 164 test (13 file)
```

---

<p align="center">
  <strong>AGXIOME</strong> — Precision. Performance. Persistence.<br>
  Built for .NET 10. Driven by LLMWIKY. Powered by PostgreSQL.
</p>
