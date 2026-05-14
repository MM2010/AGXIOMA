# F15 — Master Agent Orchestration
## Piano di Implementazione Dettagliato — Versione 1.0
> Data: 12 Maggio 2026  
> Strategia: **Plan-and-Execute agnostico** — nessun lock-in su Redis, Postgres, o qualsiasi LLM provider.  
> Tutto comunicato via interfacce. Le implementazioni concrete sono intercambiabili.

---

## 1. Visione Architetturale

```
┌─────────────────────────────────────────────────────────────────┐
│                        USER REQUEST                             │
└───────────────────────────┬─────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│                    MASTER AGENT                                 │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────┐  │
│  │DecomposeReq  │→ │  PersistPlan │→ │   GenerateAgents     │  │
│  │Tool          │  │  Tool        │  │   Tool               │  │
│  └──────────────┘  └──────────────┘  └──────────┬───────────┘  │
│                                                  │              │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────▼───────────┐  │
│  │AdvancePlan   │← │ PollResults  │← │   SpawnAgent         │  │
│  │Tool          │  │ Tool         │  │   Tool               │  │
│  └──────────────┘  └──────────────┘  └──────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
           │                                         │
           ▼                                         ▼
┌──────────────────┐                    ┌────────────────────────┐
│  IHotStore       │                    │  SUB-AGENT (N)         │
│  (Redis/Memory/  │                    │  ┌──────────────────┐  │
│   Hazelcast/     │                    │  │ SaveResult Tool  │  │
│   Memcached)     │                    │  └──────────────────┘  │
│                  │                    │  ┌──────────────────┐  │
│  IOrchestration  │                    │  │ NotifyCompletion │  │
│  Store           │                    │  │ Tool             │  │
│  (Postgres/SQLite│                    │  └──────────────────┘  │
│   Mongo/Cosmos/  │                    └────────────────────────┘
│   DynamoDB)      │
└──────────────────┘
```

**Principi guida:**
- **Zero lock-in**: ogni componente comunica via interfaccia, non via tipo concreto
- **GUID-first**: ogni piano, task, agent instance ha un `Guid`
- **Dual-layer state**: hot store (veloce, TTL) + orchestration store (canonico, audit)
- **Event-driven**: notifiche push, non polling loop nel Master
- **Resumable**: il piano è ricostruibile interamente dall'orchestration store
- **Parallel-capable**: N sub-agent eseguono task contemporaneamente
- **Tool allowlisting**: ogni sub-agent riceve solo i tool necessari
- **LLM agnostico**: Master e sub-agent usano `ILLMProvider` — qualsiasi provider funziona

---

## 2. Struttura Progetti

```
AGXIOMA/src/
├── AGXIOME.Orchestration/          ← NEW: progetto principale F15
│   ├── AGXIOME.Orchestration.csproj
│   ├── Abstractions/
│   │   ├── IPlanOrchestrator.cs    ← contratto master loop
│   │   ├── ITaskDispatcher.cs      ← contratto spawn sub-agent
│   │   ├── IHotStore.cs            ← contratto hot storage (Redis-agnostic)
│   │   ├── IOrchestrationStore.cs  ← contratto cold storage (DB-agnostic)
│   │   ├── IAgentSpawner.cs        ← contratto factory sub-agent
│   │   ├── IResultStore.cs         ← contratto salvataggio risultati
│   │   └── ICompletionNotifier.cs  ← contratto notifica completamento
│   ├── Models/
│   │   ├── TaskSpec.cs
│   │   ├── TaskResult.cs
│   │   ├── PlanState.cs
│   │   └── OrchestrationEnums.cs
│   ├── Tools/
│   │   ├── Master/
│   │   │   ├── DecomposeRequestTool.cs
│   │   │   ├── PersistPlanTool.cs
│   │   │   ├── GenerateAgentsTool.cs
│   │   │   ├── SpawnAgentTool.cs
│   │   │   ├── PollResultsTool.cs
│   │   │   └── AdvancePlanTool.cs
│   │   └── SubAgent/
│   │       ├── SaveResultTool.cs
│   │       └── NotifyCompletionTool.cs
│   ├── Orchestrator/
│   │   ├── PlanOrchestrator.cs     ← implementazione loop principale
│   │   └── PlanResumeService.cs    ← recovery piani interrotti
│   ├── Dispatcher/
│   │   └── TaskDispatcher.cs
│   ├── Infrastructure/
│   │   ├── InMemoryHotStore.cs     ← default hot store (test + dev)
│   │   ├── InMemoryOrchestrationStore.cs  ← default cold (test + dev)
│   │   └── InMemoryCompletionNotifier.cs  ← default notifier (test + dev)
│   └── Extensions/
│       └── OrchestrationServiceExtensions.cs  ← DI registration
│
├── AGXIOME.Orchestration.Redis/    ← NEW: implementazione Redis IHotStore
│   ├── AGXIOME.Orchestration.Redis.csproj
│   └── RedisHotStore.cs
│
└── AGXIOME.Tests/
    └── F15/
        ├── F15_TS01_DomainModelTests.cs      (15 test)
        ├── F15_TS02_HotStoreContractTests.cs  (10 test)
        ├── F15_TS03_ColdStoreContractTests.cs (10 test)
        ├── F15_TS04_MasterToolsTests.cs       (20 test)
        ├── F15_TS05_SubAgentToolsTests.cs     (10 test)
        ├── F15_TS06_OrchestratorTests.cs      (15 test)
        ├── F15_TS07_DispatcherTests.cs        (10 test)
        └── F15_TS08_IntegrationTests.cs       (10 test)
```

---

## 3. Domain Models

### 3.1 `TaskSpec` — Specifica di un task atomico

```csharp
// AGXIOME.Orchestration/Models/TaskSpec.cs
namespace AGXIOME.Orchestration.Models;

/// <summary>
/// Specifica immutabile di un task che il Master assegna a un sub-agent.
/// Tutto ciò che il sub-agent deve sapere è contenuto qui — zero dipendenze esterne.
/// </summary>
public sealed record TaskSpec
{
    public required Guid TaskId { get; init; }
    public required Guid PlanId { get; init; }
    public required int StepIndex { get; init; }

    /// <summary>Descrizione naturale del task — passata direttamente come prompt al sub-agent.</summary>
    public required string Description { get; init; }

    /// <summary>Contesto aggiuntivo derivato dai task precedenti nel piano.</summary>
    public string? PriorContext { get; init; }

    /// <summary>Lista esatta di tool names che il sub-agent può usare (allowlist).</summary>
    public IReadOnlyList<string> AllowedToolNames { get; init; } = [];

    /// <summary>Parametri LLM specifici per questo task — il Master può calibrare per task type.</summary>
    public float Temperature { get; init; } = 0.7f;
    public int? TopK { get; init; }
    public float? TopP { get; init; }
    public int MaxTokens { get; init; } = 4096;
    public int MaxIterations { get; init; } = 10;

    /// <summary>ID del provider LLM preferito — null = usa il default del sistema.</summary>
    public string? PreferredLLMProvider { get; init; }

    /// <summary>Metadata arbitrari — il Master può passare qualsiasi dato strutturato.</summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }

    public OrchestratorTaskStatus Status { get; init; } = OrchestratorTaskStatus.Pending;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; init; }
    public TimeSpan? Timeout { get; init; }
}
```

### 3.2 `TaskResult` — Risultato prodotto dal sub-agent

```csharp
// AGXIOME.Orchestration/Models/TaskResult.cs
namespace AGXIOME.Orchestration.Models;

/// <summary>
/// Risultato prodotto da un sub-agent al completamento del suo task.
/// Immutabile. Serializzabile come JSON.
/// </summary>
public sealed record TaskResult
{
    public required Guid TaskId { get; init; }
    public required Guid PlanId { get; init; }
    public required bool Success { get; init; }

    /// <summary>Output grezzo del sub-agent (testo libero o JSON strutturato).</summary>
    public required string Output { get; init; }

    /// <summary>Riassunto sintetico — usato dal Master per costruire PriorContext dei task successivi.</summary>
    public string? Summary { get; init; }

    /// <summary>Dati strutturati opzionali — il sub-agent può esportare risultati typed.</summary>
    public IReadOnlyDictionary<string, object>? Data { get; init; }

    public string? ErrorMessage { get; init; }
    public string? ErrorType { get; init; }
    public int RetryCount { get; init; }
    public DateTimeOffset CompletedAt { get; init; } = DateTimeOffset.UtcNow;
    public TimeSpan ElapsedTime { get; init; }

    /// <summary>Numero di token consumati dal sub-agent — per budgeting.</summary>
    public int? TokensUsed { get; init; }
}
```

### 3.3 `PlanState` — Stato del piano globale

```csharp
// AGXIOME.Orchestration/Models/PlanState.cs
namespace AGXIOME.Orchestration.Models;

/// <summary>
/// Stato completo del piano di orchestrazione.
/// È il documento canonico — ricostruisce tutto il contesto da zero.
/// </summary>
public sealed record PlanState
{
    public required Guid PlanId { get; init; }
    public required string OriginalRequest { get; init; }
    public required string SessionId { get; init; }

    /// <summary>Tutti i task in ordine di StepIndex — possono essere paralleli (stesso StepIndex).</summary>
    public IReadOnlyList<TaskSpec> Tasks { get; init; } = [];

    public int CurrentStep { get; init; }
    public OrchestratorPlanStatus Status { get; init; } = OrchestratorPlanStatus.Decomposing;

    /// <summary>Mappa taskId → risultato dei task completati.</summary>
    public IReadOnlyDictionary<Guid, TaskResult> CompletedResults { get; init; }
        = new Dictionary<Guid, TaskResult>();

    public string? FinalSummary { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>
    /// Restituisce i task dello step corrente (possono essere paralleli).
    /// </summary>
    public IReadOnlyList<TaskSpec> CurrentStepTasks =>
        Tasks.Where(t => t.StepIndex == CurrentStep).ToList();

    /// <summary>True se tutti i task dello step corrente sono completati.</summary>
    public bool IsCurrentStepComplete =>
        CurrentStepTasks.All(t => CompletedResults.ContainsKey(t.TaskId));
}
```

### 3.4 Enum

```csharp
// AGXIOME.Orchestration/Models/OrchestrationEnums.cs
namespace AGXIOME.Orchestration.Models;

public enum OrchestratorTaskStatus
{
    Pending,
    Running,
    Done,
    Failed,
    Skipped,
    Cancelled
}

public enum OrchestratorPlanStatus
{
    Decomposing,
    Running,
    Paused,
    Completed,
    Failed,
    Cancelled
}

/// <summary>Tipo di parallelismo per un gruppo di task dello stesso step.</summary>
public enum StepExecutionMode
{
    Sequential,     // un task alla volta
    Parallel,       // tutti in parallelo
    RaceFirst,      // il primo a finire vince, gli altri vengono cancellati
    Quorum          // aspetta N/2+1 completamenti
}
```

---

## 4. Interfacce (Layer Agnostico)

### 4.1 `IHotStore` — Fast transient state storage

```csharp
// AGXIOME.Orchestration/Abstractions/IHotStore.cs
namespace AGXIOME.Orchestration.Abstractions;

/// <summary>
/// Contratto per lo storage veloce e transitorio (Redis, Hazelcast, Memcached, InMemory...).
/// Nessun tipo concreto esposto. Tutti i valori sono stringhe serializzate.
/// Il caller gestisce la serializzazione — lo store gestisce solo bytes.
/// </summary>
public interface IHotStore
{
    Task SetAsync(string key, string value, TimeSpan? ttl = null, CancellationToken ct = default);
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);

    /// <summary>Atomicamente incrementa un contatore. Usato per step index.</summary>
    Task<long> IncrementAsync(string key, CancellationToken ct = default);

    /// <summary>Aggiunge elementi a un set. Usato per tracking taskIds per piano.</summary>
    Task SetAddAsync(string key, string member, CancellationToken ct = default);
    Task<IReadOnlySet<string>> SetMembersAsync(string key, CancellationToken ct = default);

    /// <summary>Subscribe a key changes — implementazione opzionale (non tutti gli store la supportano).</summary>
    IAsyncEnumerable<string> SubscribeAsync(string channel, CancellationToken ct = default);
}
```

### 4.2 `IOrchestrationStore` — Canonical persistent storage

```csharp
// AGXIOME.Orchestration/Abstractions/IOrchestrationStore.cs
namespace AGXIOME.Orchestration.Abstractions;

/// <summary>
/// Contratto per lo storage canonico dei piani e risultati.
/// Implementabile su Postgres, SQLite, MongoDB, CosmosDB, DynamoDB, etc.
/// Ogni metodo restituisce null se l'entità non esiste (no exception per not-found).
/// </summary>
public interface IOrchestrationStore
{
    // ── Piano ──────────────────────────────────────────────────────────────
    Task SavePlanAsync(PlanState plan, CancellationToken ct = default);
    Task<PlanState?> GetPlanAsync(Guid planId, CancellationToken ct = default);
    Task UpdatePlanStatusAsync(Guid planId, OrchestratorPlanStatus status,
        string? finalSummary = null, CancellationToken ct = default);

    /// <summary>Aggiorna atomicamente il CurrentStep e il set di risultati.</summary>
    Task AdvancePlanStepAsync(Guid planId, int newStep,
        IReadOnlyDictionary<Guid, TaskResult> newResults, CancellationToken ct = default);

    /// <summary>Recupera tutti i piani con status Running o Paused — usato per recovery.</summary>
    Task<IReadOnlyList<PlanState>> GetActivePlansAsync(CancellationToken ct = default);

    // ── Task ───────────────────────────────────────────────────────────────
    Task SaveTaskSpecAsync(TaskSpec spec, CancellationToken ct = default);
    Task<TaskSpec?> GetTaskSpecAsync(Guid taskId, CancellationToken ct = default);
    Task UpdateTaskStatusAsync(Guid taskId, OrchestratorTaskStatus status,
        CancellationToken ct = default);

    // ── Risultati ──────────────────────────────────────────────────────────
    Task SaveResultAsync(TaskResult result, CancellationToken ct = default);
    Task<TaskResult?> GetResultAsync(Guid taskId, CancellationToken ct = default);
    Task<IReadOnlyList<TaskResult>> GetPlanResultsAsync(Guid planId,
        CancellationToken ct = default);
}
```

### 4.3 `IAgentSpawner` — Sub-agent factory agnostica

```csharp
// AGXIOME.Orchestration/Abstractions/IAgentSpawner.cs
namespace AGXIOME.Orchestration.Abstractions;

/// <summary>
/// Contratto per la creazione e avvio di sub-agent.
/// Implementabile via BackgroundRunWorker, SignalR, HTTP remota, WASM sandbox, etc.
/// </summary>
public interface IAgentSpawner
{
    /// <summary>
    /// Avvia un sub-agent con la specifica fornita.
    /// Restituisce il JobId opaco — non dipende dall'implementazione interna.
    /// </summary>
    Task<string> SpawnAsync(TaskSpec spec, CancellationToken ct = default);

    /// <summary>Cancella un sub-agent in esecuzione.</summary>
    Task CancelAsync(string jobId, CancellationToken ct = default);

    /// <summary>Verifica se il job è ancora attivo (non terminale).</summary>
    Task<bool> IsActiveAsync(string jobId, CancellationToken ct = default);
}
```

### 4.4 `ICompletionNotifier` — Event bus agnostico

```csharp
// AGXIOME.Orchestration/Abstractions/ICompletionNotifier.cs
namespace AGXIOME.Orchestration.Abstractions;

/// <summary>
/// Contratto per la notifica di completamento task.
/// Implementabile via SignalR, pub/sub Redis, in-memory Channel<T>, webhook, gRPC stream.
/// Il Master si abbona; il sub-agent pubblica.
/// </summary>
public interface ICompletionNotifier
{
    /// <summary>Sub-agent chiama questo metodo al completamento.</summary>
    Task PublishAsync(TaskResult result, CancellationToken ct = default);

    /// <summary>
    /// Master si abbona ai completamenti per un piano specifico.
    /// Stream termina automaticamente quando tutti i task del piano sono Done/Failed.
    /// </summary>
    IAsyncEnumerable<TaskResult> SubscribeAsync(Guid planId, CancellationToken ct = default);
}
```

### 4.5 `IPlanOrchestrator` — Contratto del loop principale

```csharp
// AGXIOME.Orchestration/Abstractions/IPlanOrchestrator.cs
namespace AGXIOME.Orchestration.Abstractions;

/// <summary>
/// Contratto del Master orchestrator.
/// Riceve una richiesta utente → pianifica → esegue → sintetizza.
/// </summary>
public interface IPlanOrchestrator
{
    /// <summary>
    /// Avvia un nuovo piano. Restituisce il PlanId assegnato.
    /// Ritorna immediatamente — l'esecuzione avviene in background.
    /// </summary>
    Task<Guid> StartAsync(string request, string sessionId,
        OrchestratorOptions? options = null, CancellationToken ct = default);

    /// <summary>Riprende un piano paused o interrotto.</summary>
    Task ResumeAsync(Guid planId, CancellationToken ct = default);

    /// <summary>Pausa un piano in esecuzione.</summary>
    Task PauseAsync(Guid planId, CancellationToken ct = default);

    /// <summary>Cancella un piano e tutti i sub-agent attivi.</summary>
    Task CancelAsync(Guid planId, CancellationToken ct = default);

    /// <summary>Stato corrente del piano.</summary>
    Task<PlanState?> GetStateAsync(Guid planId, CancellationToken ct = default);

    /// <summary>Stream degli eventi del piano (task started, completed, plan done, etc.).</summary>
    IAsyncEnumerable<OrchestratorEvent> WatchAsync(Guid planId, CancellationToken ct = default);
}

/// <summary>Opzioni globali per l'esecuzione del piano.</summary>
public record OrchestratorOptions
{
    public int MaxParallelTasks { get; init; } = 4;
    public TimeSpan DefaultTaskTimeout { get; init; } = TimeSpan.FromMinutes(5);
    public bool EnableAutoResume { get; init; } = true;
    public string? PreferredLLMProvider { get; init; }
    public IReadOnlyList<string>? GlobalToolAllowlist { get; init; }
}

/// <summary>Evento emesso dall'orchestrator durante l'esecuzione del piano.</summary>
public abstract record OrchestratorEvent(Guid PlanId, DateTimeOffset Timestamp);
public sealed record PlanStartedEvent(Guid PlanId, DateTimeOffset Timestamp) : OrchestratorEvent(PlanId, Timestamp);
public sealed record TaskSpawnedEvent(Guid PlanId, Guid TaskId, int StepIndex, DateTimeOffset Timestamp) : OrchestratorEvent(PlanId, Timestamp);
public sealed record TaskCompletedEvent(Guid PlanId, Guid TaskId, bool Success, DateTimeOffset Timestamp) : OrchestratorEvent(PlanId, Timestamp);
public sealed record StepAdvancedEvent(Guid PlanId, int OldStep, int NewStep, DateTimeOffset Timestamp) : OrchestratorEvent(PlanId, Timestamp);
public sealed record PlanCompletedEvent(Guid PlanId, string Summary, DateTimeOffset Timestamp) : OrchestratorEvent(PlanId, Timestamp);
public sealed record PlanFailedEvent(Guid PlanId, string Reason, DateTimeOffset Timestamp) : OrchestratorEvent(PlanId, Timestamp);
```

---

## 5. Hot Store — Schema chiavi

```
Prefisso:  agxiome:orch:

agxiome:orch:plan:{planId}:meta          → JSON serializzato di PlanState (TTL: 72h)
agxiome:orch:plan:{planId}:step          → indice step corrente come stringa int (INCR atomico)
agxiome:orch:plan:{planId}:status        → "Decomposing|Running|Paused|Completed|Failed"
agxiome:orch:plan:{planId}:tasks         → Set di taskId (string GUID) per navigazione rapida
agxiome:orch:task:{taskId}:spec          → JSON serializzato di TaskSpec (TTL: 24h)
agxiome:orch:task:{taskId}:result        → JSON serializzato di TaskResult (TTL: 48h)
agxiome:orch:task:{taskId}:status        → "Pending|Running|Done|Failed"
agxiome:orch:task:{taskId}:jobid         → jobId opaco assegnato da IAgentSpawner
agxiome:orch:session:{sessionId}:plans   → Set dei planId della sessione
agxiome:orch:notify:{planId}             → canale pub/sub per ICompletionNotifier
```

**Regole:**
- Tutti i TTL si resettano ad ogni write (sliding TTL).
- Le chiavi `plan:{planId}:step` e `plan:{planId}:status` sono le più accedute — NON duplicare in hot store oggetti grandi.
- Il payload `plan:{planId}:meta` viene scritto solo su: creazione piano, avanzamento step, completamento/fallimento.

---

## 6. Orchestration Store — Schema DB

```sql
-- Tabella piani (una riga = un piano completo serializzato)
CREATE TABLE orchestration_plans (
    plan_id         UUID PRIMARY KEY,
    session_id      TEXT NOT NULL,
    original_request TEXT NOT NULL,
    status          TEXT NOT NULL,
    current_step    INTEGER NOT NULL DEFAULT 0,
    tasks_json      JSONB NOT NULL,           -- TaskSpec[]
    results_json    JSONB NOT NULL DEFAULT '{}',  -- {taskId: TaskResult}
    final_summary   TEXT,
    created_at      TIMESTAMPTZ NOT NULL,
    completed_at    TIMESTAMPTZ,
    updated_at      TIMESTAMPTZ NOT NULL
);

-- Tabella task (per query per piano e navigazione individuale)
CREATE TABLE orchestration_tasks (
    task_id     UUID PRIMARY KEY,
    plan_id     UUID NOT NULL REFERENCES orchestration_plans(plan_id),
    step_index  INTEGER NOT NULL,
    status      TEXT NOT NULL,
    spec_json   JSONB NOT NULL,
    created_at  TIMESTAMPTZ NOT NULL,
    started_at  TIMESTAMPTZ,
    updated_at  TIMESTAMPTZ NOT NULL
);

-- Tabella risultati (separata per auditing indipendente)
CREATE TABLE orchestration_results (
    task_id      UUID PRIMARY KEY,
    plan_id      UUID NOT NULL,
    success      BOOLEAN NOT NULL,
    output       TEXT NOT NULL,
    summary      TEXT,
    data_json    JSONB,
    error_msg    TEXT,
    tokens_used  INTEGER,
    elapsed_ms   BIGINT,
    completed_at TIMESTAMPTZ NOT NULL
);

-- Indici operativi
CREATE INDEX idx_plans_session   ON orchestration_plans(session_id);
CREATE INDEX idx_plans_status    ON orchestration_plans(status) WHERE status IN ('Running','Paused');
CREATE INDEX idx_tasks_plan      ON orchestration_tasks(plan_id);
CREATE INDEX idx_results_plan    ON orchestration_results(plan_id);
```

**Note:**
- Questo schema è espresso in SQL PostgreSQL come **riferimento**.
- L'implementazione concreta di `IOrchestrationStore` può usare EF Core, Dapper, o qualsiasi ORM.
- Per SQLite: stesso schema, rimuovere `JSONB` → `TEXT`, `UUID` → `TEXT`.
- Per MongoDB: documento `plans` con subdocumenti embedded per tasks e results.
- Per CosmosDB: container `orchestration` con partition key `/plan_id`.

---

## 7. Master Tools — Specifica Dettagliata

### 7.1 `DecomposeRequestTool`

**Scopo**: trasforma una richiesta utente in una lista ordinata di `TaskSpec`.  
**Input**:
```json
{
  "request": "string — la richiesta originale dell'utente",
  "sessionId": "string — session ID corrente",
  "context": "string? — contesto aggiuntivo (doc, history, etc.)",
  "maxTasks": "int? — default 8, max 20",
  "allowedProviders": "string[]? — LLM provider allowlist per sub-agent"
}
```
**Output**:
```json
{
  "planId": "guid",
  "tasks": [
    {
      "taskId": "guid",
      "stepIndex": 0,
      "description": "...",
      "allowedToolNames": ["SearchTool", "WebFetchTool"],
      "temperature": 0.5,
      "estimatedComplexity": "low|medium|high"
    }
  ],
  "decompositionRationale": "string — perché il Master ha scelto questa suddivisione"
}
```
**Comportamento interno**:
1. Costruisce un prompt strutturato che descrive al LLM come decomporre in task atomici
2. Richiede output JSON con schema validato (`JsonSchema` o `System.Text.Json`)
3. Assegna `Guid.NewGuid()` a ogni task
4. Valida: `stepIndex` sequenziale, almeno 1 task, nessun task con description vuota
5. NON persiste — è solo decomposizione; la persistenza è compito di `PersistPlanTool`

**Firma implementazione**:
```csharp
public sealed class DecomposeRequestTool : IAgxiomeTool
{
    public string Name => "DecomposeRequest";
    public string Description => "Transforms a user request into an ordered list of atomic tasks for sub-agents";

    private readonly ILLMProvider _llm;
    private readonly IJsonSchemaValidator _schema;   // agnostico — non dipende da Newtonsoft

    public DecomposeRequestTool(ILLMProvider llm, IJsonSchemaValidator schema) { ... }

    public async Task<ToolResult> InvokeAsync(ToolCall call, CancellationToken ct) { ... }
}
```

---

### 7.2 `PersistPlanTool`

**Scopo**: salva il piano decomposto in hot store + orchestration store.  
**Input**:
```json
{
  "planId": "guid",
  "sessionId": "string",
  "originalRequest": "string",
  "tasks": "TaskSpec[]"
}
```
**Output**:
```json
{
  "persisted": true,
  "planId": "guid",
  "taskCount": 5,
  "hotStoreTtlHours": 72
}
```
**Comportamento interno**:
1. Costruisce `PlanState` con `Status = Decomposing`
2. `IOrchestrationStore.SavePlanAsync(plan)` — storage canonico
3. `IHotStore.SetAsync("agxiome:orch:plan:{planId}:meta", json, 72h)` — hot cache
4. `IHotStore.IncrementAsync("agxiome:orch:plan:{planId}:step")` → inizializza a 0
5. Per ogni task: `IHotStore.SetAsync("agxiome:orch:task:{taskId}:spec", json, 24h)`
6. `IHotStore.SetAsync("agxiome:orch:plan:{planId}:status", "Decomposing")`
7. Ritorna conferma

**Firma implementazione**:
```csharp
public sealed class PersistPlanTool : IAgxiomeTool
{
    public string Name => "PersistPlan";

    private readonly IHotStore _hot;
    private readonly IOrchestrationStore _cold;
    private readonly IJsonSerializer _json;

    public PersistPlanTool(IHotStore hot, IOrchestrationStore cold, IJsonSerializer json) { ... }
}
```

---

### 7.3 `GenerateAgentsTool`

**Scopo**: determina la configurazione ottimale dei sub-agent per ogni task.  
**Input**:
```json
{
  "planId": "guid",
  "tasks": "TaskSpec[]",
  "availableTools": "string[] — tutti i tool disponibili nel sistema",
  "llmProviders": "string[] — provider LLM disponibili"
}
```
**Output**:
```json
{
  "agentConfigurations": [
    {
      "taskId": "guid",
      "agentRole": "string — descrizione del ruolo del sub-agent",
      "allowedToolNames": ["..."],
      "preferredLLMProvider": "openai:gpt-4o",
      "temperature": 0.3,
      "systemPrompt": "string — istruzioni specifiche del ruolo"
    }
  ]
}
```
**Comportamento interno**:
1. Chiama il LLM con un prompt che descrive i task e chiede la configurazione ottimale degli agent
2. Per ogni task: selezione tool by task type (research → search+web; coding → code exec; analysis → no tools)
3. Temperature calibrata per tipo: factual=0.2, creative=0.9, analytical=0.5
4. Valida che ogni `allowedToolNames` sia sottoinsieme di `availableTools`
5. Produce `systemPrompt` role-specific per massimizzare la qualità del sub-agent

---

### 7.4 `SpawnAgentTool`

**Scopo**: istanzia e avvia i sub-agent per il current step.  
**Input**:
```json
{
  "planId": "guid",
  "stepIndex": 0,
  "agentConfigurations": "[{taskId, allowedToolNames, ...}]",
  "executionMode": "Sequential|Parallel|RaceFirst|Quorum"
}
```
**Output**:
```json
{
  "spawnedJobs": [
    { "taskId": "guid", "jobId": "string — opaco, dipende da IAgentSpawner" }
  ]
}
```
**Comportamento interno**:
1. Per ogni `agentConfiguration`:
   a. Legge `TaskSpec` da `IHotStore`
   b. Chiama `IAgentSpawner.SpawnAsync(spec)` → jobId
   c. `IHotStore.SetAsync("agxiome:orch:task:{taskId}:jobid", jobId)`
   d. `IHotStore.SetAsync("agxiome:orch:task:{taskId}:status", "Running")`
   e. `IOrchestrationStore.UpdateTaskStatusAsync(taskId, Running)`
2. Se `Parallel`: avvia tutti i task con `Task.WhenAll`
3. Se `Sequential`: avvia in sequenza (aspetta completamento prima di spawn successivo)
4. Se `RaceFirst`: avvia tutti, ma `ICompletionNotifier.SubscribeAsync` cancella gli altri al primo completamento
5. Aggiorna `IHotStore` con lista jobId per tracking

---

### 7.5 `PollResultsTool`

**Scopo**: attende i completamenti dal `ICompletionNotifier` e aggiorna lo stato.  
**Input**:
```json
{
  "planId": "guid",
  "stepIndex": 0,
  "expectedTaskIds": ["guid", "guid"],
  "timeoutSeconds": 300
}
```
**Output**:
```json
{
  "completedTasks": [
    { "taskId": "guid", "success": true, "summary": "..." }
  ],
  "failedTasks": [
    { "taskId": "guid", "errorMessage": "..." }
  ],
  "timedOutTasks": [],
  "allComplete": true
}
```
**Comportamento interno**:
1. `ICompletionNotifier.SubscribeAsync(planId, ct)` → stream di `TaskResult`
2. Raccoglie risultati finché `expectedTaskIds` non sono tutti ricevuti o timeout
3. Per ogni result ricevuto:
   a. `IHotStore.SetAsync("agxiome:orch:task:{taskId}:result", json)`
   b. `IHotStore.SetAsync("agxiome:orch:task:{taskId}:status", "Done|Failed")`
   c. `IOrchestrationStore.SaveResultAsync(result)`
4. Emette `TaskCompletedEvent` per ogni task completato
5. Alla fine restituisce sommario strutturato

---

### 7.6 `AdvancePlanTool`

**Scopo**: analizza i risultati dello step corrente, sintetizza, avanza o conclude il piano.  
**Input**:
```json
{
  "planId": "guid",
  "completedStepResults": "[TaskResult]",
  "nextStepIndex": 1,
  "totalSteps": 3
}
```
**Output**:
```json
{
  "decision": "Advance|Complete|Fail|Retry",
  "nextStep": 1,
  "synthesizedContext": "string — contesto costruito dai risultati per i task successivi",
  "finalSummary": "string? — solo se decision=Complete",
  "retryTaskIds": ["guid"] // solo se decision=Retry
}
```
**Comportamento interno**:
1. Costruisce un prompt con tutti i `TaskResult.Summary` dello step corrente
2. Chiede al LLM: questi risultati sono sufficienti per procedere? Sintetizza il contesto.
3. Se `decision = Advance`:
   a. `IHotStore.IncrementAsync("agxiome:orch:plan:{planId}:step")`
   b. Inietta `synthesizedContext` nelle `TaskSpec` dello step successivo (campo `PriorContext`)
   c. `IOrchestrationStore.AdvancePlanStepAsync(planId, nextStep, results)`
4. Se `decision = Complete`:
   a. `IOrchestrationStore.UpdatePlanStatusAsync(planId, Completed, finalSummary)`
   b. Emette `PlanCompletedEvent`
5. Se `decision = Fail`:
   a. Cancella tutti i job attivi via `IAgentSpawner.CancelAsync`
   b. `IOrchestrationStore.UpdatePlanStatusAsync(planId, Failed)`

---

## 8. Sub-Agent Tools — Specifica Dettagliata

### 8.1 `SaveResultTool`

**Scopo**: il sub-agent salva il suo output nell'orchestration storage.  
**Input**:
```json
{
  "taskId": "guid",
  "planId": "guid",
  "output": "string",
  "summary": "string",
  "success": true,
  "data": "{}? — dati strutturati opzionali",
  "tokensUsed": 1240
}
```
**Comportamento interno**:
1. Costruisce `TaskResult` con `ElapsedTime` misurato
2. `IHotStore.SetAsync("agxiome:orch:task:{taskId}:result", json, 48h)`
3. `IOrchestrationStore.SaveResultAsync(result)`
4. NON notifica — la notifica è compito di `NotifyCompletionTool`

---

### 8.2 `NotifyCompletionTool`

**Scopo**: il sub-agent notifica il Master del completamento del suo task.  
**Input**:
```json
{
  "taskId": "guid",
  "planId": "guid",
  "success": true
}
```
**Comportamento interno**:
1. Legge il `TaskResult` da `IHotStore` (già salvato da `SaveResultTool`)
2. `ICompletionNotifier.PublishAsync(result)` → master riceve via `SubscribeAsync`
3. `IHotStore.SetAsync("agxiome:orch:task:{taskId}:status", "Done|Failed")`
4. Log dell'evento con timestamp e taskId

**Nota**: `SaveResultTool` + `NotifyCompletionTool` sono tool separati per principio di singola responsabilità. Il sub-agent può chiamarli in sequenza o combinare in un wrapper.

---

## 9. Orchestrator Loop — Implementazione

```csharp
// AGXIOME.Orchestration/Orchestrator/PlanOrchestrator.cs

public sealed class PlanOrchestrator : IPlanOrchestrator
{
    private readonly IAgxiomeAgent _masterAgent;    // Agent con 6 master tools registrati
    private readonly IOrchestrationStore _cold;
    private readonly IHotStore _hot;
    private readonly ICompletionNotifier _notifier;
    private readonly IAgentSpawner _spawner;
    private readonly Channel<OrchestratorEvent> _events;
    private readonly ILogger<PlanOrchestrator> _log;

    public async Task<Guid> StartAsync(string request, string sessionId,
        OrchestratorOptions? options = null, CancellationToken ct = default)
    {
        var planId = Guid.NewGuid();
        // Avvia il loop in background — non blocca il caller
        _ = Task.Run(() => RunPlanLoopAsync(planId, request, sessionId, options, ct), ct);
        return planId;
    }

    private async Task RunPlanLoopAsync(Guid planId, string request, string sessionId,
        OrchestratorOptions? options, CancellationToken ct)
    {
        try
        {
            await _events.Writer.WriteAsync(new PlanStartedEvent(planId, DateTimeOffset.UtcNow), ct);

            // STEP 1 — Decomposizione
            var decomposeResponse = await _masterAgent.RunAsync(new AgentRequest(
                $"DecomposeRequest: {request}",
                Context: new AgentContext { ["planId"] = planId, ["sessionId"] = sessionId }
            ), ct);

            // STEP 2 — Persistenza (il Master chiama PersistPlanTool internamente)

            // STEP 3 — Leggi piano dal cold store (fonte di verità)
            var plan = await _cold.GetPlanAsync(planId, ct)
                ?? throw new InvalidOperationException($"Plan {planId} not found after decomposition");

            // STEP 4 — Loop per ogni step
            while (plan.Status == OrchestratorPlanStatus.Running && !ct.IsCancellationRequested)
            {
                // Genera configurazioni agent per lo step corrente
                var genResponse = await _masterAgent.RunAsync(new AgentRequest(
                    $"GenerateAgents for step {plan.CurrentStep} of plan {planId}",
                    Context: BuildStepContext(plan)
                ), ct);

                // Spawn sub-agent per lo step
                var spawnResponse = await _masterAgent.RunAsync(new AgentRequest(
                    $"SpawnAgent for step {plan.CurrentStep} of plan {planId}",
                    Context: BuildStepContext(plan)
                ), ct);

                // Attendi completamenti
                var pollResponse = await _masterAgent.RunAsync(new AgentRequest(
                    $"PollResults for step {plan.CurrentStep} of plan {planId}",
                    Context: BuildStepContext(plan)
                ), ct);

                // Avanza o conclude
                var advanceResponse = await _masterAgent.RunAsync(new AgentRequest(
                    $"AdvancePlan for step {plan.CurrentStep} of plan {planId}",
                    Context: BuildStepContext(plan)
                ), ct);

                // Rileggi stato aggiornato
                plan = await _cold.GetPlanAsync(planId, ct) ?? plan;
            }
        }
        catch (OperationCanceledException) { /* normale cancellazione */ }
        catch (Exception ex)
        {
            _log.LogError(ex, "Plan {PlanId} failed with unhandled exception", planId);
            await _cold.UpdatePlanStatusAsync(planId, OrchestratorPlanStatus.Failed, ct: ct);
            await _events.Writer.WriteAsync(new PlanFailedEvent(planId, ex.Message, DateTimeOffset.UtcNow), ct);
        }
    }

    private static AgentContext BuildStepContext(PlanState plan) => new()
    {
        ["planId"]       = plan.PlanId,
        ["currentStep"]  = plan.CurrentStep,
        ["totalSteps"]   = plan.Tasks.Select(t => t.StepIndex).Max() + 1,
        ["completedIds"] = plan.CompletedResults.Keys.Select(g => g.ToString()).ToArray()
    };
}
```

---

## 10. Infrastructure Defaults — InMemory (Test + Dev)

```csharp
// Per test e sviluppo locale — nessuna dipendenza esterna

// InMemoryHotStore: ConcurrentDictionary<string, (string Value, DateTimeOffset? Expires)>
// InMemoryOrchestrationStore: ConcurrentDictionary<Guid, PlanState> + ConcurrentDictionary<Guid, TaskResult>
// InMemoryCompletionNotifier: Channel<TaskResult> per pianId

// Questi sono le implementazioni di DEFAULT registrate in DI.
// Le implementazioni Redis/Postgres vengono sostituite in produzione via DI overrides.
```

---

## 11. DI Registration

```csharp
// AGXIOME.Orchestration/Extensions/OrchestrationServiceExtensions.cs

public static class OrchestrationServiceExtensions
{
    /// <summary>
    /// Registra l'orchestrazione con implementazioni InMemory (dev/test).
    /// Sostituire IHotStore, IOrchestrationStore, ICompletionNotifier in produzione.
    /// </summary>
    public static IServiceCollection AddAgxiomeOrchestration(
        this IServiceCollection services,
        Action<OrchestratorOptions>? configure = null)
    {
        var options = new OrchestratorOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);

        // Defaults — intercambiabili via DI
        services.TryAddSingleton<IHotStore, InMemoryHotStore>();
        services.TryAddSingleton<IOrchestrationStore, InMemoryOrchestrationStore>();
        services.TryAddSingleton<ICompletionNotifier, InMemoryCompletionNotifier>();
        services.TryAddSingleton<IAgentSpawner, BackgroundWorkerAgentSpawner>();

        // Tools master — tutti registrabili individualmente
        services.AddTransient<DecomposeRequestTool>();
        services.AddTransient<PersistPlanTool>();
        services.AddTransient<GenerateAgentsTool>();
        services.AddTransient<SpawnAgentTool>();
        services.AddTransient<PollResultsTool>();
        services.AddTransient<AdvancePlanTool>();

        // Tools sub-agent
        services.AddTransient<SaveResultTool>();
        services.AddTransient<NotifyCompletionTool>();

        // Orchestrator principale
        services.AddSingleton<IPlanOrchestrator, PlanOrchestrator>();

        // Recovery service — rileva piani Running/Paused all'avvio
        services.AddHostedService<PlanResumeService>();

        return services;
    }

    /// <summary>Override per Redis hot store.</summary>
    public static IServiceCollection UseRedisHotStore(
        this IServiceCollection services, string connectionString)
    {
        services.AddSingleton<IHotStore>(_ => new RedisHotStore(connectionString));
        return services;
    }

    /// <summary>Override per Postgres orchestration store.</summary>
    public static IServiceCollection UsePostgresOrchestrationStore(
        this IServiceCollection services, string connectionString)
    {
        services.AddSingleton<IOrchestrationStore>(_ =>
            new PostgresOrchestrationStore(connectionString));
        return services;
    }
}
```

---

## 12. Piano di Esecuzione — Task Table

| ID | Task | Progetto | Dipende da | Test | Priorità |
|----|------|----------|-----------|------|---------|
| **F15-01** | Crea progetto `AGXIOME.Orchestration` + `.csproj` con refs | Orchestration | F0-06 | 0 | P0 |
| **F15-02** | `OrchestrationEnums.cs` — `OrchestratorTaskStatus`, `OrchestratorPlanStatus`, `StepExecutionMode` | Orchestration | F15-01 | 5 | P0 |
| **F15-03** | `TaskSpec.cs` — record immutabile con validazione | Orchestration | F15-02 | 8 | P0 |
| **F15-04** | `TaskResult.cs` — record immutabile | Orchestration | F15-02 | 5 | P0 |
| **F15-05** | `PlanState.cs` — record con `CurrentStepTasks`, `IsCurrentStepComplete` | Orchestration | F15-03, F15-04 | 8 | P0 |
| **F15-06** | `IHotStore.cs` — interfaccia completa con Set/Get/Delete/Incr/SetAdd | Orchestration | F15-01 | 0 | P0 |
| **F15-07** | `IOrchestrationStore.cs` — interfaccia completa | Orchestration | F15-05 | 0 | P0 |
| **F15-08** | `IAgentSpawner.cs`, `ICompletionNotifier.cs`, `IPlanOrchestrator.cs` | Orchestration | F15-06, F15-07 | 0 | P0 |
| **F15-09** | `OrchestratorEvent` hierarchy — 6 record derivati | Orchestration | F15-08 | 5 | P0 |
| **F15-10** | `InMemoryHotStore.cs` — implementazione ConcurrentDictionary + TTL | Orchestration | F15-06 | 10 | P1 |
| **F15-11** | `InMemoryOrchestrationStore.cs` — implementazione completa | Orchestration | F15-07 | 10 | P1 |
| **F15-12** | `InMemoryCompletionNotifier.cs` — Channel<TaskResult> per planId | Orchestration | F15-08 | 8 | P1 |
| **F15-13** | `DecomposeRequestTool.cs` + test | Orchestration | F15-08, F15-10 | 8 | P1 |
| **F15-14** | `PersistPlanTool.cs` + test | Orchestration | F15-10, F15-11 | 8 | P1 |
| **F15-15** | `GenerateAgentsTool.cs` + test | Orchestration | F15-13 | 6 | P1 |
| **F15-16** | `SpawnAgentTool.cs` + test | Orchestration | F15-15, F15-12 | 8 | P2 |
| **F15-17** | `PollResultsTool.cs` + test | Orchestration | F15-12, F15-16 | 8 | P2 |
| **F15-18** | `AdvancePlanTool.cs` + test | Orchestration | F15-17 | 8 | P2 |
| **F15-19** | `SaveResultTool.cs` + test | Orchestration | F15-10, F15-11 | 5 | P2 |
| **F15-20** | `NotifyCompletionTool.cs` + test | Orchestration | F15-12, F15-19 | 5 | P2 |
| **F15-21** | `BackgroundWorkerAgentSpawner.cs` — implementa `IAgentSpawner` via `IBackgroundRunWorker` | Orchestration | F15-08 | 5 | P2 |
| **F15-22** | `PlanOrchestrator.cs` — loop principale senza DI | Orchestration | F15-13..F15-21 | 10 | P3 |
| **F15-23** | `PlanResumeService.cs` — `IHostedService` per recovery all'avvio | Orchestration | F15-22 | 5 | P3 |
| **F15-24** | `OrchestrationServiceExtensions.cs` — DI registration + overrides | Orchestration | F15-22 | 5 | P3 |
| **F15-25** | Crea progetto `AGXIOME.Orchestration.Redis` + `RedisHotStore.cs` | Orch.Redis | F15-06 | 5 | P3 |
| **F15-26** | `PostgresOrchestrationStore.cs` in `AGXIOME.Storage` | Storage | F15-07 | 5 | P3 |
| **F15-27** | Test file `F15_TS01_DomainModelTests.cs` (15 test su models + enums) | Tests | F15-05 | 15 | P1 |
| **F15-28** | Test file `F15_TS02_HotStoreContractTests.cs` (10 test su InMemoryHotStore) | Tests | F15-10 | 10 | P1 |
| **F15-29** | Test file `F15_TS03_ColdStoreContractTests.cs` (10 test su InMemoryOrchestrationStore) | Tests | F15-11 | 10 | P1 |
| **F15-30** | Test file `F15_TS04_MasterToolsTests.cs` (20 test su 6 tools in isolamento) | Tests | F15-18 | 20 | P2 |
| **F15-31** | Test file `F15_TS05_SubAgentToolsTests.cs` (10 test su SaveResult + NotifyCompletion) | Tests | F15-20 | 10 | P2 |
| **F15-32** | Test file `F15_TS06_OrchestratorTests.cs` (15 test loop con mock agent) | Tests | F15-22 | 15 | P3 |
| **F15-33** | Test file `F15_TS07_DispatcherTests.cs` (10 test spawn + cancel) | Tests | F15-21 | 10 | P3 |
| **F15-34** | Test file `F15_TS08_IntegrationTests.cs` (10 test end-to-end con InMemory) | Tests | F15-32 | 10 | P3 |

**Totale test F15: 100**

---

## 13. Sequenza di Esecuzione Raccomandata

```
Settimana 1 — Foundation:
  F15-01 → F15-02 → F15-03 → F15-04 → F15-05 (models)
  F15-06 → F15-07 → F15-08 → F15-09 (interfaces)
  F15-27 — TS01 (model tests) ✓

Settimana 1 → 2 — InMemory Infrastructure:
  F15-10 → F15-11 → F15-12 (InMemory impls)
  F15-28 — TS02, F15-29 — TS03 ✓

Settimana 2 — Master Tools:
  F15-13 → F15-14 → F15-15 (decompose + persist + generate)
  F15-16 → F15-17 → F15-18 (spawn + poll + advance)
  F15-30 — TS04 ✓

Settimana 2 → 3 — SubAgent Tools + Spawner:
  F15-19 → F15-20 (sub-agent tools)
  F15-21 (BackgroundWorkerAgentSpawner)
  F15-31 — TS05 ✓

Settimana 3 — Orchestrator + DI:
  F15-22 → F15-23 → F15-24 (orchestrator + resume + DI)
  F15-32 — TS06, F15-33 — TS07, F15-34 — TS08 ✓

Settimana 3 → 4 — Prod Implementations:
  F15-25 (Redis) + F15-26 (Postgres)
  Test con store concreti
```

---

## 14. Regole di Agnosticità — Checklist

Per ogni componente, verificare:

- [ ] Il componente dipende **solo da interfacce**, mai da classi concrete esterne
- [ ] Il componente non importa `StackExchange.Redis`, `Npgsql`, `MongoDB.Driver` direttamente
- [ ] I metodi pubblici non espongono tipi di framework esterno (no `IDatabase`, no `NpgsqlConnection`)
- [ ] La serializzazione JSON è dietro `IJsonSerializer` — non `JsonSerializer.Serialize` hardcoded
- [ ] Il LLM è accessibile solo via `ILLMProvider` — nessun SDK specifico nei tool
- [ ] I tool sono registrabili individualmente — non c'è accoppiamento tra tool Master e SubAgent
- [ ] Il caller può sostituire qualsiasi implementazione via DI senza ricompilare `AGXIOME.Orchestration`
- [ ] Tutti i GUID sono `System.Guid` — non string UUID di librerie terze
- [ ] I timestamp sono `DateTimeOffset` — non `DateTime` né timestamp UNIX hardcoded

---

## 15. Dipendenze Nuove Minime

```xml
<!-- AGXIOME.Orchestration.csproj — nessuna dipendenza esterna obbligatoria -->
<ItemGroup>
  <ProjectReference Include="..\AGXIOME.Shared\AGXIOME.Shared.csproj" />
  <ProjectReference Include="..\AGXIOME.Core\AGXIOME.Core.csproj" />
  <!-- Nessun NuGet aggiunto — usa solo System.* e Microsoft.Extensions.* già presenti -->
</ItemGroup>

<!-- AGXIOME.Orchestration.Redis.csproj — solo se si vuole Redis -->
<ItemGroup>
  <PackageReference Include="StackExchange.Redis" Version="2.*" />
  <ProjectReference Include="..\AGXIOME.Orchestration\AGXIOME.Orchestration.csproj" />
</ItemGroup>
```

---

## 16. Integration con Esistente — Mapping

| Componente Esistente | Ruolo in F15 |
|---------------------|-------------|
| `BackgroundRunWorker` | Implementa `IAgentSpawner` (wrapper `BackgroundWorkerAgentSpawner`) |
| `AgxiomeAgent` | È sia il Master agent che il base per i sub-agent |
| `IToolRegistry` | Registra i 6 Master tools e i 2 SubAgent tools |
| `RedisSessionStore` | Può implementare `IHotStore` via adapter |
| `PostgresCommandStore` | Può implementare `IOrchestrationStore` via adapter |
| `AgxiomeTeam` | Alternativa per spawn parallelo sub-agent (TeamMode.Tasks) |
| `ILLMProvider` | Usato da tutti i tool per chiamate LLM |
| `AgentSerializer` | Usato per serializzare/deserializzare contesto agent |
| `SignalR AgentHub` | Può implementare `ICompletionNotifier` per notifiche real-time |
| `WorkflowHistory` (SS-04) | Estendibile per tracciare avanzamento step del piano |

---

*Fine Piano F15 — Master Agent Orchestration*
