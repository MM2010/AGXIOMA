# P10 — Session + State

## File: `SessionManager.cs`

Namespace: `AGXIOME.Core.Session`

## Classi

### `SessionManager : ISessionManager`

```csharp
public sealed class SessionManager : ISessionManager
{
    private readonly ICommandStore _store;
    private readonly IQueryStore<Session> _query;

    public async Task<Session> CreateSessionAsync(string userId, string componentId,
        SessionType type, SessionOptions? options = null, CancellationToken ct = default)
    {
        var session = new Session(
            Id: Guid.CreateVersion7().ToString(),
            UserId: userId,
            ComponentId: componentId,
            Type: type,
            Name: options?.Name,
            State: new(),
            Metadata: options?.Metadata,
            CreatedAt: DateTime.UtcNow
        );
        await _store.SaveAsync(session, ct);
        return session;
    }

    public async Task UpdateSessionStateAsync<T>(string sessionId, T state, CancellationToken ct)
    {
        // Carica sessione → aggiorna state → salva
    }
}
```

### `SessionState : ISessionState`

```csharp
public class SessionState : ISessionState
{
    private readonly Dictionary<string, object> _state = new();

    public T? Get<T>(string key)
        => _state.TryGetValue(key, out var val) ? (T?)val : default;

    public void Set<T>(string key, T value) => _state[key] = value!;
    public bool ContainsKey(string key) => _state.ContainsKey(key);
    public Dictionary<string, object> ToDictionary() => new(_state);
}
```

### `RunContext` (Agno compat)

```csharp
public record RunContext(
    string UserId,
    string SessionId,
    ISessionState SessionState,
    Dictionary<string, object>? Dependencies = null,
    Dictionary<string, object>? Metadata = null,
    Dictionary<string, string>? KnowledgeFilters = null
);

// Iniettato automaticamente nei tool se parametro presente:
// Tool(RunContext runContext, string query) → runContext.SessionState
```

## Test

| Test | Descrizione |
|------|------------|
| Create session | Session.Id non vuoto |
| State persist | Get dopo Set |
| State load across runs | Valore mantenuto |
| RunContext injection | Context popolato |
| Session summary | Summary generato |
