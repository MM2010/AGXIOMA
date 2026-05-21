# Storage & RAG — AGXIOME

Persistenza multi-provider con CQRS. Architettura a 2 livelli:

```
AGXIOME.Core (Interfacce)         AGXIOME.Storage (Implementazioni)
├── ICommandStore                 ├── Stores/          → 25+ DB provider
├── IQueryStore<T>                ├── VectorStores/    → 12+ vector DB
├── IUnitOfWork                   ├── Embedders/       → 15+ embedder
├── ISessionManager               ├── Chunkers/        → 10+ chunker
├── IAgentMemory                  ├── Readers/         → 15+ document reader
├── IAgentMemoryStore             ├── Rerankers/       → 5+ reranker
├── ISemanticFactStore            ├── Session/         → RedisSessionManager
└── ICacheStore                   ├── Agent/           → AgentStorageBridge
                                  ├── Team/            → TeamStorageBridge, TeamToolPolicyStore
                                  ├── Knowledge/       → ContentStore, MarkdownMemoryStore
                                  └── Workflow/        → WorkflowHistoryStore, WorkflowHitlStore, WorkflowStorage
```

**Regola**: Core definisce interfacce e orchestrazione. Storage implementa persistenza.

---

## Provider supportati

## Provider supportati

| Database | Namespace | Classe | Uso principale | ACID |
|----------|-----------|--------|---------------|------|
| **PostgreSQL** | `AGXIOME.Storage.Stores` | `PostgresCommandStore` | Canonical state, pgvector, RAG | ✅ |
| **PostgreSQL(Session)** | `AGXIOME.Storage.Session` | `PostgresSessionManager` | Session lifecycle + history | ✅ |
| **Redis(Session)** | `AGXIOME.Storage.Session` | `RedisSessionManager` | Session hot cache with TTL | ❌ |
| **MySQL** | `AGXIOME.Storage.Stores` | `MySqlCommandStore` | Session storage, chat history | ✅ |
| **MariaDB** | `AGXIOME.Storage.Stores` | `MariaDbStore` | Drop-in MySQL compat | ✅ |
| **MongoDB** | `AGXIOME.Storage.Stores` | `MongoStore` | Document store, unstructured memory | ✅ (replica) |
| **Redis** | `AGXIOME.Storage.Stores` | `RedisCache` | Cache, pub/sub, session, rate-limit | ❌ |

---

## Configurazione

```csharp
var config = new StorageConfig
{
    DefaultProvider = "PostgreSQL",
    Providers = new()
    {
        ["PostgreSQL"] = new() { ConnectionString = "Host=localhost;Database=agxiome" },
        ["MySQL"]      = new() { ConnectionString = "Server=localhost;Database=agxiome;User=root" },
        ["MariaDB"]    = new() { ConnectionString = "Server=localhost;Database=agxiome;User=root" },
        ["MongoDB"]    = new() { ConnectionString = "mongodb://localhost:27017/agxiome" },
        ["Redis"]      = new() { ConnectionString = "localhost:6379,abortConnect=false" },
    }
};
```

---

## PostgreSQL (CQRS + RAG)

```csharp
// Write
var store = new PostgresCommandStore("Host=localhost;Database=agxiome");
await store.SaveAsync(session, ct);

// Read via Dapper
await using var conn = new NpgsqlConnection(connStr);
var sessions = await conn.QueryAsync<Session>("SELECT * FROM sessions WHERE user_id = @U", new { U = userId });

// RAG con pgvector
var vectorStore = new PgVectorStore(connStr);
await vectorStore.UpsertAsync("doc-1", chunk, embedding, ct);
var results = await vectorStore.SearchAsync(queryVec, new KnowledgeSearchOptions { TopK = 5 }, ct);
```

### Unit of Work

```csharp
await using var uow = new UnitOfWork(connStr);
await uow.BeginAsync(ct);
await uow.Commands.SaveAsync(entity, ct);
await uow.CommitAsync(ct);
```

### Outbox

```csharp
var outbox = new OutboxWriter(connStr);
await outbox.WriteAsync(domainEvent, "session.created");

var poller = new OutboxPoller(connStr);
await poller.ProcessBatchAsync(batchSize: 50);
```

---

## MySQL

```csharp
var store = new MySqlCommandStore("Server=localhost;Database=agxiome;User=root;Password=secret");
await store.SaveAsync(new AgentInteraction("agent-1", "user", "Hello"), ct);
```

### Tabella consigliata

```sql
CREATE TABLE sessions (
    id VARCHAR(64) PRIMARY KEY,
    user_id VARCHAR(64) NOT NULL,
    component_id VARCHAR(64) NOT NULL,
    type VARCHAR(16) NOT NULL,
    session_state JSON DEFAULT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_user (user_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
```

---

## MariaDB

MariaDB è **wire-compatible** con MySQL. Usa la stessa libreria `MySqlConnector`.

```csharp
var store = new MariaDbStore("Server=localhost;Database=agxiome;User=root");
await store.SaveAsync(session, ct);
```

### Differenze da MySQL

- Storage engine predefinito: `InnoDB` (consigliato) o `Aria`
- JSON type supportato da MariaDB 10.2+
- Sequence storage engine per performance bulk insert
- Per pgvector-like: usare plugin `MARIADB_VEC` (MariaDB 11.7+)

```sql
-- MariaDB 11.7+ vector support
CREATE TABLE knowledge_chunks (
    id VARCHAR(64) PRIMARY KEY,
    content TEXT NOT NULL,
    embedding VECTOR(1536),
    VECTOR INDEX idx_embedding (embedding) USING HNSW
);
```

---

## MongoDB

Per document store non strutturati, memory agent, session state flessibile.

```csharp
var mongo = new MongoStore("mongodb://localhost:27017", "agxiome");

// Salva documento
await mongo.SaveAsync(new MongoSession
{
    Id = "session-1",
    UserId = "user-1",
    ComponentId = "agent-1",
    State = new BsonDocument { ["preferences"] = "dark_mode" }
}, ct);

// Query
var results = await mongo.QueryAsync<MongoSession>(
    Builders<MongoSession>.Filter.Eq(s => s.UserId, "user-1"), ct);
```

### Collections

| Collection | Modello C# | Scopo |
|-----------|-----------|-------|
| `sessions` | `MongoSession` | Sessioni con stato JSON flessibile |
| `messages` | `BsonDocument` | Chat history (schema-less) |
| `memory` | `BsonDocument` | Learning machine store |

---

## Redis (Cache)

```csharp
var cache = new RedisCache("localhost:6379,abortConnect=false");

await cache.SetAsync("agent:session:123", sessionData, TimeSpan.FromMinutes(5));
var cached = await cache.GetAsync<SessionData>("agent:session:123");
await cache.InvalidateAsync("agent:session:*");
```

### Pattern cache

| Pattern | Comportamento |
|---------|--------------|
| Read-through | Cache miss → carica da PostgreSQL → popola Redis |
| Write-through | Scrittura → aggiorna DB + cache |
| TTL | Default 5 minuti, override per tipo |
| Invalidation | Event-driven su domain events |

---

## Confronto provider

| Feature | PostgreSQL | MySQL | MariaDB | MongoDB | Redis |
|---------|-----------|-------|---------|---------|-------|
| CQRS Command Store | ✅ | ✅ | ✅ | ✅ | ❌ |
| Unit of Work | ✅ | ✅ | ✅ | ❌ | ❌ |
| Outbox Pattern | ✅ | ✅ | ✅ | ✅ (Change Streams) | ❌ |
| RAG / pgvector | ✅ | ❌ | ✅ (11.7+) | ✅ (Atlas Vector Search) | ❌ |
| Session Cache | ❌ | ❌ | ❌ | ❌ | ✅ |
| Rate Limiting | ❌ | ❌ | ❌ | ❌ | ✅ |
| Pub/Sub | ❌ | ❌ | ❌ | ❌ | ✅ |
| JSON Document | ✅ (JSONB) | ✅ (JSON) | ✅ (JSON) | ✅ (BSON) | ❌ |
| ACID Transactions | ✅ | ✅ (InnoDB) | ✅ | ✅ (Replica Set) | ❌ |
