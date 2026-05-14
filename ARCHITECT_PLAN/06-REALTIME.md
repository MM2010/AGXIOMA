# 06 — Realtime (SignalR)

## Scopo

Comunicazione bidirezionale in tempo reale: streaming risposte agenti, notifiche eventi, presenza, aggiornamenti stato workflow.

## Architettura SignalR

```
Client (Blazor / JS / Mobile)
    │  wss://
    ▼
┌──────────────────────┐
│   SignalR Gateway    │  ← Auth, Rate-limit, Connection validation
└─────────┬────────────┘
          │
          ▼
┌──────────────────────┐
│   SignalR Hubs       │
│  ┌────────────────┐  │
│  │ AgentHub       │  │  → streaming agent response
│  ├────────────────┤  │
│  │ NotificationHub│  │  → eventi push (workflow, system)
│  ├────────────────┤  │
│  │ PresenceHub    │  │  → online/offline agenti
│  ├────────────────┤  │
│  │ TeamHub        │  │  → team execution progress
│  └────────────────┘  │
└─────────┬────────────┘
          │
          ▼
┌──────────────────────┐
│   Redis Backplane    │  ← Scale-out: multi-instance fan-out
└──────────────────────┘
```

## Hub definizioni

```csharp
// ── Agent streaming ──
public class AgentHub : Hub<IAgentClient>
{
    public async Task StreamAgentRun(string agentId, AgentRequest request);
    public async Task CancelAgentRun(string runId);
    public async Task JoinAgentSession(string sessionId);
    public async Task LeaveAgentSession(string sessionId);
}

public interface IAgentClient
{
    Task OnChunk(AgentChunk chunk);
    Task OnComplete(AgentResponse response);
    Task OnError(string runId, string error);
    Task OnStatusChange(string agentId, AgentStatus status);
}

// ── Notifications ──
public class NotificationHub : Hub<INotificationClient>
{
    public async Task SubscribeToAgent(string agentId);
    public async Task UnsubscribeFromAgent(string agentId);
}

public interface INotificationClient
{
    Task OnNotification(NotificationEvent evt);
    Task OnWorkflowUpdate(string workflowId, WorkflowStatus status);
    Task OnTeamUpdate(string teamId, TeamProgress progress);
}

// ── Presence ──
public class PresenceHub : Hub<IPresenceClient>
{
    public async Task SubscribeToAgentPresence(string agentId);
}

public interface IPresenceClient
{
    Task OnAgentOnline(string agentId);
    Task OnAgentOffline(string agentId);
    Task OnAgentBusy(string agentId);
}
```

## Group strategy

| Hub | Group pattern | Scope |
|-----|--------------|-------|
| AgentHub | `agent:{id}` | Per-agente |
| AgentHub | `session:{id}` | Per-sessione utente |
| NotificationHub | `user:{id}` | Per-utente |
| NotificationHub | `workflow:{id}` | Per-workflow |
| NotificationHub | `team:{id}` | Per-team |
| PresenceHub | `presence` | Globale (solo admin) |

## Reconnect strategy

```
Disconnect → Backoff progressivo (1s, 2s, 5s, 10s, max 30s)
                  │
                  ▼
         ┌──────────────────┐
         │  Reconnect       │
         │  (Negotiate)     │
         └────────┬─────────┘
                  │
                  ▼
         ┌──────────────────┐
         │  Cursor replay   │
         │  (lastEventId)   │
         │  window: 100 msgs│
         └────────┬─────────┘
                  │
                  ▼
         ┌──────────────────┐
         │  Resume stream   │
         │  from cursor     │
         └──────────────────┘
```

- `lastEventId` passato in reconnect → server invia eventi persi
- Finestra replay: ultimi 100 eventi per connessione
- Oltre finestra: full state sync via REST

## Backpressure model

```csharp
public class HubBackpressureOptions
{
    public int MaxConcurrentConnections { get; set; } = 10_000;
    public int MaxInboundMessageSize { get; set; } = 128 * 1024; // 128KB
    public int MaxOutboundMessageSize { get; set; } = 256 * 1024; // 256KB
    public int QueueLimitPerConnection { get; set; } = 100;
    public DropPolicy OverflowPolicy { get; set; } = DropPolicy.DropOldest;
    public int MaxInvokeCountPerSecond { get; set; } = 50;
}

public enum DropPolicy
{
    DropNewest,   // Scarta messaggio più recente
    DropOldest,   // Scarta il più vecchio in coda
    Reject        // Rifiuta nuova connessione
}
```

## Redis backplane

- `Microsoft.AspNetCore.SignalR.StackExchangeRedis`
- Multi-instance fan-out: messaggio pubblicato su Redis → tutte le istanze
- Fallback: disconnessione parziale non blocca le altre istanze
- Health check: ping periodico sullo stream Redis

## Message ordering

| Scenario | Garanzia |
|----------|----------|
| Stessa connessione | FIFO garantito (SignalR nativo) |
| Stesso hub, istanze diverse | Non garantito (eventuale use sequence number) |
| Reconnect con cursore | Ordinamento per sequence number lato client |
| Agent streaming chunk | Ordine per `chunkIndex` con validazione skip |

## Sicurezza realtime

- Autenticazione JWT su connessione
- `[Authorize]` su hub e metodi
- Rate-limit per-connessione (50 invocazioni/sec)
- Validazione input: taglia massima messaggio, niente HTML
- Disconnessione forzata su violazione policy

## NFR

| Metrica | Target |
|---------|--------|
| Connection establishment | < 200 ms |
| Message delivery p95 (same instance) | < 50 ms |
| Message delivery p95 (cross-instance) | < 150 ms |
| Reconnect p95 | < 500 ms |
| Drop reconnected | < 1% |
| Max concurrent connections | 10K per instance |
