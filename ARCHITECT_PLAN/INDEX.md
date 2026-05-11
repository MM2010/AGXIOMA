# AGXIOME — Architect Plan Index

## Scopo

Framework .NET 10 per orchestrazione agentic AI. Ispirato ad [Agno](https://docs.agno.com/), esteso con sicurezza Zero Trust, CQRS rigoroso, replay determinista, multi-model persistence, SignalR realtime, LLMWIKY inference engine, X-Link transport.

**Corpo** = AGXIOME (orchestrazione, storage, networking). **Mente** = LLMWIKY (inferenza, ragionamento, embedding). **Sistema nervoso** = .NET 10 + SignalR + X-Link.

## Architettura a strati

```
┌─────────────────────────────────────────────────────────┐
│                   APPLICATION LAYER                      │
│  (Console, Web, Blazor, API Gateway, SignalR Client)     │
├─────────────────────────────────────────────────────────┤
│                   AGXIOME CORE                           │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌───────────┐  │
│  │  Agent   │ │  Teams   │ │Workflows │ │  Memory   │  │
│  │  Engine  │ │  Router  │ │  Engine  │ │  Manager  │  │
│  └────┬─────┘ └────┬─────┘ └────┬─────┘ └─────┬─────┘  │
│       │            │            │              │        │
│  ┌────┴────────────┴────────────┴──────────────┴─────┐  │
│  │              TOOL & FUNCTION REGISTRY              │  │
│  └─────────────────────┬─────────────────────────────┘  │
│                        │                                │
│  ┌─────────────────────┴─────────────────────────────┐  │
│  │              LLMWIKY REASONING ENGINE              │  │
│  │    (Inference, Embedding, Function Calling)        │  │
│  └─────────────────────┬─────────────────────────────┘  │
│                        │                                │
│  ┌─────────────────────┴─────────────────────────────┐  │
│  │              X-LINK TRANSPORT PROTOCOL             │  │
│  │    (Checksum, Compression, Encryption, Routing)    │  │
│  └─────────────────────┬─────────────────────────────┘  │
│                        │                                │
├────────────────────────┴────────────────────────────────┤
│                   PERSISTENCE LAYER (CQRS)              │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌───────────┐  │
│  │PostgreSQL│ │  Redis   │ │ MongoDB  │ │ Snowflake │  │
│  │(canonical│ │ (cache + │ │(doc      │ │(analytics)│  │
│  │+pgvector)│ │pub/sub)  │ │store)    │ │           │  │
│  └──────────┘ └──────────┘ └──────────┘ └───────────┘  │
├─────────────────────────────────────────────────────────┤
│                   REALTIME LAYER (SignalR)              │
│  (Agent Stream, Notifications, Presence, Backplane)     │
├─────────────────────────────────────────────────────────┤
│                   SECURITY LAYER                        │
│  (Zero Trust, HMAC/JWE, Audit, Rate Limit, CORS/CSP)    │
├─────────────────────────────────────────────────────────┤
│                   OBSERVABILITY LAYER                   │
│  (OpenTelemetry, Metrics, Logs, Traces, Alerting)       │
└─────────────────────────────────────────────────────────┘
```

## Mappa documenti

| # | File | Dominio | Sezioni | Pag |
|---|------|---------|---------|-----|
| 1 | `01-CORE-AGENT.md` | Agente base, ciclo vita, tool registry, memory | 8 | 6 |
| 2 | `02-TEAMS.md` | Team topology: Router/Swarm/Pipeline/Supervisor | 6 | 4 |
| 3 | `03-STORAGE.md` | Multi-model persistence layer + CQRS + outbox | 10 | 7 |
| 4 | `04-RAG-KNOWLEDGE.md` | RAG engine, vector search, document ingestion | 7 | 5 |
| 5 | `05-WORKFLOWS.md` | Workflow engine: DAG, runnables, retry, state machine | 6 | 4 |
| 6 | `06-REALTIME.md` | SignalR hub layer, backplane, reconnect, ordering | 7 | 5 |
| 7 | `07-SECURITY.md` | Zero Trust, HMAC/JWE chain, audit, abuse control | 8 | 6 |
| 8 | `08-LLMWIKY.md` | LLMWIKY engine spec, embedding, function calling | 7 | 5 |
| 9 | `09-XLINK.md` | X-Link transport protocol, checksum, compression | 5 | 4 |
| 10 | `10-MONITORING.md` | Observability, metrics, OpenTelemetry, alerting | 5 | 3 |
| 11 | `11-TESTING.md` | Test strategy: unit/integration/replay/chaos | 6 | 4 |
| 12 | `12-MIGRATION.md` | v5.0 migration roadmap, ADR, NFR budgets | 5 | 3 |
| 13 | `13-TOOLS.md` | Tool registry, injection, discovery, execution | 6 | 3 |
| 14 | `14-SKILLS.md` | Skills system, remote registry, derivation, composition | 5 | 3 |
| 15 | `15-LLM-PROVIDERS.md` | Provider abstraction, mappa supporto, auto-discovery | 8 | 3 |
| 16 | `16-SESSION-STATE.md` | Session, state, context engineering, dependencies | 7 | 4 |
| 17 | `17-GOVERNANCE.md` | Guardrails, HITL, approval, hooks, retry | 8 | 4 |
| 18 | `18-LEARNING.md` | Learning machine, culture, structured output | 6 | 3 |

## Nuove sezioni rilevanti

### Session, State & Context (16-SESSION-STATE.md)
- Session lifecycle: create, persist, resume
- `session_state` mutabile condiviso tra run
- Context engineering: instructions, system message, few-shot, introduction
- Dependencies injection in agent/team/tool
- Chat history management e session summary
- Workflow session state

### Governance: Guardrails, HITL & Hooks (17-GOVERNANCE.md)
- Input guardrails: PII detection, prompt injection, OpenAI moderation
- Output guardrails: content validation, structured output validation
- Human-in-the-Loop: `@approval`, `requires_confirmation`, `requires_user_input`, `external_execution`
- Approval system: admin-mediated con audit trail persistente
- Pre-hooks, post-hooks, stream hooks, tool hooks, background hooks
- Retry policy: `RetryAgentRun`, `StopAgentRun`, exponential backoff
- Error handling pattern e retry configuration

### Learning, Culture & Structured Output (18-LEARNING.md)
- Learning Machine: automatic/agentic mode
- User memory, user profile (structured), entity memory (facts+events+relationships)
- Session context (summary mode, planning mode)
- Learned knowledge (cross-user insights)
- Decision log
- Culture system: universal knowledge condiviso tra agenti
- Structured output: `output_schema`, `input_schema`, Pydantic, parser model, response model

## Architettura a strati (aggiornata)

### Tool System (13-TOOLS.md)
- `IAgxiomeTool` registry globale + per-agent
- Tool injection via config, attribute, runtime
- Tool discovery via reflection + registry
- Tool execution pipeline (pre/post hooks, audit, timeout)
- Tool versioning e deprecation

### Skills System (14-SKILLS.md)
- Skill = pacchetto di tool + prompt + knowledge pre-configurati
- Remote registry stile skills.sh
- Skill derivation: `BaseSkill -> ResearchSkill -> WebResearchSkill`
- Skill composition: agente eredita skill multiple
- Skill marketplace (futuro)

### LLM Providers (15-LLM-PROVIDERS.md)
- Provider astratto: `ILLMProvider` unico per tutti
- Mappa provider: Anthropic, OpenAI, DeepSeek, Ollama, Local, Custom
- Auto-discovery via configurazione
- Provider fallback chain
- Rate-limit adattivo per provider
- Cost tracking per provider

## Flusso decisionale end-to-end (aggiornato)

```
Client Request (REST / SignalR / gRPC)
         │
         ▼
┌──────────────────┐
│  Security Gateway │  ← Rate Limit, Auth, HMAC Verify, CORS
└────────┬─────────┘
         │
         ▼
┌──────────────────┐
│  AGXIOME Core    │
│  ┌────────────┐  │
│  │  Router    │──┼──► Agent selection / Team routing
│  └─────┬──────┘  │
│  ┌─────┴──────┐  │
│  │  Agent     │──┼──► LLMWIKY inference
│  │  Engine    │  │       │
│  └─────┬──────┘  │       │ (tool call?)
│        │         │       ▼
│  ┌─────┴──────┐  │  ┌──────────┐
│  │  Memory    │  │  │  Tool    │──► Typed C# function
│  │  Manager   │  │  │  Executor│──► Storage / API / DB
│  └────────────┘  │  └──────────┘
└────────┬─────────┘
         │
         ▼ (response)
┌──────────────────┐
│  X-Link Transport│  ← Checksum, Compress, Encrypt
└────────┬─────────┘
         │
         ▼
┌──────────────────┐
│  Response        │
│  (SignalR│REST)  │
└──────────────────┘
```

## Principi vincolanti

1. **Zero Trust Client** — mai fidato, mai eseguito lato client
2. **Replay determinista** — stesso seed = stessa esecuzione
3. **CQRS rigoroso** — comandi mutano, query leggono, mai invertito
4. **Checksum obbligatorio** — ogni transazione X-Link verificata
5. **ACID su memoria agente** — niente file JSON, tutto su DB transazionale
6. **Templates: SMARTPHONE / TABLET / DESKTOP** — parità UX completa
7. **Configuration esternalizzata** — zero hardcoded secrets
8. **Event-driven cache invalidation** — mai stale read model
