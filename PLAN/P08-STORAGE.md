# P08 — Storage + CQRS

## File: `Storage/PostgresStore.cs`, `Storage/RedisStore.cs`

Namespace: `AGXIOME.Storage`

## Classi

### `ICommandStore` / `IQueryStore<T>` / `IUnitOfWork`

```csharp
public interface ICommandStore
{
    Task SaveAsync<T>(T entity, CancellationToken ct) where T : IAgxiomeEntity;
    Task UpdateAsync<T>(T entity, CancellationToken ct) where T : IAgxiomeEntity;
    Task DeleteAsync<T>(string id, CancellationToken ct) where T : IAgxiomeEntity;
}

public interface IQueryStore<T>
{
    Task<T?> GetByIdAsync(string id, CancellationToken ct);
    Task<IReadOnlyList<T>> QueryAsync(ISpecification<T> spec, CancellationToken ct);
    Task<IPaginatedResult<T>> QueryPagedAsync(ISpecification<T> spec, int page, int size, CancellationToken ct);
}

public interface IUnitOfWork : IDisposable
{
    Task BeginAsync(CancellationToken ct);
    Task CommitAsync(CancellationToken ct);
    Task RollbackAsync(CancellationToken ct);
    ICommandStore Commands { get; }
}
```

### `PostgresStore : ICommandStore, IQueryStore<T>`

```csharp
public class PostgresStore : ICommandStore
{
    private readonly string _connectionString;

    // Tutte le operazioni usano Dapper + Npgsql
    // Connessione dal pool, transazioni esplicite
    public async Task SaveAsync<T>(T entity, CancellationToken ct) where T : IAgxiomeEntity
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        // INSERT INTO {tableName} (...) VALUES (...)
        // Table name da attributo [Table("agents")] sull'entity
    }
}
```

### `RedisCache`

```csharp
public class RedisCache
{
    private readonly ConnectionMultiplexer _redis;

    public async Task<T?> GetAsync<T>(string key);
    public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null);
    public async Task InvalidateAsync(string pattern);  // Scan + DEL

    // Lettura: read-through
    // Scrittura: write-through con TTL
    // Invalidation: event-driven
}
```

## DB Schema (PostgreSQL)

```sql
-- Session table
CREATE TABLE sessions (
    id VARCHAR(64) PRIMARY KEY,
    user_id VARCHAR(64) NOT NULL,
    component_id VARCHAR(64) NOT NULL,
    type VARCHAR(16) NOT NULL,
    name VARCHAR(255),
    session_state JSONB DEFAULT '{}',
    metadata JSONB DEFAULT '{}',
    summary TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Messages
CREATE TABLE session_messages (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id VARCHAR(64) REFERENCES sessions(id) ON DELETE CASCADE,
    role VARCHAR(32) NOT NULL,
    content TEXT NOT NULL,
    tool_calls_json JSONB,
    metadata_json JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Memory (pgvector)
CREATE TABLE agent_memory (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    agent_id VARCHAR(64),
    content TEXT NOT NULL,
    embedding VECTOR(1536),
    metadata_json JSONB DEFAULT '{}',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Outbox
CREATE TABLE outbox_messages (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    event_type VARCHAR(255) NOT NULL,
    event_body_json JSONB NOT NULL,
    correlation_id VARCHAR(64),
    processed_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

## Outbox pattern

```csharp
// 1. In UoW.CommitAsync(), scrivi OutboxMessage nella stessa transazione
// 2. Background worker polla ogni 500ms
// 3. Processa eventi: publish al message bus
// 4. Segna processed_at
```

## Test

| Test | Cosa verifica |
|------|--------------|
| Save + GetById | Entity match |
| Query with filter | Risultati filtrati |
| Unit of work rollback | Nessun dato scritto |
| Cache read-through | Redis popolato dopo miss |
| Cache invalidation | Redis vuoto dopo delete |
| Outbox delivery | Evento processato |
