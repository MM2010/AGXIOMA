# P12 — Learning Machine

## File: `LearningMachine.cs`

Namespace: `AGXIOME.Core.Learning`

## Classi

### `LearningMachine : ILearningMachine`

```csharp
public sealed class LearningMachine : ILearningMachine
{
    private readonly LearningConfig _config;
    private readonly ILearningStore _store;

    // Modalità
    public LearningMode UserMemoryMode => _config.UserMemoryMode;
    public LearningMode UserProfileMode => _config.UserProfileMode;
    public LearningMode EntityMemoryMode => _config.EntityMemoryMode;
    public LearningMode SessionContextMode => _config.SessionContextMode;

    public async Task ProcessAsync(string agentId, string input, string output, CancellationToken ct)
    {
        // Always mode: auto-save dopo ogni run
        if (UserMemoryMode == LearningMode.Always)
            await ExtractUserMemoryAsync(input, output, ct);

        if (UserProfileMode == LearningMode.Always)
            await ExtractUserProfileAsync(input, output, ct);

        // Agentic mode: agent decide via tool → non qui, nei tool hook
    }

    private async Task ExtractUserMemoryAsync(string input, string output, CancellationToken ct)
    {
        // LLMWIKY estrae facts dalla conversazione
        var memories = await ExtractFactsAsync(input, output, ct);
        foreach (var mem in memories)
            await _store.SaveUserMemoryAsync("user", mem, ct);
    }
}
```

### Learning Config

```csharp
public record LearningConfig
{
    public LearningMode UserMemoryMode { get; init; } = LearningMode.Always;
    public LearningMode UserProfileMode { get; init; } = LearningMode.Always;
    public LearningMode EntityMemoryMode { get; init; } = LearningMode.Agentic;
    public LearningMode SessionContextMode { get; init; } = LearningMode.Summary;
    public LearningMode LearnedKnowledgeMode { get; init; } = LearningMode.Agentic;
    public LearningMode DecisionLogMode { get; init; } = LearningMode.Always;
    public bool EnableOptimization { get; init; } = true;
    public int MaxMemoriesBeforeOptimization { get; init; } = 50;
}

public enum LearningMode { Always, Agentic, Propose }
public enum LearningSessionMode { Summary, Planning }
```

### Learning Stores

```csharp
public interface ILearningStore
{
    // User Memory
    Task SaveUserMemoryAsync(string userId, string content, CancellationToken ct);
    Task<IReadOnlyList<MemoryEntry>> GetUserMemoriesAsync(string userId, int limit, CancellationToken ct);

    // User Profile
    Task<UserProfile> GetOrCreateProfileAsync(string userId, CancellationToken ct);
    Task UpdateProfileAsync(string userId, UserProfile profile, CancellationToken ct);

    // Entity Memory
    Task SaveEntityFactAsync(string entityId, string fact, CancellationToken ct);
    Task<IReadOnlyList<EntityMemory>> GetEntityMemoriesAsync(string entityId, CancellationToken ct);

    // Learned Knowledge
    Task SaveLearningAsync(string insight, IReadOnlyList<string> topics, CancellationToken ct);
    Task<IReadOnlyList<LearnedKnowledge>> SearchLearningsAsync(string query, int topK, CancellationToken ct);

    // Decision Log
    Task LogDecisionAsync(DecisionLogEntry entry, CancellationToken ct);
}

// Implementazione: PostgresLearningStore (tabelle dedicate in PostgreSQL)
```

### Memory Models

```csharp
public record MemoryEntry(string Id, string UserId, string Content, IReadOnlyList<string> Topics, DateTime CreatedAt);
public record UserProfile(string UserId, string? Name, string? PreferredName, Dictionary<string, object> CustomFields);
public record EntityMemory(string Id, string EntityId, EntityMemoryType Type, string Content, DateTime CreatedAt);
public enum EntityMemoryType { Fact, Event, Relationship }
public record LearnedKnowledge(string Id, string Content, IReadOnlyList<string> Topics, DateTime CreatedAt);
public record DecisionLogEntry(string Id, string AgentId, string Decision, string Reasoning, bool Successful, DateTime CreatedAt);
```

## Test

| Test | Descrizione |
|------|------------|
| Always mode saves memory | Memory salvata dopo run |
| Agentic mode needs tool | Memory non salvata automaticamente |
| User profile extraction | Profile campi popolati |
| Entity memory retrieval | Facts retrieved |
| Memory optimization | Memoried merged |
