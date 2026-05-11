# P06 — Team Orchestration

## File: `AgxiomeTeam.cs`

Namespace: `AGXIOME.Core.Teams`

## Classi

### `AgxiomeTeam : IAgxiomeTeam`

```csharp
public sealed class AgxiomeTeam : IAgxiomeTeam
{
    public string Id { get; }
    public TeamTopology Topology { get; }
    public TeamMode Mode { get; }
    public IReadOnlyList<IAgxiomeAgent> Agents => _members;

    private readonly IAgxiomeAgent _leader;
    private readonly List<IAgxiomeAgent> _members;
    private readonly TeamConfig _config;

    // Costruttore
    public AgxiomeTeam(string name, TeamConfig config)
    {
        Id = name;
        _config = config;
        _leader = config.LeaderFactory();
        _members = new();
    }

    public IAgxiomeAgent AddAgent(string name, string role, Action<AgentConfig>? configure = null)
    {
        var cfg = new AgentConfig { Id = name };
        configure?.Invoke(cfg);
        var agent = AgentFactory.Create(cfg);
        agent.Config.Role = role; // Role per delegation
        _members.Add(agent);
        return agent;
    }

    public async Task<TeamResponse> ExecuteAsync(
        string objective, TeamContext? context = null, CancellationToken ct = default)
    {
        // 1. Leader parse objective → subtask list
        // 2. Assegna subtask ai membri (mode-coordinate) o route diretto
        // 3. Task.WhenAll con timeout
        // 4. Leader synth risultati
        // 5. Return TeamResponse
    }
}
```

### `TeamConfig`

```csharp
public record TeamConfig
{
    public string Name { get; init; }
    public TeamMode Mode { get; init; } = TeamMode.Coordinate;
    public Func<IAgxiomeAgent> LeaderFactory { get; init; }
    public int MaxIterations { get; init; } = 20;
    public TimeSpan AgentTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan GlobalTimeout { get; init; } = TimeSpan.FromSeconds(120);
    public bool ContinueOnPartialFailure { get; init; } = true;
    public int MaxRetriesPerAgent { get; init; } = 3;
    public bool CacheCallables { get; init; } = true;
}
```

### Team Modes (Agno compat)

```csharp
public enum TeamMode
{
    Coordinate,   // Default: leader decompose, delegate, synth
    Route,        // Route to single specialist, return directly
    Broadcast,    // Same task to all members, synth
    Tasks         // Iterative task loop
}
```

### Callable Members Factory (Agno compat)

```csharp
// members può essere:
// - List<IAgxiomeAgent>
// - Func<TeamContext, List<IAgxiomeAgent>>
// La factory è chiamata per ogni run, cacheable
public class CallableMemberFactory { /* cache per user_id/session_id */ }
```

### Model Inheritance (Agno compat)

```csharp
// Se un member non ha model esplicito, eredita dal team:
// member.Config.ReasoningEngine ??= team.Config.LeaderConfig.ReasoningEngine
```

## Team Response

```csharp
public record TeamResponse(
    string Content,
    TeamMode ModeUsed,
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
```

## Nested Teams

Un team può contenere altri team come membri. Il leader del team padre delega al leader del sub-team.

## Test

| Test | Cosa verifica |
|------|--------------|
| Coordinate mode | Subtask distribuiti, synth finale |
| Route mode | Single specialist response |
| Broadcast mode | Tutti i membri chiamati |
| Tasks mode | Loop fino a max_iterations |
| Member failure (partial) | Risultato parziale |
| Nested team execution | Sub-team esegue |
| Model inheritance | Member senza model usa team model |
