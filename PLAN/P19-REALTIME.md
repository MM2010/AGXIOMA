# P19 — SignalR Realtime

## File: `Hubs/AgentHub.cs`, `Hubs/NotificationHub.cs`

Namespace: `AGXIOME.Realtime`

## Hub classes

### `AgentHub : Hub<IAgentClient>`

```csharp
public class AgentHub : Hub<IAgentClient>
{
    public async Task StreamAgentRun(string agentId, AgentRequest request)
    {
        // 1. Auth check
        // 2. Join group "agent:{agentId}"
        // 3. Stream chunks via Clients.Caller.OnChunk()
        // 4. On complete → OnComplete()
        // 5. On HITL pause → OnPaused()
    }

    public async Task CancelAgentRun(string runId) { /* cancel token */ }
    public async Task JoinAgentSession(string sessionId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"session:{sessionId}");
}

public interface IAgentClient
{
    Task OnChunk(AgentChunk chunk);
    Task OnComplete(AgentResponse response);
    Task OnError(string runId, string error);
    Task OnStatusChange(string agentId, AgentStatus status);
    Task OnPaused(string runId, IReadOnlyList<HITLRequirement> requirements);
}
```

### `NotificationHub : INotificationClient`

```csharp
public class NotificationHub : Hub<INotificationClient>
{
    public async Task SubscribeToAgent(string agentId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"agent:{agentId}");
}

public interface INotificationClient
{
    Task OnNotification(NotificationEvent evt);
    Task OnWorkflowUpdate(string workflowId, WorkflowStatus status);
}
```

### `PresenceHub`

```csharp
public class PresenceHub : Hub<IPresenceClient>
{
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "presence");
        await Clients.Group("presence").OnAgentOnline(Context.UserIdentifier);
    }

    public override async Task OnDisconnectedAsync(Exception? ex)
    {
        await Clients.Group("presence").OnAgentOffline(Context.UserIdentifier);
    }
}
```

### Redis backplane

```csharp
// Microsoft.AspNetCore.SignalR.StackExchangeRedis
builder.AddSignalR().AddStackExchangeRedis(connectionString);
```

## Reconnect strategy

```csharp
// Client passa lastEventId (sequence number)
// Server: buffer ultimi 100 eventi per connessione
// Se evento disponibile → replay
// Se oltre finestra → full state sync

public class ReconnectManager
{
    private readonly ConcurrentDictionary<string, Queue<object>> _eventBuffers = new();

    public void Enqueue(string connectionId, object evt)
    {
        var queue = _eventBuffers.GetOrAdd(connectionId, _ => new(100));
        queue.Enqueue(evt);
        if (queue.Count > 100) queue.TryDequeue(out _);
    }

    public IReadOnlyList<object> GetMissedEvents(string connectionId, string lastEventId)
    {
        // Return events after lastEventId
    }
}
```

## Test

| Test | Descrizione |
|------|------------|
| Agent stream via SignalR | Chunk inviati al client |
| Group join/leave | Messaggi solo al gruppo |
| Reconnect replay | Eventi persi reinviati |
| Backpressure | Drop su coda piena |
| Redis backplane | Messaggi cross-instance |
