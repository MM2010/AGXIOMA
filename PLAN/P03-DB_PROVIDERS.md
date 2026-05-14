# P03 — DB Provider (25+)

> **Priority**: 🔴 P0 | **Dipende da**: P09 (Core) | **Usato da**: P08 (Runtime), P10 (Storage)
> **Target**: 25+ database provider (Agno ha 18)

---

## Provider Esistenti (5) ✅

| # | Provider | Type | File |
|---|----------|------|------|
| 1 | PostgreSQL | ✅ SQL | `PostgresCommandStore.cs` |
| 2 | MySQL | ✅ SQL | `MySqlCommandStore.cs` |
| 3 | MariaDB | ✅ SQL | `MariaDbStore.cs` |
| 4 | MongoDB | ✅ NoSQL | `MongoStore.cs` |
| 5 | Redis (cache) | ✅ KV | `RedisCache.cs` |

---

## Provider da Implementare (20+)

### 🔴 Wave 1 — SQL (8)

| # | Provider | Type | Package | NuGet |
|---|----------|------|---------|-------|
| DB-01 | **SQLite** | SQL embedded | `Microsoft.Data.Sqlite` | ✅ |
| DB-02 | **SQL Server** | SQL enterprise | `Microsoft.Data.SqlClient` | ✅ |
| DB-03 | **SQL Server LocalDB** | SQL dev | `Microsoft.Data.SqlClient` | ✅ |
| DB-04 | **Neon (Serverless PG)** | SQL serverless | `Npgsql` (endpoint custom) | ✅ |
| DB-05 | **Supabase** | SQL + BaaS | `Npgsql` + `Supabase` | ✅ |
| DB-06 | **SingleStore** | SQL distribuito | `SingleStoreConnector` | ✅ |
| DB-07 | **CockroachDB** | SQL distribuito | `Npgsql` (compat PG) | ✅ |
| DB-08 | **TimescaleDB** | SQL time-series | `Npgsql` (compat PG) | ✅ |

### 🔴 Wave 2 — NoSQL (6)

| # | Provider | Type | Package | NuGet |
|---|----------|------|---------|-------|
| DB-09 | **DynamoDB** | KV/doc | `AWSSDK.DynamoDBv2` | ✅ |
| DB-10 | **Cosmos DB** | Multi-model | `Microsoft.Azure.Cosmos` | ✅ |
| DB-11 | **Firestore** | Document | `Google.Cloud.Firestore` | ✅ |
| DB-12 | **SurrealDB** | Multi-model | `SurrealDb.Client` | ✅ |
| DB-13 | **RavenDB** | Document | `RavenDB.Client` | ✅ |
| DB-14 | **Couchbase** | KV/doc | `CouchbaseNetClient` | ✅ |

### 🟡 Wave 3 — KV & Cache (4)

| # | Provider | Type | Package | NuGet |
|---|----------|------|---------|-------|
| DB-15 | **Redis Session** | KV session | `Microsoft.Extensions.Caching.StackExchangeRedis` | ✅ |
| DB-16 | **Memcached** | KV cache | `EnyimMemcachedCore` | ✅ |
| DB-17 | **Hazelcast** | KV distribuito | `Hazelcast.Net` | ✅ |
| DB-18 | **Etcd** | KV distribuito | `dotnet-etcd` | ✅ |

### 🟢 Wave 4 — Time Series & Specialized (7)

| # | Provider | Type | Package | NuGet |
|---|----------|------|---------|-------|
| DB-19 | **ClickHouse** | Columnar OLAP | `ClickHouse.Client` | ✅ |
| DB-20 | **InfluxDB** | Time Series | `InfluxDB.Client` | ✅ |
| DB-21 | **QuestDB** | Time Series | `Npgsql` (compat PG) | ✅ |
| DB-22 | **DuckDB** | Analytical embedded | `DuckDB.NET.Data.Full` | ✅ |
| DB-23 | **Elasticsearch** | Search/Doc | `Elastic.Clients.Elasticsearch` | ✅ |
| DB-24 | **Meilisearch** | Search | `Meilisearch` | ✅ |
| DB-25 | **JSON File** | File-based | `System.Text.Json` | ✅ |

---

## Pattern di Implementazione

```csharp
// Stores/XXXStore.cs
public sealed class XXXStore : IAgxiomeStore
{
    private readonly string _connectionString;
    private readonly ResilientExecutor _resilient;
    private readonly IAgxiomeLogger _logger;

    // InitAsync: test connessione, crea tabelle se necessario
    // SaveAsync: upsert con idempotency key
    // LoadAsync: get by id
    // DeleteAsync: soft-delete con audit trail
    // SearchAsync: query con filtri
}
```

---

## Interfaccia Unificata

```csharp
public interface IAgxiomeStore
{
    string Provider { get; }
    bool SupportsTransactions { get; }
    bool SupportsVectorSearch { get; }
    
    Task<bool> HealthCheckAsync(CancellationToken ct);
    Task<T> SaveAsync<T>(T entity, string idempotencyKey, CancellationToken ct);
    Task<T?> LoadAsync<T>(string id, CancellationToken ct);
    Task<bool> DeleteAsync(string id, bool softDelete, CancellationToken ct);
    Task<List<T>> SearchAsync<T>(QueryFilter filter, CancellationToken ct);
    Task<long> CountAsync(string collection, CancellationToken ct);
}
```

---

## Test per DB Provider

```csharp
// Ogni provider ha:
[Fact] public async Task Provider_Init_CreatesSchema()
[Fact] public async Task Provider_Save_Load_Roundtrip()
[Fact] public async Task Provider_Search_WithFilters()
[Fact] public async Task Provider_Delete_SoftDelete()
[Fact] public async Task Provider_ConcurrentAccess_Safe()
[Fact] public async Task Provider_ConnectionLoss_ErrorCode()
[Fact] public async Task Provider_Idempotency_Duplicate()
```

**Totale provider: 25 | File: ~50 | Test: ~175**
