# Realtime — AGXIOME

SignalR hubs per streaming agent, notifiche, e presenza.
Redis backplane per scale-out multi-instanza.

---

## Hub disponibili

| Hub | Client interface | Scopo |
|-----|-----------------|-------|
| `AgentHub` | `IAgentClient` | Streaming risposte agente, HITL pause |
| `NotificationHub` | `INotificationClient` | Notifiche eventi, aggiornamenti workflow |
| `PresenceHub` | `IPresenceClient` | Online/offline agenti |

## AgentHub

```csharp
public interface IAgentClient
{
    Task OnChunk(AgentChunk chunk);
    Task OnComplete(AgentResponse response);
    Task OnError(string runId, string error);
    Task OnStatusChange(string agentId, AgentStatus status);
    Task OnPaused(string runId, IReadOnlyList<HITLRequirement> requirements);
}

// Client:
await connection.InvokeAsync("StreamAgentRun", "agent-1", request);
connection.On<AgentChunk>("OnChunk", chunk => Console.Write(chunk.Content));
```

## NotificationHub

```csharp
public interface INotificationClient
{
    Task OnNotification(NotifyEvent evt);
    Task OnWorkflowUpdate(string workflowId, WorkflowStatus status);
}

await connection.InvokeAsync("SubscribeToAgent", "agent-1");
connection.On<NotifyEvent>("OnNotification", evt => Console.WriteLine(evt.Type));
```

## Redis Backplane

```csharp
builder.AddSignalR()
    .AddStackExchangeRedis("localhost:6379");
```

## Group strategy

| Gruppo | Contenuto |
|--------|-----------|
| `agent:{id}` | Stream dedicato per agente |
| `session:{id}` | Tutti i messaggi di una sessione |
| `workflow:{id}` | Aggiornamenti workflow |
| `presence` | Presenza globale (admin) |

## Reconnect

```
Disconnect → Backoff (1s, 2s, 5s, 10s, max 30s)
     ↓
Reconnect → Cursor replay (last N events)
     ↓
Resume stream
```

Ogni connessione ha un buffer degli ultimi 100 eventi per reconnect without state reload.
