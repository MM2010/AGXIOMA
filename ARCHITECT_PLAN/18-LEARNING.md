# 18 — Learning, Culture & Structured Output

## Scopo

Agenti che apprendono: memorie strutturate, profili utente, conoscenza cross-sessione.
Cultura: conoscenza universale condivisa tra agenti.
Output strutturato: schemi Pydantic per input/output tipizzati.

## Architettura Learning

```
Learning Machine (automatic/agentic mode)
    │
    ├── User Memory        → Osservazioni non strutturate
    ├── User Profile       → Campi strutturati (nome, preferenze)
    ├── Entity Memory      → Fatti, eventi, relazioni su entità
    ├── Session Context    → Riepilogo sessione / planning
    ├── Learned Knowledge  → Insight cross-user
    └── Decision Log       → Decisioni con ragionamento
    
Ogni store → PostgreSQL (o DB configurato)
           → Modalità: ALWAYS (automatico) | AGENTIC (agent decide) | PROPOSE (agent propone, user conferma)
```

## Learning Machine

```csharp
public interface ILearningMachine
{
    void Configure(LearningConfig config);
    
    // User Memory
    Task SaveUserMemoryAsync(string userId, string content, CancellationToken ct);
    Task<IReadOnlyList<MemoryEntry>> GetUserMemoriesAsync(
        string userId, int limit = 20, CancellationToken ct = default);
    Task OptimizeMemoriesAsync(string userId, OptimizationStrategy strategy, CancellationToken ct);

    // User Profile
    Task<UserProfile> GetOrCreateProfileAsync(string userId, CancellationToken ct);
    Task UpdateProfileAsync(string userId, UserProfile profile, CancellationToken ct);

    // Entity Memory
    Task SaveEntityFactAsync(string entityId, string fact, CancellationToken ct);
    Task SaveEntityEventAsync(string entityId, string evt, CancellationToken ct);
    Task SaveEntityRelationshipAsync(
        string sourceId, string targetId, string relationship, CancellationToken ct);
    Task<IReadOnlyList<EntityMemory>> GetEntityMemoriesAsync(
        string entityId, CancellationToken ct);

    // Session Context
    Task<SessionContext> GetSessionContextAsync(
        string sessionId, CancellationToken ct);
    Task UpdateSessionContextAsync(
        string sessionId, SessionContext context, CancellationToken ct);

    // Learned Knowledge
    Task SaveLearningAsync(string insight, IReadOnlyList<string> topics, CancellationToken ct);
    Task<IReadOnlyList<LearnedKnowledge>> SearchLearningsAsync(
        string query, int topK = 5, CancellationToken ct = default);

    // Decision Log
    Task LogDecisionAsync(DecisionLogEntry entry, CancellationToken ct);
    Task<IReadOnlyList<DecisionLogEntry>> GetDecisionLogsAsync(
        string agentId, int limit = 20, CancellationToken ct = default);
}

public class LearningConfig
{
    public LearningMode UserMemoryMode { get; set; } = LearningMode.Always;
    public LearningMode UserProfileMode { get; set; } = LearningMode.Always;
    public LearningMode EntityMemoryMode { get; set; } = LearningMode.Agentic;
    public LearningMode SessionContextMode { get; set; } = LearningMode.Summary;
    public LearningMode LearnedKnowledgeMode { get; set; } = LearningMode.Agentic;
    public LearningMode DecisionLogMode { get; set; } = LearningMode.Always;
    
    public bool EnableOptimization { get; set; } = true;
    public OptimizationStrategy DefaultOptimization { get; set; } = OptimizationStrategy.Summarize;
    public int MaxMemoriesBeforeOptimization { get; set; } = 50;
}

public enum LearningMode { Always, Agentic, Propose }
public enum LearningSessionMode { Summary, Planning }
public enum OptimizationStrategy { Summarize, KeepRecent, ArchiveOld }
```

## Memory Stores

```csharp
// ── User Memory (non-strutturata) ──
public record MemoryEntry(
    string Id,
    string UserId,
    string Content,
    IReadOnlyList<string> Topics,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

// ── User Profile (strutturata) ──
public record UserProfile(
    string UserId,
    string? Name,
    string? PreferredName,
    Dictionary<string, object> CustomFields
);

// ── Entity Memory ──
public record EntityMemory(
    string Id,
    string EntityId,
    EntityMemoryType Type,   // Fact, Event, Relationship
    string Content,
    string? RelatedEntityId,
    string? RelationshipType,
    DateTime CreatedAt
);

public enum EntityMemoryType { Fact, Event, Relationship }

// ── Session Context ──
public record SessionContext(
    string SessionId,
    LearningSessionMode Mode,
    string? Summary,
    List<Goal>? Goals,
    DateTime UpdatedAt
);

public record Goal(
    string Description,
    GoalStatus Status,
    List<string>? Steps,
    int? Progress
);

public enum GoalStatus { NotStarted, InProgress, Completed, Blocked }

// ── Learned Knowledge ──
public record LearnedKnowledge(
    string Id,
    string Content,
    float[]? Embedding,
    IReadOnlyList<string> Topics,
    DateTime CreatedAt
);

// ── Decision Log ──
public record DecisionLogEntry(
    string Id,
    string AgentId,
    string Decision,
    string Reasoning,
    Dictionary<string, object>? Context,
    bool Successful,
    DateTime CreatedAt
);
```

## Culture System

```csharp
public interface ICultureSystem
{
    Task AddCulturalKnowledgeAsync(
        CultureEntry entry, CancellationToken ct);
    Task<IReadOnlyList<CultureEntry>> SearchCultureAsync(
        string query, int topK = 5, CancellationToken ct = default);
    Task<IReadOnlyList<CultureEntry>> GetAllAsync(
        string? category = null, CancellationToken ct = default);
    Task UpdateFromInteractionAsync(
        string agentId, string interaction, CancellationToken ct);
}

public record CultureEntry(
    string Id,
    string Title,
    string Content,
    string Category,      // "principle", "best_practice", "style_guide", "domain_knowledge"
    string? Source,
    int Priority,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

// ── Automatic update ──
// Ogni N interazioni, un agente "culture manager" analizza
// le interazioni e propone nuovi cultural knowledge entries
// o aggiorna quelli esistenti
```

## Structured Output

```csharp
public interface IStructuredOutput
{
    Type OutputType { get; }
    Task<object> ParseAsync(string rawOutput, CancellationToken ct);
}

// ── Input Schema ──
public record AgentInputSchema
{
    public Type InputType { get; init; }
    public JsonObject JsonSchema { get; init; }
    public object? Validate(Dictionary<string, object> input);
}

// ── Output Schema ──
public record AgentOutputSchema
{
    public Type OutputType { get; init; }
    public JsonObject JsonSchema { get; init; }

    // LLMWIKY risponde con JSON validato contro questo schema
    // Invece di testo libero, l'output è un object tipizzato
}

// ── Parser Model ──
public interface IOutputParser
{
    // Modello separato che parse l'output dell'agente principale
    // in un formato strutturato
    Task<T> ParseAsync<T>(string agentOutput, CancellationToken ct);
}

// ── Response Model ──
public interface IResponseModel
{
    // Modello separato che genera la response finale
    // prendendo l'output strutturato e formattandolo
    // in linguaggio naturale
    Task<string> FormatResponseAsync<T>(T structuredOutput, CancellationToken ct);
}

// ── Esempio ──
// [Input: "What is the stock price of AAPL?"]
// → LLMWIKY genera JSON strutturato:
// { "symbol": "AAPL", "price": 198.50, "change": 2.3, "currency": "USD" }
// → Response Model genera: "AAPL è a $198.50, in rialzo del 2.3% oggi."
```

## Modalità Learning

| Modalità | Comportamento | Quando usare |
|----------|--------------|-------------|
| **Always** | Auto-salva dopo ogni run | Preferenze utente, profilo |
| **Agentic** | Agent decide via tool | Entity memory, learned knowledge |
| **Propose** | Agent propone, user conferma | Decisioni critiche |

## NFR

| Metrica | Target |
|---------|--------|
| Memory save p95 | < 20 ms |
| Memory retrieval (20 entries) p95 | < 15 ms |
| Profile get/create p95 | < 10 ms |
| Semantic memory search p95 | < 50 ms |
| Decision log write p95 | < 10 ms |
| Culture search p95 | < 30 ms |
| Memory optimization (50 entries) | < 1 s |
