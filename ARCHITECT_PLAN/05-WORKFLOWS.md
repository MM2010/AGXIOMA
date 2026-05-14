# 05 — Workflows

## Scopo

Esecuzione DAG di step. Ogni step = agente, tool, condizione o trasformazione.
Supporto retry, timeout, branching, parallel execution, human-in-the-loop, scheduling cron.

## Scheduling & Background Execution

```csharp
public interface IWorkflowScheduler
{
    Task<Schedule> CreateScheduleAsync(
        string name, string workflowId,
        string cronExpression,
        SchedulePayload? payload = null,
        CancellationToken ct = default);

    Task<Schedule> CreateScheduleAsync(
        string name, string agentId,
        string cronExpression,
        AgentRequest? request = null,
        CancellationToken ct = default);

    Task UpdateScheduleAsync(string scheduleId, string? cronExpression = null,
        bool? enabled = null, CancellationToken ct = default);

    Task DeleteScheduleAsync(string scheduleId, CancellationToken ct);
    Task<IReadOnlyList<Schedule>> ListSchedulesAsync(CancellationToken ct);
    Task TriggerAsync(string scheduleId, CancellationToken ct);
    Task<IReadOnlyList<ScheduleRun>> GetRunHistoryAsync(
        string scheduleId, int limit = 20, CancellationToken ct = default);
}

public record Schedule(
    string Id,
    string Name,
    string WorkflowId,
    string CronExpression,
    SchedulePayload? Payload,
    ScheduleStatus Status,
    DateTime CreatedAt,
    DateTime? LastRunAt,
    DateTime? NextRunAt
);

public record SchedulePayload(
    Dictionary<string, object>? Input = null,
    string? SessionId = null
);

public enum ScheduleStatus { Active, Paused, Completed, Failed }

public record ScheduleRun(
    string ScheduleId,
    string RunId,
    ScheduleRunStatus Status,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string? ErrorMessage = null
);

public enum ScheduleRunStatus { Running, Completed, Failed, Cancelled }
```

## Workflow Session State (Cron + Persistence)

```csharp
// I workflow schedulati hanno session state persistente
// Ogni run può accedere/modificare stato della run precedente
// Esempio: counter incrementale, accumulo dati, watermark

public class ScheduledWorkflowState
{
    public int RunCount { get; set; }
    public DateTime? LastSuccessfulRun { get; set; }
    public object? LastOutput { get; set; }
    public Dictionary<string, object> State { get; set; } = new();
}
```

## Contratti

```csharp
public interface IAgxiomeWorkflow
{
    string Id { get; }
    WorkflowStatus Status { get; }
    IWorkflowStep AddStep(string name, Func<WorkflowContext, Task> handler);
    IWorkflowStep AddAgentStep(string name, IAgxiomeAgent agent);
    IWorkflowStep AddToolStep<T>(string name) where T : AgxiomeFunction;
    void AddTransition(string from, string to,
        Func<WorkflowContext, bool>? condition = null);
    Task<WorkflowResult> ExecuteAsync(
        WorkflowContext context, CancellationToken ct = default);
}

public enum WorkflowStatus
{
    Created, Running, Paused, Completed, Failed, Cancelled
}

public record WorkflowResult(
    string WorkflowId,
    WorkflowStatus Status,
    IReadOnlyList<StepResult> StepResults,
    WorkflowMetadata Metadata
);

public record StepResult(
    string StepName,
    StepStatus Status,
    object? Output,
    TimeSpan ExecutionTime,
    string? ErrorMessage = null
);

public enum StepStatus { Pending, Running, Completed, Skipped, Failed }

public record WorkflowContext(
    string? SessionId = null,
    Dictionary<string, object> SharedState = null,
    CancellationToken CancellationToken = default
);
```

## Step types

| Type | Classe | Descrizione |
|------|--------|-------------|
| **AgentStep** | `AgentWorkflowStep` | Esegue un agente LLMWIKY |
| **ToolStep** | `ToolWorkflowStep` | Esegue function C# pura |
| **ConditionStep** | `ConditionWorkflowStep` | Branch logico (if/else) |
| **ParallelStep** | `ParallelWorkflowStep` | Fan-out N step, wait all |
| **TransformStep** | `TransformWorkflowStep` | Trasforma dati tra step |
| **HumanStep** | `HumanWorkflowStep` | Pausa per approvazione umana |
| **DelayStep** | `DelayWorkflowStep` | Attesa deliberata (rate-limit) |
| **LoopStep** | `LoopWorkflowStep` | Iterazione su array/collection |

## DAG execution engine

```
ExecuteAsync:
  1. Validate DAG: no cicli, start node esiste
  2. Topological sort: BFS
  3. Initialize context with shared state
  4. Execute nodi in ordine:
     a. Se condition false → skip con dipendenti
     b. Se parallel → Task.WhenAll
     c. Se human → await approval signal
     d. Se errore → retry logic o fail
  5. Persist: salva stato workflow
  6. Return WorkflowResult
```

## Retry & error policy

```csharp
public class StepRetryPolicy
{
    public int MaxRetries { get; set; } = 3;
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromMilliseconds(200);
    public bool ExponentialBackoff { get; set; } = true;
    public Func<Exception, bool>? RetryOnException { get; set; }
    // Default: retry su transient (timeout, network, 503)
}
```

| Scenario | Comportamento |
|----------|--------------|
| Step fallisce (transient) | Retry con backoff |
| Step fallisce (permanente) | Workflow fails, rollback opzionale |
| Timeout step | Retry 1x, poi fail |
| Parallel step fallisce | Altri step completano, report partial |
| Human step scade | Skip con warning |

## Human-in-the-loop

```csharp
public interface IHumanStepHandler
{
    Task<HumanApproval> RequestApprovalAsync(
        string workflowId, string stepName,
        object payload, TimeSpan timeout, CancellationToken ct);
    Task SubmitApprovalAsync(string workflowId, bool approved, string? notes = null);
}

public record HumanApproval(
    bool Approved,
    string? Notes = null,
    DateTime RespondedAt = default
);
```

- Timeout default: 24h
- Notifica via SignalR/email
- Payload: contesto + azione richiesta + opzioni

## Workflow persistence

```sql
CREATE TABLE workflows (
    id              VARCHAR(64) PRIMARY KEY,
    name            VARCHAR(255) NOT NULL,
    status          VARCHAR(32) NOT NULL DEFAULT 'created',
    dag_json        JSONB NOT NULL,
    context_json    JSONB NOT NULL DEFAULT '{}',
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ,
    completed_at    TIMESTAMPTZ
);

CREATE TABLE workflow_steps (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workflow_id     VARCHAR(64) NOT NULL REFERENCES workflows(id),
    name            VARCHAR(255) NOT NULL,
    step_type       VARCHAR(64) NOT NULL,
    status          VARCHAR(32) NOT NULL DEFAULT 'pending',
    input_json      JSONB,
    output_json     JSONB,
    error_message   TEXT,
    attempt         INTEGER NOT NULL DEFAULT 0,
    started_at      TIMESTAMPTZ,
    completed_at    TIMESTAMPTZ
);
CREATE INDEX idx_workflow_steps_wf ON workflow_steps(workflow_id, name);
```

## Esempio d'uso

```csharp
var wf = workflowFactory.Create("etl-report");

// Definisci step
var extract   = wf.AddAgentStep("extract", extractAgent);
var transform = wf.AddToolStep<DataTransformerTool>("transform");
var analyze   = wf.AddAgentStep("analyze", analyzeAgent);
var validate  = wf.AddAgentStep("validate", validatorAgent);
var notify    = wf.AddToolStep<EmailNotifierTool>("notify");
var archive   = wf.AddToolStep<ArchiveTool>("archive");

// Definisci transizioni (DAG)
wf.AddTransition("extract", "transform");
wf.AddTransition("transform", "analyze");
wf.AddTransition("analyze", "validate");
wf.AddTransition("validate", "notify", ctx =>
    ctx.Get<bool>("validationPassed"));
wf.AddTransition("validate", "archive", ctx =>
    !ctx.Get<bool>("validationPassed"));
wf.AddTransition("notify", "archive");

// Esegui
var result = await wf.ExecuteAsync(new WorkflowContext
{
    SharedState = new Dictionary<string, object>
    {
        ["source"] = "s3://data/q4-2025.parquet"
    }
});
```

## Confronto Agno → AGXIOME

| Feature Agno | AGXIOME | Delta |
|-------------|---------|-------|
| `Workflow` | `IAgxiomeWorkflow` | Interfacce formali |
| Sequential steps | DAG con transizioni | Branching + parallel |
| `add_step()` | `AddStep() / AddAgentStep() / AddToolStep()` | Typed step types |
| Retry implicito | `StepRetryPolicy` configurabile | Backoff + condition |
| Nessun human step | `IHumanStepHandler` | Human-in-the-loop |
| Nessuna persistenza | Workflow state su PostgreSQL | Riprendibile dopo crash |
