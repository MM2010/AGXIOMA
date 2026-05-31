# AGXIOME v0.0.1-beta

**Agentic Generative C# X-Linked Integrated Orchestration Memory Architecture**

[![NuGet](https://img.shields.io/badge/nuget-v0.0.1--beta-blue)](https://www.nuget.org/packages/AGXIOME.Core)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)
[![Tests](https://img.shields.io/badge/tests-3190%20passed-brightgreen)]()

Framework .NET 10 per orchestrazione agentic AI.  
Ispirato ad [Agno](https://docs.agno.com/), esteso con Zero Trust security, CQRS persistence,
realtime SignalR, resilience enterprise-grade, e sistema errori strutturato.

> **AGXIOME è il corpo. LLMWIKY è la mente. .NET 10 è il sistema nervoso.**

## 📦 Installazione

```bash
# Richiede flag --prerelease per pacchetti beta
dotnet add package AGXIOME.Core --prerelease
dotnet add package AGXIOME.Shared --prerelease
```

## ⚡ Quick Start

```csharp
using AGXIOME.Core.Agent;
using AGXIOME.Shared.Models;

var agent = AgentFactory.CreateTestAgent("demo");
var result = await agent.RunAsync(new AgentRequest("Ciao!"));
Console.WriteLine(result.Content);
```

[→ Guida completa](docs/GETTING_STARTED.md)

## 🏗 Architettura

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
| **Storage** | PostgreSQL, MySQL, MariaDB, MongoDB, Redis — CQRS, UoW, Outbox, RAG | ✅ |
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
| **Testing** | 3.190 test, 0 falliti — unità, edge, concorrenza, resilience, real-world E2E | ✅ |

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
| [Storage & RAG](docs/STORAGE.md) | PostgreSQL, MySQL, MariaDB, MongoDB, Redis — CQRS, RAG |
| [Realtime](docs/REALTIME.md) | SignalR hub, backplane, reconnect |
| [Interfaces](docs/INTERFACES.md) | Slack, Telegram, WhatsApp, Discord, AG-UI, A2A, MCP |
| [Security](docs/SECURITY.md) | Crypto, HMAC, JWT, audit |
| [Testing](docs/TESTING.md) | Strategia, copertura, 164 test |

---

## 📚 Documentazione

| Documento | Descrizione |
|-----------|-------------|
| [Getting Started](docs/GETTING_STARTED.md) | Guida rapida: primo agente in 5 minuti |
| [API Reference](docs/API.md) | API completa con link ai sorgenti |
| [Core Concepts](docs/CORE_CONCEPTS.md) | Concetti fondamentali: Agent, Team, Workflow |
| [Architecture](docs/ARCHITECTURE.md) | Design patterns e decisioni architetturali |
| [Providers](docs/PROVIDERS.md) | Tutti i provider: LLM, DB, Vector, Embedder |
| [Storage](docs/STORAGE.md) | CQRS, Unit of Work, multi-model persistence |
| [Security](docs/SECURITY.md) | Zero Trust, Crypto, Audit, RBAC |
| [Realtime](docs/REALTIME.md) | SignalR hubs, backplane, reconnect |
| [Error Handling](docs/ERROR_HANDLING.md) | Codici errore strutturati, resilience |
| [Logging](docs/LOGGING.md) | Provider logging, livelli, pseudonimizzazione |
| [Testing](docs/TESTING.md) | Strategia test, coverage, CI/CD |
| [Roadmap](docs/ROADMAP.md) | Prossime feature e milestone |

### Generazione documentazione API da XML doc

```bash
# Il file XML viene generato automaticamente ad ogni build Release
# si trova in: src/AGXIOME.Shared/bin/Release/net10.0/AGXIOME.Shared.xml
# Per generare documentazione HTML installare docfx:
dotnet tool install -g docfx
docfx init
docfx build
```

## 📊 Numeri del progetto

| Metrica | Valore |
|---------|--------|
| Progetti .NET | 17 |
| Test totali | 3.276 (3.190 passati, 82 skipped, 0 falliti) |
| Provider LLM | 50+ |
| Provider DB | 25 |
| Vector DB | 30 |
| Embedder | 25 |
| Document Reader | 28 |
| Chunker | 12 |
| Reranker | 8 |
| Toolkit prebuilt | 161 |
| Interfacce pubbliche | 30+ |
| XML documentation | 100% tipi pubblici |
| Versione | 0.0.1-beta |
| Target | .NET 10 |

## 🛠 Build & Test

```bash
# Build
dotnet build

# Test (richiede Docker per integration test)
dotnet test AGXIOME.Tests

# Pack NuGet
dotnet pack AGXIOME.Core -c Release -o ./artifacts
```

Per i rerun che usano `Microsoft.AspNetCore.Mvc.Testing`, in alcuni ambienti Windows il file
`MvcTestingAppManifest.json` sotto `src/AGXIOME.Tests/obj` puo` restare lockato da un processo
esterno. In quel caso usa il runner isolato, che usa `--artifacts-path` in una directory temporanea
senza riusare il path `obj` condiviso del progetto.

```powershell
pwsh .\scripts\test-agxiome-tests-isolated.ps1
pwsh .\scripts\test-agxiome-tests-isolated.ps1 -Filter FullyQualifiedName~F16_TS01_RealWorldScenarioTests
```

## Struttura progetto

```
src/
├── AGXIOME.Shared/      → Records, enum, interfacce, error codes, resilience
├── AGXIOME.Core/        → Agent, tools, teams, workflows, session, hooks, guardrails, HITL, learning
├── AGXIOME.LLMWIKY/     → Engine, factory, 8 provider, cost tracker
├── AGXIOME.Storage/     → PostgreSQL, MySQL, MariaDB, MongoDB, Redis, CQRS, RAG
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
