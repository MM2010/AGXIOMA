# 03 — Storage Layer

## Scopo

Persistenza multi-model con CQRS rigoroso. PostgreSQL per stato canonico + vettori, Redis per cache/pub/sub, altri provider per casi specifici.

## Architettura CQRS

```
         ┌──────────────────────────────────────┐
         │         APPLICATION / AGENT           │
         └──────────┬───────────────┬───────────┘
                    │               │
                    ▼               ▼
         ┌─────────────────┐ ┌─────────────────┐
         │  COMMAND SIDE   │ │   QUERY SIDE    │
         │  (Write Model)  │ │  (Read Model)   │
         └────────┬────────┘ └────────┬────────┘
                  │                   │
                  ▼                   ▼
         ┌─────────────────┐ ┌─────────────────┐
         │   Command       │ │    Query        │
         │   Handler       │ │    Handler      │
         └────────┬────────┘ └────────┬────────┘
                  │                   │
                  ▼                   │
         ┌─────────────────┐          │
         │  PostgreSQL     │          │
         │  (Canonical)    │◄─────────┘ (read-through)
         └────────┬────────┘
                  │ event
                  ▼
         ┌─────────────────┐
         │   Outbox        │──► Event Bus → Invalidate cache
         └─────────────────┘
                              ┌─────────────────┐
                              │    Redis Cache   │◄── Read-Through
                              └─────────────────┘
```

## Contratti

```csharp
// ── Write side ──
public interface ICommandStore
{
    Task SaveAsync<T>(T entity, CancellationToken ct) where T : IAgxiomeEntity;
    Task UpdateAsync<T>(T entity, CancellationToken ct) where T : IAgxiomeEntity;
    Task DeleteAsync<T>(string id, CancellationToken ct) where T : IAgxiomeEntity;
    Task<TResult> ExecuteAsync<TResult>(ICommand<TResult> command, CancellationToken ct);
}

// ── Read side ──
public interface IQueryStore<T>
{
    Task<T?> GetByIdAsync(string id, CancellationToken ct);
    Task<IReadOnlyList<T>> QueryAsync(ISpecification<T> spec, CancellationToken ct);
    Task<IPaginatedResult<T>> QueryPagedAsync(
        ISpecification<T> spec, int page, int size, CancellationToken ct);
}

// ── Unit of Work ──
public interface IUnitOfWork : IDisposable
{
    Task BeginAsync(CancellationToken ct);
    Task CommitAsync(CancellationToken ct);
    Task RollbackAsync(CancellationToken ct);
    ICommandStore Commands { get; }
}

// ── Entity marker ──
public interface IAgxiomeEntity
{
    string Id { get; }
    DateTime CreatedAt { get; }
    DateTime? UpdatedAt { get; }
    string? TenantId { get; }
    byte[] RowVersion { get; } // Concurrency token
}
```

## Provider implementati

| Provider | Modello | Uso principale | ACID | Index |
|----------|---------|---------------|------|-------|
| **PostgreSQL** | Relational + pgvector | Stato canonico, embeddings, memoria agenti | ✅ | B-tree, GIN, IVFFlat |
| **Redis** | Key-value | Cache, pub/sub, rate-limit, sessioni | ❌ | O(1) lookup |
| **SQL Server** | Relational | Legacy enterprise | ✅ | Full-text |
| **MySQL** | Relational | Drop-in compat | ✅ | InnoDB |
| **MariaDB** | Relational | Compat MySQL | ✅ | InnoDB |
| **MongoDB** | Document | Document store unstructured | ✅ (replica set) | 2dsphere, text |
| **Snowflake** | Columnar | Analytics, telemetry agenti | ✅ | Micro-partition |

## PostgreSQL — Schema principale

```sql
-- Agents table
CREATE TABLE agents (
    id              VARCHAR(64) PRIMARY KEY,
    name            VARCHAR(255) NOT NULL,
    status          VARCHAR(32) NOT NULL DEFAULT 'idle',
    config_json     JSONB NOT NULL DEFAULT '{}',
    tenant_id       VARCHAR(64),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ,
    row_version     INTEGER NOT NULL DEFAULT 1
);

-- Agent interactions (history)
CREATE TABLE agent_interactions (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    agent_id        VARCHAR(64) NOT NULL REFERENCES agents(id),
    role            VARCHAR(32) NOT NULL, -- 'user', 'assistant', 'tool'
    content         TEXT NOT NULL,
    tool_calls_json JSONB,
    metadata_json   JSONB,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_interactions_agent_time
    ON agent_interactions(agent_id, created_at DESC);

-- Agent memory (embeddings via pgvector)
CREATE TABLE agent_memory (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    agent_id        VARCHAR(64) NOT NULL REFERENCES agents(id),
    content         TEXT NOT NULL,
    embedding       VECTOR(1536),  -- 1536-dim default (configurable)
    metadata_json   JSONB DEFAULT '{}',
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_memory_agent ON agent_memory(agent_id);
CREATE INDEX idx_memory_embedding ON agent_memory
    USING IVFFLAT (embedding vector_cosine_ops) WITH (lists = 100);

-- Outbox per reliable event publication
CREATE TABLE outbox_messages (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    event_type      VARCHAR(255) NOT NULL,
    event_body_json JSONB NOT NULL,
    correlation_id  VARCHAR(64),
    tenant_id       VARCHAR(64),
    processed_at    TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_outbox_unprocessed
    ON outbox_messages(created_at) WHERE processed_at IS NULL;
```

## Cache strategy (Redis)

| Cache | Key pattern | TTL | Invalidation |
|-------|-----------|-----|-------------|
| Agent state | `agent:{id}:state` | 5 min | On interaction save |
| Agent config | `agent:{id}:config` | 1 h | On config update |
| Query result | `query:{hash}:page:{n}` | 2 min | On domain event |
| Session | `session:{id}` | 30 min | On session end |
| Rate-limit | `ratelimit:{agent}:{op}` | sliding window | Automatic expiry |

## Read-Through Cache Flow

```
Query(agentId, spec):
  1. hash = computeHash(spec)
  2. Check Redis: `query:{hash}:page:{n}`
  3. HIT → return deserialized
  4. MISS → query PostgreSQL
  5. Store result in Redis (TTL 2 min)
  6. Return result
```

## Outbox pattern

```
Command handler:
  1. Begin UoW (PostgreSQL transaction)
  2. Execute domain logic
  3. Save aggregate to command store
  4. Write OutboxMessage(s) to outbox table
  5. Commit UoW
  6. Background worker: poll outbox → publish to event bus
  7. On publish success: mark processed_at
```

## Configurazione provider

```csharp
public class StorageConfig
{
    public string DefaultProvider { get; set; } = "PostgreSQL";
    public Dictionary<string, ProviderConfig> Providers { get; set; } = new();

    public class ProviderConfig
    {
        public string ConnectionString { get; set; } = "";
        public int MaxPoolSize { get; set; } = 100;
        public int CommandTimeoutSeconds { get; set; } = 30;
        public bool EnableLogging { get; set; } = false;
        public RetryPolicyConfig RetryPolicy { get; set; } = new();
    }

    public class RetryPolicyConfig
    {
        public int MaxRetries { get; set; } = 3;
        public double BaseDelayMs { get; set; } = 100;
        public bool EnableExponentialBackoff { get; set; } = true;
    }
}
```

## Principi CQRS enforcement

| Regola | Sanzione |
|--------|----------|
| Command handler non può eseguire query | Review gate |
| Query handler non può scrivere | Review gate |
| UoW solo su command side | Runtime check |
| Niente transazioni aperte su SignalR fan-out | Runtime check |
| Cache invalidation solo via eventi | Design rule |

## Component Registry (Serialization)

```csharp
public interface IComponentRegistry
{
    // Save
    Task<string> SaveAgentAsync(IAgxiomeAgent agent, CancellationToken ct);
    Task<string> SaveTeamAsync(IAgxiomeTeam team, CancellationToken ct);
    Task<string> SaveWorkflowAsync(IAgxiomeWorkflow workflow, CancellationToken ct);

    // Load
    Task<IAgxiomeAgent> LoadAgentAsync(string id, CancellationToken ct);
    Task<IAgxiomeTeam> LoadTeamAsync(string id, CancellationToken ct);
    Task<IAgxiomeWorkflow> LoadWorkflowAsync(string id, CancellationToken ct);

    // Registry (non-serializable components)
    void RegisterToolType(string name, Type type);
    void RegisterModelProvider(string name, Func<LLMWIKYConfig, ILLMProvider> factory);
    void RegisterVectorDb(string name, Type dbType);
}

// ── Schema tabella ──
/*
CREATE TABLE component_registry (
    id              VARCHAR(64) PRIMARY KEY,
    type            VARCHAR(32) NOT NULL,  -- 'agent', 'team', 'workflow'
    name            VARCHAR(255) NOT NULL,
    config_json     JSONB NOT NULL,
    metadata_json   JSONB DEFAULT '{}',
    version         INTEGER NOT NULL DEFAULT 1,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE component_configs (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    component_id    VARCHAR(64) NOT NULL REFERENCES component_registry(id),
    version         INTEGER NOT NULL,
    config_json     JSONB NOT NULL,
    status          VARCHAR(32) NOT NULL DEFAULT 'draft', -- draft, published, archived
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
*/
```

## NFR Storage

| Metrica | Target |
|---------|--------|
| Write p95 (single entity) | < 10 ms |
| Read p95 (by PK, cache hit) | < 2 ms |
| Read p95 (by PK, cache miss) | < 15 ms |
| Semantic search p95 (10K docs) | < 50 ms |
| Semantic search p95 (100K docs) | < 150 ms |
| Outbox publish latency p99 | < 500 ms |
| Max pool size (per instance) | 100 |
