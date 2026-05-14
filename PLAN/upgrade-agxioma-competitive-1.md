---
goal: Close the competitive gap vs Swarms on 4 key dimensions and surpass it
version: 1.0
date_created: 2026-05-13
last_updated: 2026-05-13
owner: AGXIOMA Core Team
status: 'In Progress — Phase 1-3 e Phase 5 completate; Phase 4 (docs) pending'
tags: [architecture, multi-agent, providers, onboarding, ecosystem]
---

# Introduction

![Status: Planned](https://img.shields.io/badge/status-Planned-blue)

AGXIOMA ha un'infrastruttura enterprise superiore a Swarms (sicurezza, storage, HITL, .NET 10
performance), ma presenta 4 gap strategici rispetto al framework Python leader di mercato:

1. **Architetture multi-agent** — Swarms ha 40+ pattern pre-built; AGXIOMA ne ha 3 nativi
2. **LLM Provider coverage** — Swarms copre 100+ modelli via litellm; AGXIOMA copre ~4 provider
3. **Documentazione/Ecosystem** — Swarms ha docs complete, 11k+ GitHub stars; AGXIOMA non è pubblico
4. **Velocità onboarding** — Swarms = `pip install swarms` + 5 righe; AGXIOMA richiede scaffolding

Questo piano chiude questi gap e pone AGXIOMA al di sopra di Swarms, sfruttando i vantaggi
che Swarms non può replicare facilmente (.NET enterprise, sicurezza, storage depth, HITL, Realtime).

---

## 1. Requirements & Constraints

- **REQ-001**: Ogni nuova architettura deve essere pienamente compatibile con `IAgxiomeAgent`, `AgentConfig`, `IToolRegistry`
- **REQ-002**: I nuovi provider LLM devono implementare `ILLMProvider` e passare `LLMProviderCapabilities`
- **REQ-003**: Nessuna breaking change alle API pubbliche esistenti (backwards compatibility)
- **REQ-004**: Ogni architettura deve avere unit test in `AGXIOME.Tests`
- **REQ-005**: Il provider adapter deve gestire `litellm`-style routing senza dipendenza da Python
- **REQ-006**: La documentazione deve essere generata da XML docstrings esistenti + Docusaurus
- **SEC-001**: I nuovi provider non devono loggare API key; devono usare `SecretsScanner` esistente
- **SEC-002**: I bridge per framework esterni non devono esporre superfici di injection
- **CON-001**: Il progetto è .NET 10 — nessuna dipendenza Python diretta (use HTTP bridges dove necessario)
- **CON-002**: La priorità assoluta è mantenere il vantaggio enterprise su sicurezza e storage
- **GUD-001**: Pattern architetturali da implementare in `AGXIOME.Core` o progetto dedicato `AGXIOME.Patterns`
- **GUD-002**: Provider in `AGXIOME.LLMWIKY/Providers/` seguendo la struttura `OpenAIProvider.cs`
- **PAT-001**: Architetture multi-agent seguono il pattern `AgxiomeTeam` (leader + members + topology)
- **PAT-002**: Ogni architettura espone `RunAsync(task)`, `RunBatchedAsync(tasks)`, `RunConcurrentlyAsync(tasks)`

---

## 2. Implementation Steps

### Implementation Phase 1 — Core Multi-Agent Architectures (Priorità Alta)

- GOAL-001: Implementare le 5 architetture Swarms più usate in AGXIOMA, superandole per configurabilità

**Analisi gap vs Swarms:**

| Architettura Swarms | Parametri chiave | AGXIOMA oggi | Gap |
|---|---|---|---|
| `MixtureOfAgents` | agents, layers, final_agent, rules | Assente | 🔴 Critico |
| `GroupChat` | speaker_function, @mentions, priority weights | Assente | 🔴 Critico |
| `GraphWorkflow` | DAG, NodeType, Edge, parallel branches | `AgxiomeWorkflow` (parziale, no DAG esplicito) | 🟡 Parziale |
| `HierarchicalSwarm` | director→workers, feedback loop | `TeamMode.Coordinate` (parziale) | 🟡 Parziale |
| `SwarmRouter` / `ModelRouter` | task complexity routing, multi-provider | `ModelCompatibilityMap` (no routing LLM) | 🟡 Parziale |

**Come AGXIOMA può superare Swarms:**
- `MixtureOfAgents` → aggiungere `LayerStrategy` (parallel, sequential, hybrid) + `AggregationMode` (vote, merge, critic)
- `GroupChat` → aggiungere `@mention` parsing + `SpeakerPolicy` enum + WebSocket realtime via `AGXIOME.Realtime`
- `GraphWorkflow` → rafforzare l'esistente con `NodeType` enum, parallel branch detection, callback per nodo
- `ModelRouter` → routing intelligente su costo/latenza/capability, fallback chain automatica

| Task     | Description | Completed | Date |
|---|---|---|---|
| TASK-001 | Creare `AGXIOME.Core/Patterns/MixtureOfAgentsSwarm.cs` — N agenti paralleli per layer, final_agent aggregatore, `LayerStrategy` enum (Parallel/Sequential/Hybrid), `AggregationMode` (Vote/Merge/CriticSynth). Esporre `RunAsync`, `RunBatchedAsync`, `RunConcurrentlyAsync`. | ✅ | 2026-05-13 |
| TASK-002 | Creare `AGXIOME.Core/Patterns/GroupChatSwarm.cs` — `SpeakerPolicy` enum (RoundRobin/Random/Priority/MentionDriven/Custom), `@mention` regex parser, `ChatTurn` record, `InteractiveMode` flag per REPL. Integrare con `AGXIOME.Realtime` SignalR hub per live streaming turni. | ✅ | 2026-05-13 |
| TASK-003 | Refactor `AgxiomeWorkflow.cs` → `AgxiomeDagWorkflow.cs` — aggiungere `NodeType` enum, `AddParallelBranch()`, `AddConditionalEdge()`, callback `OnNodeComplete`, topological sort con parallel branch detection automatica. Mantenere backward compat. | ✅ | 2026-05-13 |
| TASK-004 | Creare `AGXIOME.Core/Patterns/HierarchicalSwarm.cs` — Director LLM che genera piano strutturato, spawns workers per task, feedback loop (worker risponde al director che può correggere), N livelli gerarchici configurabili. | ✅ | 2026-05-13 |
| TASK-005 | Creare `AGXIOME.Core/Routing/AgxiomeModelRouter.cs` — routing basato su `RoutingStrategy` (Cost/Latency/Capability/Hybrid), `ProviderMetrics` per track latency/cost per provider, fallback chain automatica, circuit breaker integrato da `AGXIOME.LLMWIKY.Resilience`. | ✅ | 2026-05-13 |
| TASK-006 | Creare `AGXIOME.Core/Patterns/MajorityVotingSwarm.cs` — N agenti rispondono allo stesso task, `VotingMode` (SimpleCount/WeightedScore/LLMJudge), confidence threshold configurabile. | ✅ | 2026-05-13 |
| TASK-007 | Creare `AGXIOME.Core/Patterns/PlannerWorkerSwarm.cs` — Planner LLM genera task list strutturata (JSON), workers eseguono in parallelo, Evaluator LLM valida output e rilancia task falliti. Ispirato a Swarms `planner_worker_swarm` ma con HITL integrato. | ✅ | 2026-05-13 |
| TASK-008 | Aggiungere `SwarmPatternFactory` statico in `AGXIOME.Core/Patterns/` per creare pattern con 1 chiamata: `SwarmFactory.MixtureOfAgents(agents, layers)`, `SwarmFactory.GroupChat(agents)`, ecc. — abilita onboarding in 3 righe. | ✅ | 2026-05-13 |

---

### Implementation Phase 2 — LLM Provider Coverage (Priorità Alta)

- GOAL-002: Portare AGXIOMA da 4 provider a 20+ con routing intelligente, superando Swarms su qualità del routing

**Analisi gap vs Swarms:**

Swarms usa `litellm` come adapter universale — supporta 100+ modelli ma il routing è basico (selezione manuale).
AGXIOMA può superarlo costruendo routing intelligente basato su `ProviderMetrics` real-time.

| Provider | Swarms | AGXIOMA oggi | Gap |
|---|---|---|---|
| OpenAI (gpt-4o, gpt-4.1, o1, o3) | ✅ via litellm | ✅ `OpenAIProvider` | Allineato |
| Anthropic Claude (3.5, 3.7, 4) | ✅ | ✅ bridge parziale | 🟡 |
| Google Gemini (1.5, 2.0, 2.5) | ✅ | ❌ | 🔴 |
| Meta Llama 3.x (local/API) | ✅ | ❌ | 🔴 |
| Mistral (large, medium, small) | ✅ | ❌ | 🔴 |
| DeepSeek (chat, reasoner, v3) | ✅ | ✅ via OpenAI compat | ✅ |
| Groq (llama, mixtral, gemma) | ✅ | ❌ | 🔴 |
| Cohere (command-r, command-r+) | ✅ | ❌ | 🔴 |
| AWS Bedrock (multi-model) | ✅ | `BedrockAndAzureEmbedders` (solo embed) | 🟡 |
| Azure OpenAI | ✅ | `BedrockAndAzureEmbedders` (solo embed) | 🟡 |
| Ollama (local) | ✅ | `OllamaEmbedders` (solo embed) | 🟡 |
| Perplexity / Grok | ✅ | ❌ | 🔴 |

| Task     | Description | Completed | Date |
|---|---|---|---|
| TASK-009 | Creare `AGXIOME.LLMWIKY/Providers/AnthropicProvider.cs` — Messages API v1, streaming, tool_use nativo (Anthropic ha proprio formato function calling diverso da OpenAI), vision support. | ✅ | 2026-05-13 |
| TASK-010 | Creare `AGXIOME.LLMWIKY/Providers/GeminiProvider.cs` — Google AI Studio API + Vertex AI endpoint, `generateContent` con `functionDeclarations`, streaming via SSE, multimodal (testo+immagini). | ✅ | 2026-05-13 |
| TASK-011 | Creare `AGXIOME.LLMWIKY/Providers/OllamaProvider.cs` — HTTP API locale (`/api/chat`), streaming, tool calling (Ollama 0.3+), modelli dinamici da `/api/tags`. Zero API key — ideale per dev locale. | ✅ | 2026-05-13 |
| TASK-012 | Creare `AGXIOME.LLMWIKY/Providers/GroqProvider.cs` — OpenAI-compatible endpoint (`api.groq.com/openai/v1`), specializzato per inferenza ultra-rapida, rate limit handling specifico Groq. | ✅ | 2026-05-13 |
| TASK-013 | Creare `AGXIOME.LLMWIKY/Providers/MistralProvider.cs` — `api.mistral.ai/v1`, OpenAI-compat + endpoint nativo per function calling Mistral format. | ✅ | 2026-05-13 |
| TASK-014 | Estendere `BedrockAndAzureEmbedders.cs` → creare `BedrockProvider.cs` e `AzureOpenAIProvider.cs` in LLMWIKY per completion (non solo embedding). | ✅ | 2026-05-13 |
| TASK-015 | Creare `AGXIOME.LLMWIKY/Providers/OpenAICompatibleProvider.cs` — provider generico per qualsiasi API OpenAI-compatible (DeepSeek, Together AI, Fireworks, Perplexity, ecc.) configurabile via `Endpoint` + `ApiKey` + `Model`. Swarms non ha questo livello di genericità. | ✅ | 2026-05-13 |
| TASK-016 | Implementare `ProviderMetricsCollector` in `AGXIOME.LLMWIKY` — traccia latency p50/p95/p99, token cost per provider, error rate, circuit breaker status. Alimenta `AgxiomeModelRouter` (TASK-005) per routing intelligente. Differenziatore vs Swarms: routing data-driven invece che manuale. | ✅ | 2026-05-13 |
| TASK-017 | Aggiungere `LLMWIKYConfig.Auto` builder — `LLMWIKYConfig.FromEnv()` che legge automaticamente le env vars presenti (`OPENAI_API_KEY`, `ANTHROPIC_API_KEY`, `GEMINI_API_KEY`, ...) e configura i provider disponibili. Onboarding da 0 a multi-provider in 1 riga. | ✅ | 2026-05-13 |

---

### Implementation Phase 3 — Developer Experience & Onboarding (Priorità Media-Alta)

- GOAL-003: Ridurre il time-to-first-agent da ~30 min a < 5 min, superare Swarms su configurabilità zero-config

**Analisi gap vs Swarms:**

Swarms: `pip install swarms` → `from swarms import Agent` → 3 righe di codice → agente funzionante.
AGXIOMA: richiede `AgentFactory.Configure(loggerFactory)` + `LLMWIKYConfig` + `AgentConfig` + `AgentFactory.CreateWithEngine()`.

**Come superare Swarms:**
- AGXIOMA può offrire un'esperienza superiore grazie al type safety di C#, IntelliSense, e integrazione VS Code
- `dotnet new agxiome-agent` → progetto funzionante in 10 secondi
- Fluent builder API più espressiva di Python kwargs

| Task     | Description | Completed | Date |
|---|---|---|---|
| TASK-018 | Creare `AgxiomeBuilder` fluent API in `AGXIOME.Core` — `AgxiomeBuilder.Agent("name").WithModel("deepseek-chat").WithApiKey(apiKey).WithTool<HttpFetchTool>().WithMaxIterations(8).Build()` — zero boilerplate, IntelliSense-friendly. Ispirato a Swarms ma con type safety C#. | ✅ | 2026-05-13 |
| TASK-019 | Creare .NET template `AGXIOME.Templates` — `dotnet new install AGXIOME.Templates` → `dotnet new agxiome-agent`, `dotnet new agxiome-orchestrator`, `dotnet new agxiome-team`. Ogni template produce un progetto funzionante in < 30 secondi. | ⚡ | 2026-05-13 |
| TASK-020 | Creare `AGXIOME.Extensions.DependencyInjection` — `services.AddAgxiome(o => o.UseDeepSeek(apiKey).UseRedis(connStr))` per integrazione ASP.NET Core. Swarms non ha DI nativo. | ✅ | 2026-05-13 |
| TASK-021 | Aggiornare `AGXIOME.CLI` con comandi strutturati: `agxiome new agent`, `agxiome run <file>`, `agxiome benchmark`, `agxiome doctor` (verifica config, API keys, Redis). Prendere ispirazione da `dotnet ef` UX. | ⚡ | 2026-05-13 |
| TASK-022 | Creare `AgxiomeQuickStart` — static helper che combina TASK-017 e TASK-018: `var agent = AgxiomeQuickStart.CreateAgent("deepseek-chat")` legge API key da env, crea agent funzionante in 1 riga. Demo parity con Swarms `Agent(model_name="gpt-4")`. | ✅ | 2026-05-13 |

---

### Implementation Phase 4 — Documentazione & Ecosystem (Priorità Media)

- GOAL-004: Avere documentazione tecnica completa, navigabile, con esempi eseguibili superiori a Swarms

**Analisi gap vs Swarms:**

Swarms ha docs.swarms.world con 40+ pagine strutturate, esempi copy-paste per ogni architettura.
AGXIOMA non ha documentazione pubblica. Swarms ha community Discord da 3000+ dev.

**Come superare Swarms:**
- Documentazione auto-generata da XML docstrings già presenti in AGXIOMA (363 file .cs)
- Notebook interattivi C# (dotnet-interactive/Polyglot) → esempi eseguibili in browser
- Benchmark pubblici vs Swarms/LangChain/AutoGen su casi reali

| Task     | Description | Completed | Date |
|---|---|---|---|
| TASK-023 | Setup Docusaurus (`docs/`) con struttura: Getting Started / Core Concepts / Architectures / Providers / Storage / Security / Examples / API Reference. Configurare `docfx` per auto-generazione API reference da XML docstrings. | | |
| TASK-024 | Scrivere 5 quickstart guides: (1) First Agent, (2) Multi-Agent Orchestrator, (3) Agent with Tools, (4) RAG with Vector Store, (5) Enterprise Setup with Redis+Security. Ogni guide < 50 righe di codice funzionante. | | |
| TASK-025 | Creare `AGXIOME.Notebooks/` — 10 Polyglot Notebooks (.ipynb C# kernel): Patterns Overview, MixtureOfAgents Demo, GroupChat Demo, RAG Pipeline, Security Setup, Benchmark vs Swarms. Eseguibili su GitHub Codespaces. | | |
| TASK-026 | Pubblicare benchmarks pubblici in `docs/benchmarks/` — latency per provider, throughput multi-agent, memory footprint vs Swarms (Python). Usare il vantaggio .NET 10 performance per differenziazione quantitativa. | | |
| TASK-027 | Configurare GitHub Actions CI/CD — build matrix (.NET 8, 9, 10), test coverage report, auto-publish NuGet su tag, deploy Docusaurus su GitHub Pages. | | |
| TASK-028 | Creare `AGXIOME.Samples/` repository separato con 20+ esempi production-ready: news aggregator, code reviewer, financial analyst, customer support bot, document RAG. Ogni esempio con README + Docker compose. | | |

---

### Implementation Phase 5 — Architetture Differenzianti (AGXIOMA vs Swarms beyond parity)

- GOAL-005: Aggiungere pattern che Swarms non ha, sfruttando i vantaggi esclusivi .NET/enterprise di AGXIOMA

| Task     | Description | Completed | Date |
|---|---|---|---|
| TASK-029 | `RealtimeGroupChat` — GroupChat con SignalR WebSocket: ogni turno agente streamma token in real-time al client. Swarms non ha WebSocket nativo. Integra `AGXIOME.Realtime` esistente. | ✅ | 2026-05-13 |
| TASK-030 | `TransactionalSwarm` — Swarm con Outbox Pattern: ogni tool call e ogni agent result viene salvato atomicamente in DB prima di procedere. Replay esatto in caso di crash. Usa `Outbox.cs` e `UnitOfWork.cs` esistenti. Swarms perde tutti i dati in memoria se crashe. | ✅ | 2026-05-13 |
| TASK-031 | `HITLSwarm` — Swarm con approvazione umana obbligatoria per tool call critiche (es. `file_delete`, `send_email`, `db_write`). Usa `HITLManager` esistente. Pause/resume con stato persistito in Redis. Nessun equivalente in Swarms. | ✅ | 2026-05-13 |
| TASK-032 | `ComplianceSwarm` — Swarm con guardrail PII automatici, audit trail immutabile per ogni decisione agente, RBAC per chi può lanciare quale swarm. Usa `PIIDetectionGuardrail`, `AuditTrail`, `Rbac` esistenti. Target: settori regolamentati (finance, healthcare, legal). | ✅ | 2026-05-13 |
| TASK-033 | `DistributedSwarm` — Swarm che distribuisce agenti su più nodi via message queue (Redis Streams / Azure Service Bus). Ogni agente gira in processo separato, risultati aggregati via `TaskStore`. Scala orizzontalmente. Swarms è single-process. | ✅ | 2026-05-13 |

---

## 3. Alternatives

- **ALT-001**: Wrappare `litellm` Python via gRPC bridge → scartato per CON-001 (no Python), overhead di processo, complessità deploy
- **ALT-002**: Fork di Swarms in C# → scartato perché AGXIOMA ha già architettura superiore; il fork porterebbe debt tecnico di Swarms
- **ALT-003**: Implementare solo routing OpenAI-compatible → scartato perché Anthropic e Gemini hanno format function calling diversi e perderemmo qualità tool use
- **ALT-004**: Generare docs con AI da codice → valutato come supplemento, non sostituto; docs AI-generated senza review umana sono spesso inaccurate

---

## 4. Dependencies

- **DEP-001**: `AGXIOME.Core` (AgxiomeAgent, AgentFactory, AgxiomeTeam) — base per tutte le architetture
- **DEP-002**: `AGXIOME.LLMWIKY` (ILLMProvider, LLMWIKYConfig, ResilientExecutor) — base per tutti i provider
- **DEP-003**: `AGXIOME.Realtime` (SignalR hubs) — per RealtimeGroupChat (TASK-029)
- **DEP-004**: NuGet: `Microsoft.AspNetCore.SignalR.Client` (già referenziato), `System.Text.Json` (già incluso)
- **DEP-005**: Per Gemini: `Google.Cloud.AIPlatform.V1` NuGet oppure REST endpoint diretto (preferito per evitare dipendenze pesanti)
- **DEP-006**: Per Ollama: solo `HttpClient` — nessuna dipendenza aggiuntiva
- **DEP-007**: Docusaurus (Node.js, solo per docs build — non runtime)
- **DEP-008**: `Microsoft.DotNet.Interactive` — per Polyglot Notebooks (TASK-025)

---

## 5. Files

- **FILE-001**: `AGXIOME.Core/Patterns/MixtureOfAgentsSwarm.cs` — TASK-001
- **FILE-002**: `AGXIOME.Core/Patterns/GroupChatSwarm.cs` — TASK-002
- **FILE-003**: `AGXIOME.Core/Workflows/AgxiomeDagWorkflow.cs` — TASK-003
- **FILE-004**: `AGXIOME.Core/Patterns/HierarchicalSwarm.cs` — TASK-004
- **FILE-005**: `AGXIOME.Core/Routing/AgxiomeModelRouter.cs` — TASK-005
- **FILE-006**: `AGXIOME.Core/Patterns/MajorityVotingSwarm.cs` — TASK-006
- **FILE-007**: `AGXIOME.Core/Patterns/PlannerWorkerSwarm.cs` — TASK-007
- **FILE-008**: `AGXIOME.Core/Patterns/SwarmPatternFactory.cs` — TASK-008
- **FILE-009**: `AGXIOME.LLMWIKY/Providers/AnthropicProvider.cs` — TASK-009
- **FILE-010**: `AGXIOME.LLMWIKY/Providers/GeminiProvider.cs` — TASK-010
- **FILE-011**: `AGXIOME.LLMWIKY/Providers/OllamaProvider.cs` — TASK-011
- **FILE-012**: `AGXIOME.LLMWIKY/Providers/GroqProvider.cs` — TASK-012
- **FILE-013**: `AGXIOME.LLMWIKY/Providers/MistralProvider.cs` — TASK-013
- **FILE-014**: `AGXIOME.LLMWIKY/Providers/BedrockProvider.cs` — TASK-014
- **FILE-015**: `AGXIOME.LLMWIKY/Providers/AzureOpenAIProvider.cs` — TASK-014
- **FILE-016**: `AGXIOME.LLMWIKY/Providers/OpenAICompatibleProvider.cs` — TASK-015
- **FILE-017**: `AGXIOME.LLMWIKY/Metrics/ProviderMetricsCollector.cs` — TASK-016
- **FILE-018**: `AGXIOME.Core/AgxiomeBuilder.cs` — TASK-018
- **FILE-019**: `AGXIOME.Templates/` — TASK-019 (dotnet new templates)
- **FILE-020**: `AGXIOME.Extensions.DependencyInjection/ServiceCollectionExtensions.cs` — TASK-020
- **FILE-021**: `AGXIOME.Core/Patterns/RealtimeGroupChatSwarm.cs` — TASK-029
- **FILE-022**: `AGXIOME.Core/Patterns/TransactionalSwarm.cs` — TASK-030
- **FILE-023**: `AGXIOME.Core/Patterns/HITLSwarm.cs` — TASK-031
- **FILE-024**: `AGXIOME.Core/Patterns/ComplianceSwarm.cs` — TASK-032
- **FILE-025**: `AGXIOME.Core/Patterns/DistributedSwarm.cs` — TASK-033
- **FILE-026**: `docs/` — Docusaurus site — TASK-023/024/025/026

---

## 6. Testing

- **TEST-001**: `AGXIOME.Tests/Patterns/MixtureOfAgentsTests.cs` — test `Run`, `RunBatched`, `RunConcurrently`, layer count, final_agent aggregation, AggregationMode.Vote vs Merge
- **TEST-002**: `AGXIOME.Tests/Patterns/GroupChatTests.cs` — test RoundRobin, Priority, MentionDriven speaker, @mention parsing, max_loops
- **TEST-003**: `AGXIOME.Tests/Workflows/DagWorkflowTests.cs` — test topological sort, parallel branch execution, conditional edge, cycle detection
- **TEST-004**: `AGXIOME.Tests/Routing/ModelRouterTests.cs` — test routing strategy (Cost/Latency/Capability), fallback chain, circuit breaker trigger
- **TEST-005**: `AGXIOME.Tests/Providers/AnthropicProviderTests.cs` — mock HTTP, tool_use parsing, streaming, error handling
- **TEST-006**: `AGXIOME.Tests/Providers/GeminiProviderTests.cs` — mock HTTP, functionDeclarations format, multimodal
- **TEST-007**: `AGXIOME.Tests/Providers/OllamaProviderTests.cs` — local endpoint mock, model list, streaming
- **TEST-008**: `AGXIOME.Tests/Builder/AgxiomeBuilderTests.cs` — fluent chain, auto-config from env, DI integration
- **TEST-009**: Integration test: `MixtureOfAgents` con provider reale (DeepSeek) su task noto — verifica qualità output > single agent
- **TEST-010**: Performance benchmark: `AGXIOME.Tests/Benchmarks/` — BenchmarkDotNet, confronta AGXIOMA vs baseline Python equivalente stimato

---

## 7. Risks & Assumptions

- **RISK-001**: Anthropic e Gemini cambiano frequentemente il formato API — mitigazione: pin versione API in config, test di regressione automatici via CI
- **RISK-002**: `MixtureOfAgents` con layers=3 e 5 agenti per layer → 15 chiamate LLM per run → costo/latenza elevata — mitigazione: lazy evaluation, caching `ToolResultCache`, circuit breaker per layer
- **RISK-003**: `GroupChat` con `MentionDriven` speaker in loop infinito — mitigazione: `MaxLoops` obbligatorio, `LoopDetectionGuardrail` che rileva risposte identiche
- **RISK-004**: Provider coverage creep — gestire 10+ provider aumenta surface area di manutenzione — mitigazione: `OpenAICompatibleProvider` generico copre 80% dei nuovi provider senza codice custom
- **RISK-005**: Docusaurus richiede Node.js in pipeline CI — mitigazione: Docker image separata per docs build, non blocca pipeline .NET
- **ASSUMPTION-001**: Il vantaggio di AGXIOMA su sicurezza e storage è sufficiente per giustificare C# vs Python per il target enterprise
- **ASSUMPTION-002**: La community .NET enterprise è il mercato primario — non il mercato Python/data science dove Swarms è radicato
- **ASSUMPTION-003**: I provider LLM mantengono backward compatibility API per almeno 12 mesi

---

## 8. Scorecard Post-Implementation (Target)

| Categoria | AGXIOMA oggi | Swarms oggi | AGXIOMA post-plan | Delta |
|---|---|---|---|---|
| Architetture multi-agent | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | +2 |
| LLM Provider coverage | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | +2 |
| Developer Experience | ⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | +2 |
| Documentazione | ⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | +2 |
| Sicurezza enterprise | ⭐⭐⭐⭐⭐ | ⭐ | ⭐⭐⭐⭐⭐ | = (mantenuto) |
| Storage & Persistence | ⭐⭐⭐⭐⭐ | ⭐⭐ | ⭐⭐⭐⭐⭐ | = (mantenuto) |
| HITL & Compliance | ⭐⭐⭐⭐ | ⭐ | ⭐⭐⭐⭐⭐ | +1 (TASK-031/032) |
| Realtime | ⭐⭐⭐⭐ | ⭐ | ⭐⭐⭐⭐⭐ | +1 (TASK-029) |
| Performance | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | = (mantenuto) |
| **TOTALE** | **33/50** | **35/50** | **44/50** | **+11** |

---

## 9. Related Specifications / Further Reading

- [Swarms MixtureOfAgents paper](https://arxiv.org/pdf/2406.04692) — reference scientifico per TASK-001
- [Swarms GroupChat docs](https://github.com/kyegomez/swarms/blob/master/docs/swarms/structs/group_chat.md)
- [Swarms ModelRouter docs](https://github.com/kyegomez/swarms/blob/master/docs/swarms/structs/model_router.md)
- [Swarms features.md](https://github.com/kyegomez/swarms/blob/master/docs/swarms/features.md)
- [OpenAI Function Calling](https://platform.openai.com/docs/guides/function-calling) — base per tutti i provider
- [Anthropic Tool Use](https://docs.anthropic.com/en/docs/build-with-claude/tool-use) — format diverso da OpenAI, vedi TASK-009
- [Google Gemini Function Calling](https://ai.google.dev/gemini-api/docs/function-calling) — vedi TASK-010
- [Docusaurus](https://docusaurus.io/) — generatore docs — vedi TASK-023
- [dotnet-interactive Polyglot Notebooks](https://github.com/dotnet/interactive) — vedi TASK-025
- [BenchmarkDotNet](https://benchmarkdotnet.org/) — vedi TEST-010
