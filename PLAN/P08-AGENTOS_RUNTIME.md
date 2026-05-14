# P08 — AgentOS Runtime Server

> **Priority**: 🔴 P0 | **Dipende da**: P09 (Core), P10 (Storage), P11 (Tools)
> **Target**: Runtime server ASP.NET Core con 50+ endpoint (Agno ha FastAPI con ~50 endpoint)

---

## Nuovo Progetto

```
AGXIOME.Runtime/       ← ASP.NET Core 10 Web API
├── Program.cs         ← Middleware, DI, routing
├── appsettings.json   ← Config
├── Controllers/
│   ├── AgentController.cs
│   ├── SessionController.cs
│   ├── RunController.cs
│   ├── TeamController.cs
│   ├── WorkflowController.cs
│   ├── KnowledgeController.cs
│   ├── ToolController.cs
│   ├── ToolRegistryController.cs
│   ├── StorageController.cs
│   ├── HealthController.cs
│   ├── AuthController.cs
│   ├── LogController.cs
│   ├── AdminController.cs
│   └── MCPBridgeController.cs
├── Middleware/
│   ├── JwtMiddleware.cs
│   ├── RbacMiddleware.cs
│   ├── RequestLogger.cs
│   ├── ErrorHandler.cs
│   ├── RateLimiter.cs
│   └── RequestValidator.cs
├── Hubs/
│   ├── AgentHub.cs (SignalR)      ← stream run events
│   ├── LogHub.cs (SignalR)        ← log streaming
│   └── AdminHub.cs (SignalR)      ← admin monitoring
├── Jobs/
│   ├── BackgroundRunWorker.cs     ← esegue run in background
│   ├── SessionCleanupJob.cs       ← pulizia sessioni expired
│   └── TokenRotationJob.cs        ← rotazione chiavi JWT
└── DTOs/
    ├── AgentRequest.cs
    ├── AgentResponse.cs
    ├── RunRequest.cs
    ├── RunResponse.cs
    └── ...
```

---

## Endpoint API

### Agent (6)

| Method | Path | Descrizione |
|--------|------|-------------|
| POST | `/api/v1/agents` | Crea nuovo agent |
| GET | `/api/v1/agents/{id}` | Get agent config |
| PUT | `/api/v1/agents/{id}` | Update agent |
| DELETE | `/api/v1/agents/{id}` | Delete agent |
| GET | `/api/v1/agents` | List agents con filtri |
| POST | `/api/v1/agents/{id}/clone` | Clona agent |

### Run (8)

| Method | Path | Descrizione |
|--------|------|-------------|
| POST | `/api/v1/agents/{id}/run` | Avvia run (sync) |
| POST | `/api/v1/agents/{id}/run/stream` | Avvia run (SSE stream) |
| POST | `/api/v1/agents/{id}/run/async` | Avvia run (background) |
| GET | `/api/v1/runs/{runId}` | Get run status/result |
| POST | `/api/v1/runs/{runId}/cancel` | Cancella run |
| GET | `/api/v1/runs/{runId}/events` | Get event history |
| GET | `/api/v1/runs/{runId}/events/stream` | SSE event stream |
| POST | `/api/v1/runs/{runId}/resume` | Resume per HITL |

### Session (5)

| Method | Path | Descrizione |
|--------|------|-------------|
| POST | `/api/v1/sessions` | Crea sessione |
| GET | `/api/v1/sessions/{id}` | Get sessione |
| PUT | `/api/v1/sessions/{id}` | Update sessione |
| DELETE | `/api/v1/sessions/{id}` | Delete sessione |
| GET | `/api/v1/sessions` | List sessioni |

### Team (6)

| Method | Path | Descrizione |
|--------|------|-------------|
| POST | `/api/v1/teams` | Crea team |
| GET | `/api/v1/teams/{id}` | Get team |
| PUT | `/api/v1/teams/{id}` | Update team |
| DELETE | `/api/v1/teams/{id}` | Delete team |
| POST | `/api/v1/teams/{id}/coordinate` | Coordina team |
| POST | `/api/v1/teams/{id}/broadcast` | Broadcast a tutti |

### Workflow (6)

| Method | Path | Descrizione |
|--------|------|-------------|
| POST | `/api/v1/workflows` | Crea workflow |
| GET | `/api/v1/workflows/{id}` | Get workflow DAG |
| PUT | `/api/v1/workflows/{id}` | Update workflow |
| DELETE | `/api/v1/workflows/{id}` | Delete workflow |
| POST | `/api/v1/workflows/{id}/execute` | Esegui workflow |
| GET | `/api/v1/workflows/{id}/history` | Cronologia esecuzioni |

### Knowledge (6)

| Method | Path | Descrizione |
|--------|------|-------------|
| POST | `/api/v1/knowledge/ingest` | Ingest documenti |
| POST | `/api/v1/knowledge/search` | Ricerca vettoriale |
| DELETE | `/api/v1/knowledge/collections/{name}` | Elimina collezione |
| GET | `/api/v1/knowledge/collections` | Lista collezioni |
| POST | `/api/v1/knowledge/query` | Query RAG |
| POST | `/api/v1/knowledge/rerank` | Rerank risultati |

### Tool (4)

| Method | Path | Descrizione |
|--------|------|-------------|
| GET | `/api/v1/tools` | Lista tools disponibili |
| GET | `/api/v1/tools/{name}` | Get tool schema |
| POST | `/api/v1/tools/register` | Registra tool custom |
| GET | `/api/v1/toolkits` | Lista toolkit |

### Auth (5)

| Method | Path | Descrizione |
|--------|------|-------------|
| POST | `/api/v1/auth/login` | Login |
| POST | `/api/v1/auth/refresh` | Refresh token |
| POST | `/api/v1/auth/logout` | Logout |
| GET | `/api/v1/auth/me` | Current user info |
| POST | `/api/v1/auth/apikey` | Genera API key |

### Admin (6)

| Method | Path | Descrizione |
|--------|------|-------------|
| GET | `/api/v1/admin/health` | Health check |
| GET | `/api/v1/admin/metrics` | Metriche |
| GET | `/api/v1/admin/logs` | Log stream |
| GET | `/api/v1/admin/errors` | Errori recenti |
| GET | `/api/v1/admin/config` | Config corrente |
| POST | `/api/v1/admin/config` | Update config runtime |

---

## Middleware Stack

```csharp
// Program.cs
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeyResolver = KeyRotationResolver.Resolve
        };
    });

builder.Services.AddAuthorization(options => {
    options.AddPolicy("admin", p => p.RequireRole("admin"));
    options.AddPolicy("agent:run", p => p.RequireClaim("permission", "agent:run"));
});

// Middleware pipeline:
app.UseMiddleware<RequestLogger>();      // logging pseudonimizzato
app.UseMiddleware<RateLimiter>();        // rate limiting
app.UseMiddleware<JwtMiddleware>();      // auth
app.UseMiddleware<RbacMiddleware>();     // authorization
app.UseMiddleware<RequestValidator>();   // input validation
app.UseMiddleware<ErrorHandler>();       // structured errors
```

---

## SignalR Hubs

```csharp
// Hubs/AgentHub.cs
public class AgentHub : Hub
{
    // Client → Server
    public async Task StartRun(string agentId, string sessionId, string input)
    public async Task CancelRun(string runId)
    public async Task ResumeRun(string runId, string input)
    
    // Server → Client (streaming)
    // OnRunStarted, OnRunDelta, OnToolCall, OnRunCompleted, OnRunError
    
    // Gruppi per sessione
    public async Task JoinSession(string sessionId)
    public async Task LeaveSession(string sessionId)
}

// Hubs/LogHub.cs
public class LogHub : Hub
{
    public async Task SubscribeToAgent(string agentId)
    public async Task SubscribeToModule(string module)
    public async Task SetLogLevel(string module, string level)
}

// Hubs/AdminHub.cs
public class AdminHub : Hub
{
    public async Task SubscribeToMetrics()
    public async Task SubscribeToErrors()
}
```

---

## Background Run Worker

```csharp
// Jobs/BackgroundRunWorker.cs
public sealed class BackgroundRunWorker : BackgroundService
{
    private readonly IChannel<RunRequest> _runChannel;
    private readonly IAgxiomeLogger _logger;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var run in _runChannel.Reader.ReadAllAsync(stoppingToken))
        {
            // Esegui agent in background
            // Pubblica eventi su SignalR
            // Salva risultati su DB
        }
    }
}
```

---

## Test per Runtime

```csharp
[Fact] public async Task AgentRun_ReturnsResult()
[Fact] public async Task AgentStream_ReturnsDeltas()
[Fact] public async Task AgentBackground_CompletesAsync()
[Fact] public async Task Session_CreateRetrieveDelete()
[Fact] public async Task Team_Coordinate_Returns()
[Fact] public async Task Workflow_Execute_AllSteps()
[Fact] public async Task Knowledge_IngestSearch()
[Fact] public async Task Auth_Login_ReturnsToken()
[Fact] public async Task Auth_InvalidToken_Rejected()
[Fact] public async Task Admin_Health_ReturnsOk()
[Fact] public async Task RateLimit_Exceeded_429()
[Fact] public async Task SignalR_StartRun_StreamsEvents()
```

**File: ~60 | Endpoint: ~52 | Test: ~200**
