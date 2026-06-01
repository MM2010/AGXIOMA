# CQRS Architecture

AGXIOME uses strict **Command-Query Responsibility Segregation** (CQRS) for all storage operations.

## Principle

```
┌─────────────────────────────────────────┐
│              APPLICATION                 │
│                 │                        │
│    ┌────────────┴────────────┐          │
│    │                         │          │
│    ▼                         ▼          │
│ COMMAND SIDE              QUERY SIDE    │
│ (writes)                  (reads)       │
│    │                         │          │
│ ICommandStore           IQueryStore<T>  │
│    │                         │          │
│ SaveAsync()              GetByIdAsync() │
│ UpdateAsync()            QueryAsync()   │
│ DeleteAsync()            QueryPaged()   │
│    │                         │          │
│    └────────────┬────────────┘          │
│                 │                        │
│          IUnitOfWork                     │
│    Begin → Commit / Rollback            │
└─────────────────────────────────────────┘
```

## Command Side (Writes)

```csharp
public interface ICommandStore
{
    Task SaveAsync<T>(T entity, CancellationToken ct = default) where T : class;
    Task UpdateAsync<T>(T entity, CancellationToken ct = default) where T : class;
    Task DeleteAsync<T>(string id, CancellationToken ct = default) where T : class;
}
```

## Query Side (Reads)

```csharp
public interface IQueryStore<T> where T : class
{
    Task<T?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> QueryAsync(ISpecification<T> spec, CancellationToken ct = default);
    Task<IPaginatedResult<T>> QueryPagedAsync(ISpecification<T> spec, int page, int size, CancellationToken ct = default);
}
```

## Unit of Work

```csharp
using var uow = new PostgresUnitOfWork(connectionString);
await uow.BeginAsync();

try
{
    await uow.Commands.SaveAsync(new Agent { Id = "agent-1", Name = "Assistant" });
    await uow.Commands.SaveAsync(new Session { Id = "sess-1", AgentId = "agent-1" });
    await uow.CommitAsync();
}
catch
{
    await uow.RollbackAsync();
    throw;
}
```

## Specification Pattern

```csharp
public class ActiveAgentsSpec : ISpecification<Agent>
{
    public Expression<Func<Agent, bool>> ToExpression()
        => a => a.Status == AgentStatus.Active;
}

var activeAgents = await queryStore.QueryAsync(new ActiveAgentsSpec());
```

## Outbox Pattern

Commands can emit domain events via the outbox pattern — ensuring atomicity between state changes and event publication:

```
Save entity ──→ Write to outbox table (same transaction)
                         │
                   Background worker
                         │
                   Publish events (SignalR, XLink, ...)
```

## Supported Storage Backends

All 25+ backends implement `ICommandStore` and `IQueryStore<T>`. Swap without changing application code:

```csharp
// PostgreSQL
var store = new PostgresCommandStore(connectionString);

// SQLite (embedded, zero setup)
var store = new SqliteCommandStore("Data Source=agxiome.db");

// Redis (cache-first)
var store = new RedisCommandStore("localhost:6379");

// MongoDB
var store = new MongoCommandStore("mongodb://localhost:27017");
```

## Next

- [Unit of Work](uow.md)
- [DB Providers](db-providers.md)
