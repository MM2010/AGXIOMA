# 01 — Core Agent

## Scopo

Istanza base AGXIOME. Ciclo vita: `Create → Configure → Run → Dispose`.
Ogni agente = 1 LLMWIKY engine + tool registry + memory store + middleware pipeline.

## Contratti

```csharp
// ── Core contract ──
public interface IAgxiomeAgent : IDisposable
{
    string Id { get; }
    AgentStatus Status { get; }
    IAgentMemory Memory { get; }
    Task<AgentResponse> RunAsync(AgentRequest request, CancellationToken ct = default);
    Task<T> RunAsync<T>(AgentRequest request, CancellationToken ct = default);
    void RegisterFunction<T>() where T : AgxiomeFunction;
    void RegisterTool(IAgxiomeTool tool);
    void UseMiddleware(IAgentMiddleware middleware);

    // ── Nuovo: Hooks ──
    void AddHook(IAgentHook hook);
    void AddToolHook(IToolHook toolHook);

    // ── Nuovo: Guardrails ──
    void AddGuardrail(IGuardrail guardrail);

    // ── Nuovo: Structured output ──
    void SetOutputSchema<T>() where T : class;
    void SetInputSchema<T>() where T : class;

    // ── Nuovo: HITL ──
    void RequireConfirmationFor<T>() where T : AgxiomeFunction;

    // ── Nuovo: Learning ──
    ILearningMachine Learning { get; }
    void EnableLearning(LearningConfig? config = null);

    // ── Nuovo: Session ──
    string? SessionId { get; }
    ISessionState SessionState { get; }
}

public enum AgentStatus
{
    Idle,        // Pronto
    Thinking,    // LLMWIKY sta inferendo
    ToolCall,    // Tool in esecuzione
    Streaming,   // Risposta in streaming
    Error,       // Errore irrecuperabile
    Completed    // Eseguito con successo
}

// ── Request/Response ──
public record AgentRequest(
    string Prompt,
    AgentContext? Context = null,
    IReadOnlyList<AgentMessage>? History = null,
    AgentRequestOptions? Options = null
);

public record AgentResponse(
    string Content,
    AgentStatus Status,
    IReadOnlyList<ToolCallResult>? ToolResults,
    AgentMetadata Metadata
);

// ── Streaming chunk ──
public record AgentChunk(
    string Content,
    string? ToolCallId = null,
    bool IsFinal = false
);

public interface IAsyncAgentExecutor
{
    IAsyncEnumerable<AgentChunk> RunStreamAsync(
        AgentRequest request, CancellationToken ct);
}
```

## Tool Registry

```csharp
// ── Marker interface ──
public interface IAgxiomeTool
{
    string Name { get; }
    string Description { get; }
}

// ── Base class with auto JSON schema ──
public abstract class AgxiomeFunction : IAgxiomeTool
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract Task<FunctionResult> ExecuteAsync(CancellationToken ct);

    // Auto-generato da reflection su parametri con [Parameter] attribute
    public JsonObject ToJsonSchema();
}

// ── Parameter attribute ──
[AttributeUsage(AttributeTargets.Property)]
public class ParameterAttribute : Attribute
{
    public bool Required { get; set; }
    public string Description { get; set; } = "";
    public Type? Type { get; set; }
}
```

## Ciclo interno esecuzione (con Governance)

```
RunAsync:
  1. Validate: request non nulla, agente non in errore
  2. Enrich: inject contesto (tenant, session, auth) + history
  3. Guardrail pre: PII detection, prompt injection, moderation
  4. Pre-Hook: input validation, audit start, telemetry start
  5. Think: LLMWIKY inference → response o tool call
  6a. If tool call:
      - Pre-Tool Hook: audit, rate-limit
      - Check requires_confirmation → pause if HITL
      - Check external_execution → pause if external
      - Execute typed fn
      - Post-Tool Hook: audit, cache result
  6b. If response: format output
  7. Loop 5-6 fino a final response o max_iterations (default 20)
  8. Guardrail post: content validation
  9. Post-Hook: persist, telemetry, audit, cache update
  10. Background Hook (async): eval, learning, culture update
  11. Learning: save memory (se Always mode)
  12. Persist: save interaction + session state
  13. Return: AgentResponse
```

## Middleware Pipeline

```csharp
public interface IAgentMiddleware
{
    Task InvokeAsync(AgentContext context, Func<Task> next);
}

// Built-in middleware (order matters):
// 1. SecurityContextMiddleware   → injection auth claims
// 2. RateLimitMiddleware         → per-agent throttle
// 3. AuditMiddleware             → log ogni interazione
// 4. TelemetryMiddleware         → OpenTelemetry spans
// 5. RetryMiddleware             → auto-retry su transient error
```

## Memoria agente

```csharp
public interface IAgentMemory
{
    Task SaveInteractionAsync(AgentInteraction interaction, CancellationToken ct);
    Task<IReadOnlyList<AgentInteraction>> GetHistoryAsync(
        string agentId, int limit = 50, DateTime? since = null, CancellationToken ct = default);
    Task ClearAsync(string agentId, CancellationToken ct);

    // Short-term (Redis, volatile)
    Task<T?> GetStateAsync<T>(string key, CancellationToken ct);
    Task SetStateAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default);
}

public interface ISemanticMemory
{
    Task<IReadOnlyList<MemoryEntry>> SemanticSearchAsync(
        string agentId, string query, int topK = 10, CancellationToken ct = default);
    Task StoreEmbeddingAsync(string agentId, string text, float[] embedding, CancellationToken ct);
}
```

## Tool Injection — Per-agent

Ogni agente può ricevere tool in 3 modi:

| Metodo | Meccanismo | Quando usare |
|--------|-----------|-------------|
| **Via config** | `AgentConfig.Tools` | Pre-configurazione statica |
| **Via Register** | `agent.RegisterFunction<T>()` | Runtime programmatico |
| **Via Skill** | `agent.ApplySkill(researchSkill)` | Da skill registry remoto |

```csharp
// ── Tool injection in AgentConfig ──
public class AgentConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string? SystemPrompt { get; set; }
    public int MaxIterations { get; set; } = 20;
    public int MaxHistorySize { get; set; } = 50;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    public LLMWIKYConfig ReasoningEngine { get; set; } = new();
    public StorageConfig Persistence { get; set; } = new();
    public bool EnableStreaming { get; set; } = false;
    public bool EnableRetry { get; set; } = true;

    // ── Tool injection ──
    public List<ToolRegistration> Tools { get; set; } = new();
    public List<SkillReference> Skills { get; set; } = new();
    public string? AgentTemplate { get; set; }  // Nome template da AgentTemplateRegistry
}

public record ToolRegistration(
    string TypeName,              // Full assembly qualifier
    Dictionary<string, object>? Config = null  // Opzioni per tool
);

public record SkillReference(
    string SkillId,               // "research.web", "data.analyst"
    string? Source = null,        // "remote:https://skills.agxiome.io/v1"
    Dictionary<string, object>? Params = null
);
```

## Agent Template System

Template = configurazione predefinita per creare agent specializzati.
I template risiedono in `AgentTemplateRegistry` e possono essere locali o remoti.

```csharp
public interface IAgentTemplateRegistry
{
    AgentTemplate GetTemplate(string name);
    void RegisterTemplate(AgentTemplate template);
    IEnumerable<AgentTemplate> ListTemplates(string? category = null);
}

public record AgentTemplate(
    string Name,
    string Description,
    string Category,              // "research", "coding", "data", "chat", "custom"
    string? SystemPrompt,
    List<string> SuggestedTools,  // Tool type names
    List<string> SuggestedSkills, // Skill IDs da applicare
    LLMWIKYConfig? LLMOverride,   // Opzionale: override provider
    AgentConfig? ConfigOverrides  // Opzionale: override parametri
);

// ── Built-in templates ──
// "research"        → WebSearchTool + DocumentReader + RAGKnowledge
// "coding"          → CodeExecutionTool + FileSystemTool + GitTool + LintTool
// "data-analyst"    → DatabaseQueryTool + StatisticalAnalysisTool + ChartTool
// "general-assist"  → Basic tools + knowledge base
// "custom"          → Vuoto, solo LLM + memoria
```

```csharp
// ── Factory con template ──
public interface IAgxiomeAgentFactory
{
    // Da config esplicito
    IAgxiomeAgent CreateAgent(AgentConfig config);

    // Da nome/template
    IAgxiomeAgent CreateAgent(string name, Action<AgentConfig>? configure = null);
    
    // Da template registrato
    IAgxiomeAgent CreateFromTemplate(
        string templateName,
        Action<AgentConfig>? overrides = null);

    void ReleaseAgent(IAgxiomeAgent agent);
}
```

### Esempio: creazione agent specializzato da template

```csharp
// 1. Da template built-in con override
var researcher = factory.CreateFromTemplate("research", cfg =>
{
    cfg.Id = "researcher-01";
    cfg.ReasoningEngine.Model = "gpt-4o";
    cfg.Tools.Add(new ToolRegistration(
        "AGXIOME.Tools.WebSearchTool, AGXIOME.Tools",
        new() { ["max_results"] = 10 }
    ));
});

// 2. Da skill remota
var analyst = factory.CreateAgent("analyst-01");
analyst.ApplySkill(new SkillReference(
    "data.analyst.v2",
    Source: "remote:https://skills.agxiome.io/v1"
));

// 3. Manuale completo
var custom = factory.CreateAgent(new AgentConfig
{
    Id = "custom-agent",
    SystemPrompt = "You are a code reviewer.",
    Tools = { new ToolRegistration(
        "AGXIOME.Tools.CodeReviewTool, AGXIOME.Tools") },
    ReasoningEngine = { Provider = "Anthropic", Model = "claude-4" }
});
```

```csharp
public interface IAgxiomeAgentFactory
{
    IAgxiomeAgent CreateAgent(AgentConfig config);
    IAgxiomeAgent CreateAgent(string name, Action<AgentConfig> configure);
    void ReleaseAgent(IAgxiomeAgent agent);
}

// Implementazione: pool di agenti riutilizzabili con
// - Lazy initialization
// - Max concurrent limit
// - Graceful shutdown su cancellazione
```

## Estensioni Agno-derived

| Feature Agno | Implementazione AGXIOME | Delta |
|-------------|------------------------|-------|
| Agent.run() | `RunAsync()` + streaming | Aggiunto `IAsyncEnumerable` |
| Agent.tools | `RegisterFunction<T>()` | Typed C#, non dictionary |
| Agent.context | `AgentContext` record | Strict typing + validation |
| Agent.memory | `IAgentMemory` + `ISemanticMemory` | Separazione short/long term |
| Agent.knowledge | `IKnowledgeBase` (vedi 04) | RAG nativo su pgvector |
| Agent.description | `AgentConfig` metadata | Schema-driven |

## NFR

| Metrica | Target | Misura |
|---------|--------|--------|
| p95 latency (no tool) | < 500ms | OpenTelemetry span |
| p95 latency (1 tool call) | < 2s | OpenTelemetry span |
| p99 latency (streaming) | < 3s | First-chunk latency |
| Max iterations default | 20 | Config override |
| Max concurrent agents/host | 100 | Circuit breaker |
| Memory overhead per idle agent | < 50 KB | DotMemory |
