# P10 — Storage & Session Enhancements

> **Priority**: 🟡 P1 | **Dipende da**: P09 (Core) | **Usato da**: P08 (Runtime)
> **Target**: Complete session storage, cancellation, remote agent support

---

## Task

| # | Task | Descrizione | Effort |
|---|------|-------------|--------|
| SS-01 | **Redis Session Storage** | `RedisSessionStore` per sessioni persistenti | 1gg |
| SS-02 | **Agent Storage Bridge** | Salva/carica agent da DB (astratto per provider) | 1gg |
| SS-03 | **Team Storage** | Salva/carica team + default tools | 1gg |
| SS-04 | **Workflow History** | Cronologia esecuzioni workflow | 1gg |
| SS-05 | **Workflow Serialization** | Serializza/deserializza DAG workflow | 1gg |
| SS-06 | **Workflow HITL** | Pause per approvazione/input a livello di step | 2gg |
| SS-07 | **Contents DB** | Database per contenuti knowledge | 1gg |
| SS-08 | **Team Default Tools** | Tool automatici per team (web search, code exec) | 1gg |
| SS-09 | **Callable Members** | Risoluzione membri a runtime per utente/sessione | 1gg |
| SS-10 | **Distributed RAG Team** | Ricerca distribuita su più fonti conoscenza | 2gg |

### Pattern: Redis Session Storage

```csharp
// Session/RedisSessionStore.cs
public sealed class RedisSessionStore : ISessionStore
{
    private readonly ConnectionMultiplexer _redis;
    private const string KeyPrefix = "session:";
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(24);
    
    public async Task SaveSessionAsync(AgentSession session, CancellationToken ct)
    {
        var key = $"{KeyPrefix}{session.SessionId}";
        var json = JsonSerializer.Serialize(session);
        await _redis.GetDatabase().StringSetAsync(key, json, DefaultTtl);
    }
    
    public async Task<AgentSession?> LoadSessionAsync(string sessionId, CancellationToken ct)
    {
        var key = $"{KeyPrefix}{sessionId}";
        var json = await _redis.GetDatabase().StringGetAsync(key);
        return json.HasValue ? JsonSerializer.Deserialize<AgentSession>(json!) : null;
    }
}
```

### Pattern: Workflow History

```csharp
// Workflow history accessibile agli step successivi
public record WorkflowExecutionContext
{
    public string WorkflowId { get; init; }
    public Dictionary<string, StepOutput> StepHistory { get; init; } // step → output
    public Dictionary<string, object> SharedState { get; init; }
    
    public T GetStepOutput<T>(string stepName) => 
        (T)StepHistory[stepName].Result;
}
```

**Totale task: 10 | File: ~18 | Test: ~70**
