# 16 — Session, State & Context Engineering

## Scopo

Gestione ciclo di vita sessione, stato mutabile condiviso, context engineering per agenti/team/workflow.
Dependency injection runtime per contestualizzare ogni run.

## Session Lifecycle

```
CreateSession(userId, agentId)
    │
    ▼
session_id ──► Run 1 ──► Run 2 ──► ... ──► Run N
                    │
              session_state (mutabile)
              chat_history (append-only)
              session_metadata
```

```csharp
public interface ISessionManager
{
    Task<Session> CreateSessionAsync(
        string userId, string componentId,
        SessionType type, SessionOptions? options = null,
        CancellationToken ct = default);

    Task<Session?> GetSessionAsync(string sessionId, CancellationToken ct);
    Task<IReadOnlyList<Session>> ListSessionsAsync(
        string userId, SessionType? type = null,
        int limit = 50, CancellationToken ct = default);

    Task UpdateSessionStateAsync<T>(
        string sessionId, T state, CancellationToken ct);
    Task<T?> GetSessionStateAsync<T>(
        string sessionId, CancellationToken ct);

    Task AddMessageAsync(string sessionId, AgentMessage msg, CancellationToken ct);
    Task<IReadOnlyList<AgentMessage>> GetHistoryAsync(
        string sessionId, int? lastN = null, CancellationToken ct = default);

    Task<string?> GetSessionSummaryAsync(string sessionId, CancellationToken ct);
    Task UpdateSessionSummaryAsync(
        string sessionId, string summary, CancellationToken ct);

    Task DeleteSessionAsync(string sessionId, CancellationToken ct);
}

public record Session(
    string Id,
    string UserId,
    string ComponentId,
    SessionType Type,
    string? Name,
    Dictionary<string, object>? State,
    Dictionary<string, string>? Metadata,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public enum SessionType { Agent, Team, Workflow }

public record SessionOptions(
    string? Name = null,
    Dictionary<string, string>? Metadata = null,
    bool EnableHistory = true,
    int MaxHistoryMessages = 100,
    bool EnableSummary = false
);
```

## Session State

```csharp
// ── Mutable state condiviso tra run ──
public interface ISessionState
{
    T? Get<T>(string key);
    void Set<T>(string key, T value);
    bool ContainsKey(string key);
    Dictionary<string, object> ToDictionary();
}

// ── Agentic state: agente modifica stato via tool ──
public class AgenticSessionState : ISessionState
{
    // L'agente ha tool built-in: state_get, state_set, state_delete
    // Le modifiche sono persistere dopo ogni run
}

// ── Uso in agente ──
public class SessionStateTool : AgxiomeToolBase
{
    public override string Name => "session_state_get";
    public override string Description => "Get a value from session state";

    [ToolParameter(Required = true)]
    public string Key { get; set; } = "";

    public override async Task<ToolResult> ExecuteAsync(ToolContext ctx, CancellationToken ct)
    {
        // ctx.SessionState.Get(Key)
    }
}
```

## Context Engineering

```csharp
public interface IAgentContext
{
    string? SystemPrompt { get; set; }
    string? IntroductionMessage { get; set; }
    List<AgentMessage>? FewShotExamples { get; set; }
    List<string> Instructions { get; }
    Dictionary<string, object> AdditionalContext { get; }

    void AddInstruction(string instruction);
    void AddInstruction(Func<Session, string> dynamicInstruction);
    void SetSystemMessage(string message);
    void SetIntroduction(string message);
    void AddFewShot(AgentMessage input, AgentMessage output);
}

public record AgentContextConfig
{
    public string? SystemPrompt { get; set; }
    public string? IntroductionMessage { get; set; }
    public List<string> Instructions { get; set; } = new();
    public List<FewShotExample>? FewShotExamples { get; set; }
    public Dictionary<string, object>? AdditionalData { get; set; }
}

public record FewShotExample(
    string Input,
    string Output,
    string? Explanation = null
);
```

### Dynamic Instructions

```csharp
// Istruzioni che usano session_state a runtime
public class DynamicInstructionProvider
{
    public string GetInstructions(Session session)
    {
        var state = session.State;
        var sb = new StringBuilder();

        if (state.TryGetValue("user_name", out var name))
            sb.AppendLine($"The user's name is {name}.");

        if (state.TryGetValue("preferred_language", out var lang))
            sb.AppendLine($"Respond in {lang}.");

        return sb.ToString();
    }
}
```

## Dependencies Injection

```csharp
public interface IDependencyProvider
{
    T GetDependency<T>() where T : class;
    void RegisterDependency<T>(T instance) where T : class;
    void RegisterDependency<T>(Func<T> factory) where T : class;
}

// ── Iniezione in agent ──
public class AgentDependencies
{
    public IUserProfileService UserProfile { get; set; }
    public ICurrentTimeService CurrentTime { get; set; }
    public IFeatureFlagService FeatureFlags { get; set; }
}

// ── Uso in tool ──
public class UserAwareTool : AgxiomeToolBase
{
    [ToolParameter(Required = true)]
    public string Query { get; set; }

    public override async Task<ToolResult> ExecuteAsync(ToolContext ctx, CancellationToken ct)
    {
        var userProfile = ctx.Dependencies.Get<IUserProfileService>();
        var time = ctx.Dependencies.Get<ICurrentTimeService>();

        // Usa profilo utente e tempo attuale
    }
}

// ── Uso in contesto ──
public class DependencyContextProvider
{
    public string BuildContext(AgentDependencies deps)
    {
        return $"Current time: {deps.CurrentTime.Now}\n" +
               $"User timezone: {deps.UserProfile.Timezone}\n" +
               $"Feature flags: {string.Join(",", deps.FeatureFlags.Enabled)}";
    }
}
```

## Chat History

```sql
-- Tabella sessioni
CREATE TABLE sessions (
    id              VARCHAR(64) PRIMARY KEY,
    user_id         VARCHAR(64) NOT NULL,
    component_id    VARCHAR(64) NOT NULL,
    type            VARCHAR(16) NOT NULL,  -- 'agent', 'team', 'workflow'
    name            VARCHAR(255),
    session_state   JSONB DEFAULT '{}',
    metadata        JSONB DEFAULT '{}',
    summary         TEXT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_sessions_user ON sessions(user_id, type);
CREATE INDEX idx_sessions_component ON sessions(component_id);

-- Tabella messaggi
CREATE TABLE session_messages (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id      VARCHAR(64) NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
    role            VARCHAR(32) NOT NULL,  -- 'user', 'assistant', 'tool', 'system'
    content         TEXT NOT NULL,
    tool_calls_json JSONB,
    metadata_json   JSONB,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_messages_session ON session_messages(session_id, created_at);
```

## NFR

| Metrica | Target |
|---------|--------|
| Session create p95 | < 10 ms |
| Session state read p95 | < 5 ms |
| Session state write p95 | < 10 ms |
| History query (50 msg) p95 | < 15 ms |
| Max history per session | 500 msg (config) |
| Session summary generation | < 2 s |
