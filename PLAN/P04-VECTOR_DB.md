# P04 — Vector Database Provider (30+)

> **Priority**: 🔴 P0 | **Dipende da**: P06 (Embedders) | **Usato da**: P05 (Readers), P08 (Runtime)
> **Target**: 30+ vector DB (Agno ha ~20)

---

## Provider Esistente (1) ✅

| # | Provider | Status | Metodo |
|---|----------|--------|--------|
| 1 | **pgvector** | ✅ Done | Cosine distance su PostgreSQL |

---

## Provider da Implementare (30+)

### 🔴 Wave 1 — Cloud & Managed (10)

| # | Provider | Engine | Package | NuGet |
|---|----------|--------|---------|-------|
| VD-01 | **Pinecone** | Serverless | `Pinecone.NET` | ✅ |
| VD-02 | **Weaviate Cloud** | Hybrid | `Weaviate.Client` | ✅ |
| VD-03 | **Qdrant Cloud** | Pure vector | `Qdrant.Client` | ✅ |
| VD-04 | **Milvus Cloud (Zilliz)** | ANN | `Milvus.Client` | ✅ |
| VD-05 | **Chroma DB** | Embedded | `ChromaDB.Client` | ✅ |
| VD-06 | **Supabase pgvector** | SQL+vector | `Npgsql` | ✅ |
| VD-07 | **Neon pgvector** | Serverless | `Npgsql` | ✅ |
| VD-08 | **Timescale pgvector** | Time+vector | `Npgsql` | ✅ |
| VD-09 | **CockroachDB pgvector** | Distr+vector | `Npgsql` | ✅ |
| VD-10 | **SingleStore vector** | SQL+vector | `SingleStoreConnector` | ✅ |

### 🔴 Wave 2 — Self-Hosted (8)

| # | Provider | Engine | Package | NuGet |
|---|----------|--------|---------|-------|
| VD-11 | **Qdrant** | Self-hosted | `Qdrant.Client` | ✅ |
| VD-12 | **Weaviate** | Self-hosted | `Weaviate.Client` | ✅ |
| VD-13 | **Milvus** | Self-hosted | `Milvus.Client` | ✅ |
| VD-14 | **Chroma** | Self-hosted | `ChromaDB.Client` | ✅ |
| VD-15 | **Elasticsearch Dense** | Search+vector | `Elastic.Clients.Elasticsearch` | ✅ |
| VD-16 | **Meilisearch vector** | Search+vector | `Meilisearch` | ✅ |
| VD-17 | **Vald** | Cloud native | gRPC client | ⚠️ |
| VD-18 | **Vespa** | Big data | `Vespa.Net` | ✅ |

### 🟡 Wave 3 — SQL-Embedded Vector (6)

| # | Provider | Engine | Package | NuGet |
|---|----------|--------|---------|-------|
| VD-19 | **SQLite + sqlite-vec** | Embedded | `sqlite-vec` | ✅ |
| VD-20 | **LanceDB** | Embedded | `LanceDb.Client` | ✅ |
| VD-21 | **DuckDB + vector** | Analytical | `DuckDB.NET` | ✅ |
| VD-22 | **Redis Stack** | KV+vector | `StackExchange.Redis` | ✅ |
| VD-23 | **MongoDB Atlas** | Doc+vector | `MongoDB.Driver` | ⚠️ |
| VD-24 | **Cosmos DB vCore** | Vector search | `Microsoft.Azure.Cosmos` | ⚠️ |

### 🟢 Wave 4 — Specialized & Emerging (6)

| # | Provider | Engine | Package | NuGet |
|---|----------|--------|---------|-------|
| VD-25 | **HNSWLib** | In-memory HNSW | embedding DLL | ✅ |
| VD-26 | **FAISS** | ANN library | `Microsoft.ML.OnnxRuntime` | ✅ |
| VD-27 | **Annoy** | Approx NN | C# binding | ✅ |
| VD-28 | **ScaNN** | ANN (Google) | gRPC | ⚠️ |
| VD-29 | **RocksDB + HNSW** | Embedded KV | `RocksDB` | ✅ |
| VD-30 | **SQL Server + vector** | Enterprise | `Microsoft.Data.SqlClient` | ⚠️ |

Nota operativa:
`VD-23`, `VD-24`, `VD-28` e `VD-30` restano backlog futuro perché la chiusura onesta richiede ambienti esterni reali o feature/runtime non presenti localmente.

---

## Interfaccia Unificata

```csharp
public interface IVectorStore
{
    string Provider { get; }
    int Dimension { get; }
    SimilarityMetric Metric { get; } // Cosine, Euclidean, DotProduct
    
    Task CreateCollectionAsync(string name, int dimension, CancellationToken ct);
    Task DeleteCollectionAsync(string name, CancellationToken ct);
    
    Task InsertAsync(string collection, VectorRecord record, CancellationToken ct);
    Task InsertBatchAsync(string collection, IList<VectorRecord> records, CancellationToken ct);
    
    Task<IList<VectorRecord>> SearchAsync(
        string collection, 
        float[] queryVector, 
        int topK, 
        IList<VectorFilter>? filters,
        CancellationToken ct);
    
    Task<bool> DeleteAsync(string collection, string id, CancellationToken ct);
    Task<long> CountAsync(string collection, CancellationToken ct);
}

public record VectorRecord
{
    public string Id { get; init; }
    public float[] Vector { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
    public float? Score { get; init; }
}

public record VectorFilter
{
    public string Field { get; init; }
    public FilterOperator Operator { get; init; }
    public object Value { get; init; }
}
```

---

## Pattern di Implementazione

```csharp
// VectorStores/PineconeStore.cs
public sealed class PineconeStore : IVectorStore
{
    private readonly PineconeClient _client;
    private readonly string _environment;
    private readonly string _projectId;
    
    // Inserimento: upsert con idempotency
    // Ricerca: query ANN con metadata filter
    // Delete: delete by id o metadata filter
    // Namespace: isolation per tenant/sessione
}
```

---

## Test per Vector DB

```csharp
// Ogni vector store ha:
[Fact] public async Task Store_CreateDelete_Collection()
[Fact] public async Task Store_InsertAndSearch_ReturnsResults()
[Fact] public async Task Store_SearchWithFilters()
[Fact] public async Task Store_BatchInsert_Consistency()
[Fact] public async Task Store_DimensionMismatch_Error()
[Fact] public async Task Store_EmptyCollection_Search()
[Fact] public async Task Store_MultipleCollections_Isolation()
```

**Totale vector DB: 30 | File: ~40 | Test: ~210**
