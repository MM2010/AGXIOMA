# AGXIOME — Execution Plan Index

## Scopo

Piano esecutivo dettagliato per ogni singola feature del framework AGXIOME.
Ogni file = 1 modulo implementativo con classi, metodi, DB schema, test, metriche.
Reference: [Agno docs](https://docs.agno.com/) -> porting C# su .NET 10.

## Visione

AGXIOME = corpo. LLMWIKY = mente. .NET 10 = sistema nervoso.
Framework agnostico, testabile, sicuro, resilient.

## Mappa Piani

1. `P01-CORE.md` -> Agent Engine Core -> `AgxiomeAgent.cs` -> dipendenze: —
2. `P02-AGENT-RUN.md` -> Agent Run + Stream -> `AgentExecutor.cs` -> dipendenze: P01
3. `P03-TOOLS.md` -> Tool System -> `ToolRegistry.cs` -> dipendenze: P01
4. `P04-LLMWIKY.md` -> LLMWIKY Engine -> `LLMWIKYEngine.cs` -> dipendenze: P01
5. `P05-PROVIDERS.md` -> LLM Provider Impl -> `Providers/*.cs` -> dipendenze: P04
6. `P06-TEAMS.md` -> Team Orchestration -> `AgxiomeTeam.cs` -> dipendenze: P01
7. `P07-WORKFLOWS.md` -> Workflow Engine -> `WorkflowEngine.cs` -> dipendenze: P01, P06
8. `P08-STORAGE.md` -> Storage + CQRS -> `Storage/*.cs` -> dipendenze: —
9. `P09-RAG.md` -> RAG + Knowledge -> `KnowledgeBase.cs` -> dipendenze: P08
10. `P10-SESSION.md` -> Session + State -> `SessionManager.cs` -> dipendenze: P08
11. `P11-CONTEXT.md` -> Context Engineering -> `AgentContext.cs` -> dipendenze: P01, P10
12. `P12-LEARNING.md` -> Learning Machine -> `LearningMachine.cs` -> dipendenze: P08, P10
13. `P13-GUARDRAILS.md` -> Guardrails + HITL -> `Guardrails/*.cs` -> dipendenze: P01
14. `P14-HOOKS.md` -> Hooks System -> `Hooks/*.cs` -> dipendenze: P01
15. `P15-HITL.md` -> Human-in-the-Loop -> `HITL/*.cs` -> dipendenze: P01, P03
16. `P16-SKILLS.md` -> Skills System -> `Skills/*.cs` -> dipendenze: P01, P03
17. `P17-SCHEDULER.md` -> Scheduling -> `Scheduler.cs` -> dipendenze: P07, P08
18. `P18-XLINK.md` -> X-Link Transport -> `XLinkTransport.cs` -> dipendenze: P08
19. `P19-REALTIME.md` -> SignalR Realtime -> `Hubs/*.cs` -> dipendenze: P01
20. `P20-SECURITY.md` -> Security + Crypto -> `Security/*.cs` -> dipendenze: —
21. `P21-MONITORING.md` -> Observability -> `Telemetry/*.cs` -> dipendenze: —
22. `P22-TESTING.md` -> Test Coverage -> `Tests/*.cs` -> dipendenze: Tutti

## PLAN_DETAILS

I file `PLAN_DETAILS` servono a trasformare una sezione del piano in una sequenza eseguibile task-by-task, con subtask concreti, file target, strategia test funzionale/offensiva/aggressiva e preferenza per provider reali o emulatori.

- `feature-db-providers-plan-details-1.md`: F4 DB Providers, hardening, completamento e validazione dei provider DB reali/esistenti.
- `feature-tools-infrastructure-plan-details-1.md`: F8 Tools Infrastructure, hardening, completamento e integrazione dei componenti core tools.
- `task-t68-openai-vision-ocr-plan-details-1.md`: T-68 OpenAI Vision, piano ultra-dettagliato per analisi immagine, OCR, object detection e test dedicati.

## Convenzioni Naming

```text
Namespace: AGXIOME.{Module}
Interfacce: I{Name}
Record: {Name}Record / {Name}Config
Enum: {Name}Type / {Name}Mode
Metodi async: {Verb}Async
Cancellation: CancellationToken ct = default
```

## Dipendenze NuGet (stimate)

```text
AGXIOME.Core
├── Microsoft.Extensions.DependencyInjection
├── Microsoft.Extensions.Logging
├── System.Text.Json
├── CommunityToolkit.HighPerformance  (MessagePack compat)
├── Npgsql  (PostgreSQL)
├── StackExchange.Redis
├── Microsoft.AspNetCore.SignalR
├── OpenTelemetry.*
└── Testcontainers.*  (test)
```

## Flusso deploy

```text
1. AGXIOME.Shared  → record, enum, interfacce base
2. AGXIOME.Core    → agent, tools, teams, workflows
3. AGXIOME.LLMWIKY → engine, providers
4. AGXIOME.Storage → PostgreSQL, Redis, CQRS
5. AGXIOME.XLink   → trasporto
6. AGXIOME.Realtime→ SignalR
7. AGXIOME.Security→ crypto, auth
8. AGXIOME.CLI     → builder per utente finale
```
