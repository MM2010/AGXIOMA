# 02 — Agent Teams

## Scopo

Orchestrazione multi-agente. Pattern: Router, Swarm, Pipeline, Supervisor.
Ogni team = N agenti LLMWIKY che collaborano su task complesso.

## Contratti

```csharp
public enum TeamTopology
{
    Router,      // Lead agent delega a specialisti
    Swarm,       // Peer broadcast + merge
    Pipeline,    // Sequenziale: A → B → C
    Supervisor   // Genera + valida + merge
}

public enum MergeStrategy
{
    Concat,         // Unisci tutti i risultati
    Vote,           // Maggioranza vince
    Weighted,       // Pesi configurabili
    SupervisorMerge // Supervisor decide il merge
}

public interface IAgxiomeTeam
{
    string Id { get; }
    TeamTopology Topology { get; }
    IReadOnlyList<IAgxiomeAgent> Agents { get; }

    IAgxiomeAgent AddAgent(string name, string role,
        Action<AgentConfig>? configure = null);

    Task<TeamResponse> ExecuteAsync(
        string objective,
        TeamContext? context = null,
        CancellationToken ct = default);

    Task<TeamStreamChunk> ExecuteStreamAsync(
        string objective,
        TeamContext? context = null,
        CancellationToken ct = default);
}

public record TeamResponse(
    string Content,
    TeamTopology TopologyUsed,
    IReadOnlyList<AgentResult> AgentResults,
    TeamMetadata Metadata
);

public record AgentResult(
    string AgentId,
    string Role,
    AgentStatus Status,
    string? Output,
    TimeSpan ExecutionTime
);

public record TeamContext(
    string? SessionId = null,
    Dictionary<string, object>? SharedState = null,
    object? GlobalInput = null
);
```

## Pattern dettagliati

### Router

```
Objective → Lead Agent (LLMWIKY)
  ├── subtask-1 → Specialist A
  ├── subtask-2 → Specialist B
  └── subtask-3 → Specialist C
  ↓
Lead Agent synth → Final response

Quando: task multi-dominio (es. "analizza mercato + genera report + traduci")
Vantaggio: specializzazione, parallelismo parziale
Rischio: collo di bottiglia sul lead agent
```

### Swarm

```
Objective → Coordinator
  ├── Agent 1 (parallel, same prompt)
  ├── Agent 2 (parallel, same prompt)
  ├── Agent N (parallel, same prompt)
  ↓
Merge strategy → Final response

Quando: same task su dati diversi, o ensemble per robustezza
Vantaggio: parallelism massimo, fault tolerance
Rischio: merge può produrre output incoerente
```

### Pipeline

```
Input → Agent A → Agent B → Agent C → Output

Quando: trasformazioni sequenziali (es. ETL + analisi + report)
Vantaggio: chiarezza, ogni step ha input/output definito
Rischio: latenza = somma step, un fallimento blocca tutto
```

### Supervisor

```
Objective → Generator Agent(s)
              ↓ output
         Supervisor Agent
              ↓ validate + merge
         Final response

Quando: quality gate richiesto (produzione, compliance)
Vantaggio: qualità controllata, rigetto automatico
Rischio: costo 2x (generator + supervisor)
```

## Esecuzione interna team

```
ExecuteAsync(Router pattern):
  1. Parse objective via lead agent → N subtask
  2. Fan-out: assegna subtask a specialisti (Task.WhenAll)
  3. Collect risultati parziali
  4. Lead agent synth: contesto unificato
  5. Generate final response
  6. Return TeamResponse con metadata

ExecuteAsync(Swarm pattern):
  1. Broadcast objective a tutti gli agenti
  2. Task.WhenAll con timeout individuale
  3. MergeStrategy applicata
  4. Return

ExecuteAsync(Pipeline pattern):
  1. Esegui Agent A con input
  2. Output A → Input B
  3. Output B → Input C
  4. Return

ExecuteAsync(Supervisor pattern):
  1. Genera N risposte candidate (in parallelo o serie)
  2. Supervisor valida ogni candidata
  3. Se tutte rifiutate → regenerate con feedback
  4. Se accettata → mergia/seleziona
```

## Error handling

| Scenario | Comportamento |
|----------|--------------|
| Un agente fallisce (Router) | Lead agent redistribuisce o procede parziale |
| Un agente fallisce (Swarm) | Merge sugli N-1 agenti riusciti |
| Un agente fallisce (Pipeline) | Pipeline fallisce, retry step |
| Timeout globale | Return partial results con warning |
| Supervisor rifiuta tutto | Max 3 regenerate, poi errore |
| Dead agent | Replace con fallback agent from pool |

## Sicurezza

- Ogni agente eredita contesto di sicurezza del team (tenant, auth)
- Tool isolation: agenti specializzati hanno solo tool necessari
- Audit trail: ogni subtask loggato con correlationId parent
- Rate limit: per-team, non per-agent (evita DoS su Swarm)

## Team Factory

```csharp
public interface IAgxiomeTeamFactory
{
    IAgxiomeTeam CreateTeam(string name, TeamTopology topology,
        Action<TeamConfig> configure);
}

public class TeamConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public TeamTopology Topology { get; set; } = TeamTopology.Router;
    public MergeStrategy Merge { get; set; } = MergeStrategy.Concat;
    public int MaxRetriesPerAgent { get; set; } = 3;
    public TimeSpan AgentTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan GlobalTimeout { get; set; } = TimeSpan.FromSeconds(120);
    public bool ContinueOnPartialFailure { get; set; } = true;
}
```

## Esempio d'uso

```csharp
var team = factory.CreateTeam("research-team", TeamTopology.Router);

var researcher = team.AddAgent("researcher", role: "Data Retrieval");
var analyst    = team.AddAgent("analyst",    role: "Pattern Analysis");
var writer     = team.AddAgent("writer",     role: "Report Generation");

researcher.RegisterFunction<WebSearchTool>();
researcher.RegisterFunction<DatabaseQueryTool>();
analyst.RegisterFunction<StatisticalAnalysisTool>();
writer.RegisterFunction<MarkdownFormatterTool>();

var result = await team.ExecuteAsync(
    "Analyze Q4 earnings trends and generate executive summary.",
    new TeamContext { SessionId = sessionId }
);

Console.WriteLine(result.Content);
```

## Confronto Agno → AGXIOME

| Feature Agno | AGXIOME | Delta |
|-------------|---------|-------|
| `Team` | `IAgxiomeTeam` | Interfacce + pattern enum |
| `Run()` | `ExecuteAsync()` | Async first + streaming |
| `add_task()` | `AddAgent()` | Role-based, non positional |
| Merge implicito | `MergeStrategy` esplicito | Configurabile |
| Error handling base | Retry + timeout + partial | Enterprise grade |
