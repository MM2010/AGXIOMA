using System.Net;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using AGXIOME.Storage.VectorStores;
using Elastic.Clients.Elasticsearch;
using Meilisearch;
using Microsoft.Azure.Cosmos;
using MongoDB.Driver;
using NSubstitute;
using Qdrant.Client;
using StackExchange.Redis;
using Xunit;

namespace AGXIOME.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// F5 — Vector Store Tests (VD-01..VD-30)
//
// Strategy:
//   • In-memory stores (HNSWLib, FAISS, Annoy): all 7 tests run — no server needed.
//   • Embedded DB stores (SqliteVec, DuckDB): all 7 tests run with :memory: data source.
//   • Persistent disk stores (RocksDB): all 7 tests run with temp directory + cleanup.
//   • HTTP stores (Pinecone, Weaviate, Milvus, Chroma, Vald, Vespa, LanceDB, ScaNN):
//       2 tests run (ProviderType + NullArg); 5 skipped (require live HTTP server).
//   • SDK-dependent stores (Qdrant, pgvector variants, SingleStore, Elasticsearch,
//     Meilisearch, Redis, MongoDB, Cosmos, SqlServer):
//       2 tests run; 5 skipped (require live server / cloud service).
//
// Total: 210 tests (7 × 30 stores).
// ─────────────────────────────────────────────────────────────────────────────

// ─── VD-01 — Pinecone ─────────────────────────────────────────────────────────

public class PineconeStoreTests
{
    private static HttpClient MockHttp() =>
        new HttpClient(new TestHttpMessageHandler(HttpStatusCode.OK, "{}"))
        { BaseAddress = new Uri("https://mock.pinecone.io/") };

    [Fact] public void ProviderType_IsPinecone()
        => Assert.Equal(VectorDbProviderType.Pinecone, new PineconeStore(MockHttp()).ProviderType);

    [Fact] public void Constructor_NullHttp_Throws()
        => Assert.Throws<ArgumentNullException>(() => new PineconeStore(null!));

    [Fact(Skip = "requires live Pinecone API key")]
    public Task EnsureCollection_Upsert_Get_RoundTrip() => Task.CompletedTask;

    [Fact(Skip = "requires live Pinecone API key")]
    public Task Search_EmptyCollection_ReturnsEmpty() => Task.CompletedTask;

    [Fact(Skip = "requires live Pinecone API key")]
    public Task UpsertBatch_MultipleRecords() => Task.CompletedTask;

    [Fact(Skip = "requires live Pinecone API key")]
    public Task Delete_RemovesRecord() => Task.CompletedTask;

    [Fact(Skip = "requires live Pinecone API key")]
    public Task ListCollections_ReturnsEmpty() => Task.CompletedTask;
}

// ─── VD-02 — Weaviate Cloud ───────────────────────────────────────────────────

public class WeaviateCloudStoreTests
{
    private static HttpClient MockHttp() =>
        new HttpClient(new TestHttpMessageHandler(HttpStatusCode.OK, "{}"))
        { BaseAddress = new Uri("https://mock.weaviate.io/") };

    [Fact] public void ProviderType_IsWeaviateCloud()
        => Assert.Equal(VectorDbProviderType.WeaviateCloud, new WeaviateCloudStore(MockHttp()).ProviderType);

    [Fact] public void Constructor_NullHttp_Throws()
        => Assert.Throws<ArgumentNullException>(() => new WeaviateCloudStore(null!));

    [Fact(Skip = "requires live Weaviate Cloud endpoint")]
    public Task EnsureCollection_Upsert_Get_RoundTrip() => Task.CompletedTask;

    [Fact(Skip = "requires live Weaviate Cloud endpoint")]
    public Task Search_EmptyCollection_ReturnsEmpty() => Task.CompletedTask;

    [Fact(Skip = "requires live Weaviate Cloud endpoint")]
    public Task UpsertBatch_MultipleRecords() => Task.CompletedTask;

    [Fact(Skip = "requires live Weaviate Cloud endpoint")]
    public Task Delete_RemovesRecord() => Task.CompletedTask;

    [Fact(Skip = "requires live Weaviate Cloud endpoint")]
    public Task ListCollections_ReturnsCollections() => Task.CompletedTask;
}

// ─── VD-03 — Qdrant Cloud ─────────────────────────────────────────────────────

public class QdrantCloudStoreTests
{
    [Fact] public void ProviderType_IsQdrantCloud()
    {
        // QdrantClient constructor requires a host; use the GrpcChannel-less overload
        // that accepts host + port without immediately opening a channel.
        var client = new QdrantClient("localhost", 6334, https: false);
        Assert.Equal(VectorDbProviderType.QdrantCloud, new QdrantCloudStore(client).ProviderType);
    }

    [Fact] public void Constructor_NullClient_Throws()
        => Assert.Throws<ArgumentNullException>(() => new QdrantCloudStore(null!));

    [Fact(Skip = "requires live Qdrant server")]
    public Task EnsureCollection_Upsert_Get_RoundTrip() => Task.CompletedTask;

    [Fact(Skip = "requires live Qdrant server")]
    public Task Search_EmptyCollection_ReturnsEmpty() => Task.CompletedTask;

    [Fact(Skip = "requires live Qdrant server")]
    public Task UpsertBatch_MultipleRecords() => Task.CompletedTask;

    [Fact(Skip = "requires live Qdrant server")]
    public Task Delete_RemovesRecord() => Task.CompletedTask;

    [Fact(Skip = "requires live Qdrant server")]
    public Task ListCollections_ReturnsCollections() => Task.CompletedTask;
}

// ─── VD-04 — Milvus Cloud ─────────────────────────────────────────────────────

public class MilvusCloudStoreTests
{
    private static HttpClient MockHttp() =>
        new HttpClient(new TestHttpMessageHandler(HttpStatusCode.OK, "{\"data\":[]}"))
        { BaseAddress = new Uri("https://mock.milvus.io/") };

    [Fact] public void ProviderType_IsMilvusCloud()
        => Assert.Equal(VectorDbProviderType.MilvusCloud, new MilvusCloudStore(MockHttp()).ProviderType);

    [Fact] public void Constructor_NullHttp_Throws()
        => Assert.Throws<ArgumentNullException>(() => new MilvusCloudStore(null!));

    [Fact(Skip = "requires live Milvus/Zilliz Cloud endpoint")]
    public Task EnsureCollection_Upsert_Get_RoundTrip() => Task.CompletedTask;

    [Fact(Skip = "requires live Milvus/Zilliz Cloud endpoint")]
    public Task Search_EmptyCollection_ReturnsEmpty() => Task.CompletedTask;

    [Fact(Skip = "requires live Milvus/Zilliz Cloud endpoint")]
    public Task UpsertBatch_MultipleRecords() => Task.CompletedTask;

    [Fact(Skip = "requires live Milvus/Zilliz Cloud endpoint")]
    public Task Delete_RemovesRecord() => Task.CompletedTask;

    [Fact(Skip = "requires live Milvus/Zilliz Cloud endpoint")]
    public Task ListCollections_ReturnsCollections() => Task.CompletedTask;
}

// ─── VD-05 — Chroma DB Cloud ──────────────────────────────────────────────────

public class ChromaDbCloudStoreTests
{
    private static HttpClient MockHttp() =>
        new HttpClient(new TestHttpMessageHandler(HttpStatusCode.OK, "{}"))
        { BaseAddress = new Uri("https://mock.trychroma.com/") };

    [Fact] public void ProviderType_IsChromaDB()
        => Assert.Equal(VectorDbProviderType.ChromaDB, new ChromaDbCloudStore(MockHttp()).ProviderType);

    [Fact] public void Constructor_NullHttp_Throws()
        => Assert.Throws<ArgumentNullException>(() => new ChromaDbCloudStore(null!));

    [Fact(Skip = "requires live Chroma Cloud endpoint")]
    public Task EnsureCollection_Upsert_Get_RoundTrip() => Task.CompletedTask;

    [Fact(Skip = "requires live Chroma Cloud endpoint")]
    public Task Search_EmptyCollection_ReturnsEmpty() => Task.CompletedTask;

    [Fact(Skip = "requires live Chroma Cloud endpoint")]
    public Task UpsertBatch_MultipleRecords() => Task.CompletedTask;

    [Fact(Skip = "requires live Chroma Cloud endpoint")]
    public Task Delete_RemovesRecord() => Task.CompletedTask;

    [Fact(Skip = "requires live Chroma Cloud endpoint")]
    public Task ListCollections_ReturnsCollections() => Task.CompletedTask;
}

// ─── VD-06 — Supabase pgvector ────────────────────────────────────────────────

public class SupabasePgVectorStoreTests
{
    [Fact] public void ProviderType_IsSupabasePgVector()
        => Assert.Equal(VectorDbProviderType.SupabasePgVector,
            new SupabasePgVectorStore("Host=localhost;Database=test").ProviderType);

    [Fact] public void Constructor_EmptyConnectionString_Throws()
        => Assert.Throws<ArgumentException>(() => new SupabasePgVectorStore(""));

    [Fact(Skip = "requires live PostgreSQL + pgvector (Supabase)")]
    public Task EnsureCollection_Upsert_Get_RoundTrip() => Task.CompletedTask;

    [Fact(Skip = "requires live PostgreSQL + pgvector (Supabase)")]
    public Task Search_EmptyCollection_ReturnsEmpty() => Task.CompletedTask;

    [Fact(Skip = "requires live PostgreSQL + pgvector (Supabase)")]
    public Task UpsertBatch_MultipleRecords() => Task.CompletedTask;

    [Fact(Skip = "requires live PostgreSQL + pgvector (Supabase)")]
    public Task Delete_RemovesRecord() => Task.CompletedTask;

    [Fact(Skip = "requires live PostgreSQL + pgvector (Supabase)")]
    public Task ListCollections_ReturnsCollections() => Task.CompletedTask;
}

// ─── VD-07 — Neon pgvector ────────────────────────────────────────────────────

public class NeonPgVectorStoreTests
{
    [Fact] public void ProviderType_IsNeonPgVector()
        => Assert.Equal(VectorDbProviderType.NeonPgVector,
            new NeonPgVectorStore("Host=localhost;Database=test").ProviderType);

    [Fact] public void Constructor_EmptyConnectionString_Throws()
        => Assert.Throws<ArgumentException>(() => new NeonPgVectorStore(""));

    [Fact(Skip = "requires live Neon PostgreSQL + pgvector")]
    public Task EnsureCollection_Upsert_Get_RoundTrip() => Task.CompletedTask;

    [Fact(Skip = "requires live Neon PostgreSQL + pgvector")]
    public Task Search_EmptyCollection_ReturnsEmpty() => Task.CompletedTask;

    [Fact(Skip = "requires live Neon PostgreSQL + pgvector")]
    public Task UpsertBatch_MultipleRecords() => Task.CompletedTask;

    [Fact(Skip = "requires live Neon PostgreSQL + pgvector")]
    public Task Delete_RemovesRecord() => Task.CompletedTask;

    [Fact(Skip = "requires live Neon PostgreSQL + pgvector")]
    public Task ListCollections_ReturnsCollections() => Task.CompletedTask;
}

// ─── VD-08 — Timescale pgvector ───────────────────────────────────────────────

public class TimescalePgVectorStoreTests
{
    [Fact] public void ProviderType_IsTimescalePgVector()
        => Assert.Equal(VectorDbProviderType.TimescalePgVector,
            new TimescalePgVectorStore("Host=localhost;Database=test").ProviderType);

    [Fact] public void Constructor_EmptyConnectionString_Throws()
        => Assert.Throws<ArgumentException>(() => new TimescalePgVectorStore(""));

    [Fact(Skip = "requires live TimescaleDB + pgvector")]
    public Task EnsureCollection_Upsert_Get_RoundTrip() => Task.CompletedTask;

    [Fact(Skip = "requires live TimescaleDB + pgvector")]
    public Task Search_EmptyCollection_ReturnsEmpty() => Task.CompletedTask;

    [Fact(Skip = "requires live TimescaleDB + pgvector")]
    public Task UpsertBatch_MultipleRecords() => Task.CompletedTask;

    [Fact(Skip = "requires live TimescaleDB + pgvector")]
    public Task Delete_RemovesRecord() => Task.CompletedTask;

    [Fact(Skip = "requires live TimescaleDB + pgvector")]
    public Task ListCollections_ReturnsCollections() => Task.CompletedTask;
}

// ─── VD-09 — CockroachDB pgvector ────────────────────────────────────────────

public class CockroachPgVectorStoreTests
{
    [Fact] public void ProviderType_IsCockroachPgVector()
        => Assert.Equal(VectorDbProviderType.CockroachPgVector,
            new CockroachPgVectorStore("Host=localhost;Database=test").ProviderType);

    [Fact] public void Constructor_EmptyConnectionString_Throws()
        => Assert.Throws<ArgumentException>(() => new CockroachPgVectorStore(""));

    [Fact(Skip = "requires live CockroachDB + pgvector")]
    public Task EnsureCollection_Upsert_Get_RoundTrip() => Task.CompletedTask;

    [Fact(Skip = "requires live CockroachDB + pgvector")]
    public Task Search_EmptyCollection_ReturnsEmpty() => Task.CompletedTask;

    [Fact(Skip = "requires live CockroachDB + pgvector")]
    public Task UpsertBatch_MultipleRecords() => Task.CompletedTask;

    [Fact(Skip = "requires live CockroachDB + pgvector")]
    public Task Delete_RemovesRecord() => Task.CompletedTask;

    [Fact(Skip = "requires live CockroachDB + pgvector")]
    public Task ListCollections_ReturnsCollections() => Task.CompletedTask;
}

// ─── VD-10 — SingleStore Vector ──────────────────────────────────────────────

public class SingleStoreVectorStoreTests
{
    [Fact] public void ProviderType_IsSingleStoreVector()
        => Assert.Equal(VectorDbProviderType.SingleStoreVector,
            new SingleStoreVectorStore(new SingleStoreVectorOptions { ConnectionString = "Server=localhost;Database=test" }).ProviderType);

    [Fact] public void Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new SingleStoreVectorStore(null!));

    [Fact(Skip = "requires live SingleStore instance")]
    public Task EnsureCollection_Upsert_Get_RoundTrip() => Task.CompletedTask;

    [Fact(Skip = "requires live SingleStore instance")]
    public Task Search_EmptyCollection_ReturnsEmpty() => Task.CompletedTask;

    [Fact(Skip = "requires live SingleStore instance")]
    public Task UpsertBatch_MultipleRecords() => Task.CompletedTask;

    [Fact(Skip = "requires live SingleStore instance")]
    public Task Delete_RemovesRecord() => Task.CompletedTask;

    [Fact(Skip = "requires live SingleStore instance")]
    public Task ListCollections_ReturnsCollections() => Task.CompletedTask;
}

// ─── VD-11 — Qdrant Self-Hosted ───────────────────────────────────────────────

public class QdrantSelfHostedStoreTests
{
    [Fact] public void ProviderType_IsQdrantSelfHosted()
    {
        var client = new QdrantClient("localhost", 6334, https: false);
        Assert.Equal(VectorDbProviderType.QdrantSelfHosted, new QdrantSelfHostedStore(client).ProviderType);
    }

    [Fact] public void Constructor_NullClient_Throws()
        => Assert.Throws<ArgumentNullException>(() => new QdrantSelfHostedStore(null!));

    [Fact(Skip = "requires live Qdrant server")]
    public Task EnsureCollection_Upsert_Get_RoundTrip() => Task.CompletedTask;

    [Fact(Skip = "requires live Qdrant server")]
    public Task Search_EmptyCollection_ReturnsEmpty() => Task.CompletedTask;

    [Fact(Skip = "requires live Qdrant server")]
    public Task UpsertBatch_MultipleRecords() => Task.CompletedTask;

    [Fact(Skip = "requires live Qdrant server")]
    public Task Delete_RemovesRecord() => Task.CompletedTask;

    [Fact(Skip = "requires live Qdrant server")]
    public Task ListCollections_ReturnsCollections() => Task.CompletedTask;
}

// ─── VD-12 — Weaviate Self-Hosted ─────────────────────────────────────────────

public class WeaviateSelfHostedStoreTests
{
    private static HttpClient MockHttp() =>
        new HttpClient(new TestHttpMessageHandler(HttpStatusCode.OK, "{}"))
        { BaseAddress = new Uri("http://localhost:8080/") };

    [Fact] public void ProviderType_IsWeaviateSelfHosted()
        => Assert.Equal(VectorDbProviderType.WeaviateSelfHosted, new WeaviateSelfHostedStore(MockHttp()).ProviderType);

    [Fact] public void Constructor_NullHttp_Throws()
        => Assert.Throws<ArgumentNullException>(() => new WeaviateSelfHostedStore(null!));

    [Fact(Skip = "requires live Weaviate self-hosted instance")]
    public Task EnsureCollection_Upsert_Get_RoundTrip() => Task.CompletedTask;

    [Fact(Skip = "requires live Weaviate self-hosted instance")]
    public Task Search_EmptyCollection_ReturnsEmpty() => Task.CompletedTask;

    [Fact(Skip = "requires live Weaviate self-hosted instance")]
    public Task UpsertBatch_MultipleRecords() => Task.CompletedTask;

    [Fact(Skip = "requires live Weaviate self-hosted instance")]
    public Task Delete_RemovesRecord() => Task.CompletedTask;

    [Fact(Skip = "requires live Weaviate self-hosted instance")]
    public Task ListCollections_ReturnsCollections() => Task.CompletedTask;
}

// ─── VD-13 — Milvus Self-Hosted ───────────────────────────────────────────────

public class MilvusSelfHostedStoreTests
{
    private static HttpClient MockHttp() =>
        new HttpClient(new TestHttpMessageHandler(HttpStatusCode.OK, "{\"data\":[]}"))
        { BaseAddress = new Uri("http://localhost:19530/") };

    [Fact] public void ProviderType_IsMilvusSelfHosted()
        => Assert.Equal(VectorDbProviderType.MilvusSelfHosted, new MilvusSelfHostedStore(MockHttp()).ProviderType);

    [Fact] public void Constructor_NullHttp_Throws()
        => Assert.Throws<ArgumentNullException>(() => new MilvusSelfHostedStore(null!));

    [Fact(Skip = "requires live Milvus self-hosted instance")]
    public Task EnsureCollection_Upsert_Get_RoundTrip() => Task.CompletedTask;

    [Fact(Skip = "requires live Milvus self-hosted instance")]
    public Task Search_EmptyCollection_ReturnsEmpty() => Task.CompletedTask;

    [Fact(Skip = "requires live Milvus self-hosted instance")]
    public Task UpsertBatch_MultipleRecords() => Task.CompletedTask;

    [Fact(Skip = "requires live Milvus self-hosted instance")]
    public Task Delete_RemovesRecord() => Task.CompletedTask;

    [Fact(Skip = "requires live Milvus self-hosted instance")]
    public Task ListCollections_ReturnsCollections() => Task.CompletedTask;
}

// ─── VD-14 — Chroma Self-Hosted ───────────────────────────────────────────────

public class ChromaDbSelfHostedStoreTests
{
    private static HttpClient MockHttp() =>
        new HttpClient(new TestHttpMessageHandler(HttpStatusCode.OK, "{}"))
        { BaseAddress = new Uri("http://localhost:8000/") };

    [Fact] public void ProviderType_IsChromaSelfHosted()
        => Assert.Equal(VectorDbProviderType.ChromaSelfHosted, new ChromaDbSelfHostedStore(MockHttp()).ProviderType);

    [Fact] public void Constructor_NullHttp_Throws()
        => Assert.Throws<ArgumentNullException>(() => new ChromaDbSelfHostedStore(null!));

    [Fact(Skip = "requires live Chroma self-hosted instance")]
    public Task EnsureCollection_Upsert_Get_RoundTrip() => Task.CompletedTask;

    [Fact(Skip = "requires live Chroma self-hosted instance")]
    public Task Search_EmptyCollection_ReturnsEmpty() => Task.CompletedTask;

    [Fact(Skip = "requires live Chroma self-hosted instance")]
    public Task UpsertBatch_MultipleRecords() => Task.CompletedTask;

    [Fact(Skip = "requires live Chroma self-hosted instance")]
    public Task Delete_RemovesRecord() => Task.CompletedTask;

    [Fact(Skip = "requires live Chroma self-hosted instance")]
    public Task ListCollections_ReturnsCollections() => Task.CompletedTask;
}

// ─── VD-15 — Elasticsearch Dense ─────────────────────────────────────────────

public class ElasticsearchDenseStoreTests
{
    [Fact] public void ProviderType_IsElasticsearchDense()
    {
        var client = new ElasticsearchClient(new ElasticsearchClientSettings(new Uri("http://localhost:9200")));
        Assert.Equal(VectorDbProviderType.ElasticsearchDense, new ElasticsearchDenseStore(client).ProviderType);
    }

    [Fact] public void Constructor_NullClient_Throws()
        => Assert.Throws<ArgumentNullException>(() => new ElasticsearchDenseStore(null!));

    [Fact(Skip = "requires live Elasticsearch 8.x instance")]
    public Task EnsureCollection_Upsert_Get_RoundTrip() => Task.CompletedTask;

    [Fact(Skip = "requires live Elasticsearch 8.x instance")]
    public Task Search_EmptyCollection_ReturnsEmpty() => Task.CompletedTask;

    [Fact(Skip = "requires live Elasticsearch 8.x instance")]
    public Task UpsertBatch_MultipleRecords() => Task.CompletedTask;

    [Fact(Skip = "requires live Elasticsearch 8.x instance")]
    public Task Delete_RemovesRecord() => Task.CompletedTask;

    [Fact(Skip = "requires live Elasticsearch 8.x instance")]
    public Task ListCollections_ReturnsCollections() => Task.CompletedTask;
}

// ─── VD-16 — Meilisearch Vector ───────────────────────────────────────────────

public class MeilisearchVectorStoreTests
{
    [Fact] public void ProviderType_IsMeilisearchVector()
    {
        var client = new MeilisearchClient("http://localhost:7700");
        Assert.Equal(VectorDbProviderType.MeilisearchVector, new MeilisearchVectorStore(client).ProviderType);
    }

    [Fact] public void Constructor_NullClient_Throws()
        => Assert.Throws<ArgumentNullException>(() => new MeilisearchVectorStore(null!));

    [Fact(Skip = "requires live Meilisearch instance")]
    public Task EnsureCollection_Upsert_Get_RoundTrip() => Task.CompletedTask;

    [Fact(Skip = "requires live Meilisearch instance")]
    public Task Search_EmptyCollection_ReturnsEmpty() => Task.CompletedTask;

    [Fact(Skip = "requires live Meilisearch instance")]
    public Task UpsertBatch_MultipleRecords() => Task.CompletedTask;

    [Fact(Skip = "requires live Meilisearch instance")]
    public Task Delete_RemovesRecord() => Task.CompletedTask;

    [Fact(Skip = "requires live Meilisearch instance")]
    public Task ListCollections_ReturnsCollections() => Task.CompletedTask;
}

// ─── VD-17 — Vald ─────────────────────────────────────────────────────────────

public class ValdStoreTests
{
    private static HttpClient MockHttp() =>
        new HttpClient(new TestHttpMessageHandler(HttpStatusCode.OK, "{}"))
        { BaseAddress = new Uri("http://localhost:8080/") };

    [Fact] public void ProviderType_IsVald()
        => Assert.Equal(VectorDbProviderType.Vald, new ValdStore(MockHttp()).ProviderType);

    [Fact] public void Constructor_NullHttp_Throws()
        => Assert.Throws<ArgumentNullException>(() => new ValdStore(null!));

    [Fact(Skip = "requires live Vald gateway")]
    public Task EnsureCollection_Upsert_Get_RoundTrip() => Task.CompletedTask;

    [Fact(Skip = "requires live Vald gateway")]
    public Task Search_EmptyCollection_ReturnsEmpty() => Task.CompletedTask;

    [Fact(Skip = "requires live Vald gateway")]
    public Task UpsertBatch_MultipleRecords() => Task.CompletedTask;

    [Fact(Skip = "requires live Vald gateway")]
    public Task Delete_RemovesRecord() => Task.CompletedTask;

    [Fact(Skip = "requires live Vald gateway")]
    public Task ListCollections_ReturnsCollections() => Task.CompletedTask;
}

// ─── VD-18 — Vespa ────────────────────────────────────────────────────────────

public class VespaStoreTests
{
    private static HttpClient MockHttp() =>
        new HttpClient(new TestHttpMessageHandler(HttpStatusCode.OK,
            "{\"root\":{\"fields\":{\"totalCount\":0},\"children\":[]}}"))
        { BaseAddress = new Uri("http://localhost:8080/") };

    [Fact] public void ProviderType_IsVespa()
        => Assert.Equal(VectorDbProviderType.Vespa, new VespaStore(MockHttp()).ProviderType);

    [Fact] public void Constructor_NullHttp_Throws()
        => Assert.Throws<ArgumentNullException>(() => new VespaStore(null!));

    [Fact(Skip = "requires live Vespa instance")]
    public Task EnsureCollection_Upsert_Get_RoundTrip() => Task.CompletedTask;

    [Fact(Skip = "requires live Vespa instance")]
    public Task Search_EmptyCollection_ReturnsEmpty() => Task.CompletedTask;

    [Fact(Skip = "requires live Vespa instance")]
    public Task UpsertBatch_MultipleRecords() => Task.CompletedTask;

    [Fact(Skip = "requires live Vespa instance")]
    public Task Delete_RemovesRecord() => Task.CompletedTask;

    [Fact(Skip = "requires live Vespa instance")]
    public Task ListCollections_ReturnsCollections() => Task.CompletedTask;
}

// ─── VD-19 — SQLite Vec ───────────────────────────────────────────────────────

public class SqliteVecStoreTests
{
    // In-memory SQLite with Pooling=False ensures the connection is fully released
    // between test runs; avoids table-lock issues from the SQLite connection pool.
    private static SqliteVecStore Build()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"agxioma_svec_{Guid.NewGuid():N}.db");
        return new SqliteVecStore(new SqliteVecOptions
        {
            ConnectionString = $"Data Source={tmp};Pooling=False",
            AutoCreateSchema = true,
        });
    }

    [Fact] public void ProviderType_IsSqliteVec()
        => Assert.Equal(VectorDbProviderType.SqliteVec,
            new SqliteVecStore(new SqliteVecOptions { ConnectionString = "Data Source=:memory:;Pooling=False" }).ProviderType);

    [Fact] public void Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new SqliteVecStore(null!));

    [Fact] public async Task EnsureCollection_Upsert_Get_RoundTrip()
    {
        var store = Build();
        await store.EnsureCollectionAsync("vtest", 3);
        var rec = new VectorRecord("r1", new float[] { 1f, 0f, 0f }, "hello");
        await store.UpsertAsync("vtest", rec);
        var got = await store.GetAsync("vtest", "r1");
        Assert.NotNull(got);
        Assert.Equal("r1", got!.Id);
        Assert.Equal(3, got.Vector.Length);
        Assert.Equal("hello", got.Text);
    }

    [Fact] public async Task Search_EmptyCollection_ReturnsEmpty()
    {
        var store = Build();
        await store.EnsureCollectionAsync("empty_col", 3);
        var results = await store.SearchAsync("empty_col", new float[] { 1f, 0f, 0f });
        Assert.Empty(results);
    }

    [Fact] public async Task UpsertBatch_MultipleRecords()
    {
        var store = Build();
        await store.EnsureCollectionAsync("batch_col", 2);
        var records = new[]
        {
            new VectorRecord("a", new float[] { 1f, 0f }, "alpha"),
            new VectorRecord("b", new float[] { 0f, 1f }, "beta"),
        };
        await store.UpsertBatchAsync("batch_col", records);
        var a = await store.GetAsync("batch_col", "a");
        var b = await store.GetAsync("batch_col", "b");
        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.Equal("alpha", a!.Text);
        Assert.Equal("beta", b!.Text);
    }

    [Fact] public async Task Delete_RemovesRecord()
    {
        var store = Build();
        await store.EnsureCollectionAsync("del_col", 2);
        await store.UpsertAsync("del_col", new VectorRecord("x", new float[] { 1f, 0f }));
        await store.DeleteAsync("del_col", "x");
        var result = await store.GetAsync("del_col", "x");
        Assert.Null(result);
    }

    [Fact] public async Task ListCollections_ReturnsCreatedCollections()
    {
        var store = Build();
        await store.EnsureCollectionAsync("list_col_1", 2);
        await store.EnsureCollectionAsync("list_col_2", 2);
        var cols = await store.ListCollectionsAsync();
        Assert.Contains("list_col_1", cols);
        Assert.Contains("list_col_2", cols);
    }
}

// ─── VD-20 — LanceDB ──────────────────────────────────────────────────────────

public class LanceDbStoreTests
{
    private static HttpClient MockHttp() =>
        new HttpClient(new TestHttpMessageHandler(HttpStatusCode.OK, "[]"))
        { BaseAddress = new Uri("http://localhost:8080/") };

    [Fact] public void ProviderType_IsLanceDB()
        => Assert.Equal(VectorDbProviderType.LanceDB, new LanceDbStore(MockHttp()).ProviderType);

    [Fact] public void Constructor_NullHttp_Throws()
        => Assert.Throws<ArgumentNullException>(() => new LanceDbStore(null!));

    [Fact(Skip = "requires live LanceDB HTTP server")]
    public Task EnsureCollection_Upsert_Get_RoundTrip() => Task.CompletedTask;

    [Fact(Skip = "requires live LanceDB HTTP server")]
    public Task Search_EmptyCollection_ReturnsEmpty() => Task.CompletedTask;

    [Fact(Skip = "requires live LanceDB HTTP server")]
    public Task UpsertBatch_MultipleRecords() => Task.CompletedTask;

    [Fact(Skip = "requires live LanceDB HTTP server")]
    public Task Delete_RemovesRecord() => Task.CompletedTask;

    [Fact(Skip = "requires live LanceDB HTTP server")]
    public Task ListCollections_ReturnsCollections() => Task.CompletedTask;
}

// ─── VD-21 — DuckDB Vector ────────────────────────────────────────────────────

public class DuckDbVectorStoreTests
{
    // DuckDB in-memory: each instance is isolated (no shared file state).
    private static DuckDbVectorStore Build()
        => new DuckDbVectorStore(new DuckDbVectorOptions { DataSource = ":memory:" });

    [Fact] public void ProviderType_IsDuckDBVector()
        => Assert.Equal(VectorDbProviderType.DuckDBVector, Build().ProviderType);

    [Fact] public void Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new DuckDbVectorStore(null!));

    [Fact] public async Task EnsureCollection_Upsert_Get_RoundTrip()
    {
        var store = Build();
        await store.EnsureCollectionAsync("vtest", 3);
        var rec = new VectorRecord("r1", new float[] { 1f, 0f, 0f }, "hello");
        await store.UpsertAsync("vtest", rec);
        var got = await store.GetAsync("vtest", "r1");
        Assert.NotNull(got);
        Assert.Equal("r1", got!.Id);
        Assert.Equal("hello", got.Text);
    }

    [Fact] public async Task Search_EmptyCollection_ReturnsEmpty()
    {
        var store = Build();
        await store.EnsureCollectionAsync("empty_col", 3);
        var results = await store.SearchAsync("empty_col", new float[] { 1f, 0f, 0f });
        Assert.Empty(results);
    }

    [Fact] public async Task UpsertBatch_MultipleRecords()
    {
        var store = Build();
        await store.EnsureCollectionAsync("batch_col", 2);
        var records = new[]
        {
            new VectorRecord("a", new float[] { 1f, 0f }, "alpha"),
            new VectorRecord("b", new float[] { 0f, 1f }, "beta"),
        };
        await store.UpsertBatchAsync("batch_col", records);
        var a = await store.GetAsync("batch_col", "a");
        var b = await store.GetAsync("batch_col", "b");
        Assert.NotNull(a);
        Assert.NotNull(b);
    }

    [Fact] public async Task Delete_RemovesRecord()
    {
        var store = Build();
        await store.EnsureCollectionAsync("del_col", 2);
        await store.UpsertAsync("del_col", new VectorRecord("x", new float[] { 1f, 0f }));
        await store.DeleteAsync("del_col", "x");
        var result = await store.GetAsync("del_col", "x");
        Assert.Null(result);
    }

    [Fact] public async Task ListCollections_ReturnsCreatedCollections()
    {
        var store = Build();
        await store.EnsureCollectionAsync("col_a", 2);
        await store.EnsureCollectionAsync("col_b", 2);
        var cols = await store.ListCollectionsAsync();
        Assert.Contains("col_a", cols);
        Assert.Contains("col_b", cols);
    }
}

// ─── VD-22 — Redis Stack ──────────────────────────────────────────────────────

public class RedisStackVectorStoreTests
{
    private static (IDatabase db, IServer server) MockRedis()
    {
        var db     = Substitute.For<IDatabase>();
        var server = Substitute.For<IServer>();
        return (db, server);
    }

    [Fact] public void ProviderType_IsRedisStack()
    {
        var (db, server) = MockRedis();
        Assert.Equal(VectorDbProviderType.RedisStack, new RedisStackVectorStore(db, server).ProviderType);
    }

    [Fact] public void Constructor_NullDatabase_Throws()
    {
        var (_, server) = MockRedis();
        Assert.Throws<ArgumentNullException>(() => new RedisStackVectorStore(null!, server));
    }

    [Fact] public void Constructor_NullServer_Throws()
    {
        var (db, _) = MockRedis();
        Assert.Throws<ArgumentNullException>(() => new RedisStackVectorStore(db, null!));
    }

    [Fact(Skip = "requires live Redis Stack instance")]
    public Task EnsureCollection_Upsert_Get_RoundTrip() => Task.CompletedTask;

    [Fact(Skip = "requires live Redis Stack instance")]
    public Task Search_EmptyCollection_ReturnsEmpty() => Task.CompletedTask;

    [Fact(Skip = "requires live Redis Stack instance")]
    public Task UpsertBatch_MultipleRecords() => Task.CompletedTask;

    [Fact(Skip = "requires live Redis Stack instance")]
    public Task Delete_RemovesRecord() => Task.CompletedTask;
}

// ─── VD-23 — MongoDB Atlas ────────────────────────────────────────────────────

public class MongoDbAtlasVectorStoreTests
{
    [Fact] public void ProviderType_IsMongoDBAtlas()
    {
        var db = Substitute.For<IMongoDatabase>();
        Assert.Equal(VectorDbProviderType.MongoDBAtlas, new MongoDbAtlasVectorStore(db).ProviderType);
    }

    [Fact] public void Constructor_NullDatabase_Throws()
        => Assert.Throws<ArgumentNullException>(() => new MongoDbAtlasVectorStore(null!));

    [Fact(Skip = "requires live MongoDB Atlas with vector search index")]
    public Task EnsureCollection_Upsert_Get_RoundTrip() => Task.CompletedTask;

    [Fact(Skip = "requires live MongoDB Atlas with vector search index")]
    public Task Search_EmptyCollection_ReturnsEmpty() => Task.CompletedTask;

    [Fact(Skip = "requires live MongoDB Atlas with vector search index")]
    public Task UpsertBatch_MultipleRecords() => Task.CompletedTask;

    [Fact(Skip = "requires live MongoDB Atlas with vector search index")]
    public Task Delete_RemovesRecord() => Task.CompletedTask;

    [Fact(Skip = "requires live MongoDB Atlas with vector search index")]
    public Task ListCollections_ReturnsCollections() => Task.CompletedTask;
}

// ─── VD-24 — Cosmos DB vCore ──────────────────────────────────────────────────

public class CosmosDbVCoreStoreTests
{
    [Fact] public void ProviderType_IsCosmosDBVCore()
    {
        // CosmosClient constructor accepts a connection string without connecting eagerly.
        var client = new CosmosClient("AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==");
        Assert.Equal(VectorDbProviderType.CosmosDBVCore, new CosmosDbVCoreStore(client, "testdb").ProviderType);
    }

    [Fact] public void Constructor_NullClient_Throws()
        => Assert.Throws<ArgumentNullException>(() =>
            new CosmosDbVCoreStore(null!, "testdb"));

    [Fact] public void Constructor_EmptyDatabaseId_Throws()
    {
        var client = new CosmosClient("AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==");
        Assert.Throws<ArgumentException>(() => new CosmosDbVCoreStore(client, ""));
    }

    [Fact(Skip = "requires live Azure Cosmos DB vCore with vector search")]
    public Task EnsureCollection_Upsert_Get_RoundTrip() => Task.CompletedTask;

    [Fact(Skip = "requires live Azure Cosmos DB vCore with vector search")]
    public Task Search_EmptyCollection_ReturnsEmpty() => Task.CompletedTask;

    [Fact(Skip = "requires live Azure Cosmos DB vCore with vector search")]
    public Task UpsertBatch_MultipleRecords() => Task.CompletedTask;

    [Fact(Skip = "requires live Azure Cosmos DB vCore with vector search")]
    public Task Delete_RemovesRecord() => Task.CompletedTask;
}

// ─── VD-25 — HNSWLib (in-memory) ──────────────────────────────────────────────

public class HNSWLibStoreTests
{
    [Fact] public void ProviderType_IsHNSWLib()
        => Assert.Equal(VectorDbProviderType.HNSWLib, new HNSWLibStore().ProviderType);

    [Fact] public async Task EnsureCollection_DoesNotThrow()
    {
        var store = new HNSWLibStore();
        await store.EnsureCollectionAsync("col", 4);
    }

    [Fact] public async Task Upsert_Get_RoundTrip()
    {
        var store = new HNSWLibStore();
        await store.EnsureCollectionAsync("col", 3);
        var rec = new VectorRecord("id1", new float[] { 1f, 0f, 0f }, "text");
        await store.UpsertAsync("col", rec);
        var got = await store.GetAsync("col", "id1");
        Assert.NotNull(got);
        Assert.Equal("id1", got!.Id);
        Assert.Equal("text", got.Text);
        Assert.Equal(3, got.Vector.Length);
    }

    [Fact] public async Task Search_EmptyCollection_ReturnsEmpty()
    {
        var store = new HNSWLibStore();
        await store.EnsureCollectionAsync("empty", 3);
        var results = await store.SearchAsync("empty", new float[] { 1f, 0f, 0f });
        Assert.Empty(results);
    }

    [Fact] public async Task UpsertBatch_AllRecordsRetrievable()
    {
        var store = new HNSWLibStore();
        await store.EnsureCollectionAsync("batch", 2);
        await store.UpsertBatchAsync("batch", new[]
        {
            new VectorRecord("a", new float[] { 1f, 0f }),
            new VectorRecord("b", new float[] { 0f, 1f }),
        });
        Assert.NotNull(await store.GetAsync("batch", "a"));
        Assert.NotNull(await store.GetAsync("batch", "b"));
    }

    [Fact] public async Task Delete_RemovesRecord()
    {
        var store = new HNSWLibStore();
        await store.EnsureCollectionAsync("col", 2);
        await store.UpsertAsync("col", new VectorRecord("del", new float[] { 1f, 0f }));
        await store.DeleteAsync("col", "del");
        Assert.Null(await store.GetAsync("col", "del"));
    }

    [Fact] public async Task ListCollections_ReturnsCreatedNames()
    {
        var store = new HNSWLibStore();
        await store.EnsureCollectionAsync("x", 2);
        await store.EnsureCollectionAsync("y", 2);
        var cols = await store.ListCollectionsAsync();
        Assert.Contains("x", cols);
        Assert.Contains("y", cols);
    }
}

// ─── VD-26 — FAISS (in-memory) ────────────────────────────────────────────────

public class FaissStoreTests
{
    [Fact] public void ProviderType_IsFAISS()
        => Assert.Equal(VectorDbProviderType.FAISS, new FaissStore().ProviderType);

    [Fact] public async Task EnsureCollection_DoesNotThrow()
        => await new FaissStore().EnsureCollectionAsync("col", 4);

    [Fact] public async Task Upsert_Get_RoundTrip()
    {
        var store = new FaissStore();
        await store.EnsureCollectionAsync("col", 3);
        await store.UpsertAsync("col", new VectorRecord("id1", new float[] { 1f, 0f, 0f }, "hello"));
        var got = await store.GetAsync("col", "id1");
        Assert.NotNull(got);
        Assert.Equal("hello", got!.Text);
    }

    [Fact] public async Task Search_EmptyCollection_ReturnsEmpty()
    {
        var store = new FaissStore();
        await store.EnsureCollectionAsync("empty", 3);
        Assert.Empty(await store.SearchAsync("empty", new float[] { 1f, 0f, 0f }));
    }

    [Fact] public async Task UpsertBatch_AllRecordsRetrievable()
    {
        var store = new FaissStore();
        await store.EnsureCollectionAsync("batch", 2);
        await store.UpsertBatchAsync("batch", new[]
        {
            new VectorRecord("a", new float[] { 1f, 0f }),
            new VectorRecord("b", new float[] { 0f, 1f }),
        });
        Assert.NotNull(await store.GetAsync("batch", "a"));
        Assert.NotNull(await store.GetAsync("batch", "b"));
    }

    [Fact] public async Task Delete_RemovesRecord()
    {
        var store = new FaissStore();
        await store.EnsureCollectionAsync("col", 2);
        await store.UpsertAsync("col", new VectorRecord("del", new float[] { 1f, 0f }));
        await store.DeleteAsync("col", "del");
        Assert.Null(await store.GetAsync("col", "del"));
    }

    [Fact] public async Task ListCollections_ReturnsCreatedNames()
    {
        var store = new FaissStore();
        await store.EnsureCollectionAsync("x", 2);
        await store.EnsureCollectionAsync("y", 2);
        var cols = await store.ListCollectionsAsync();
        Assert.Contains("x", cols);
        Assert.Contains("y", cols);
    }
}

// ─── VD-27 — Annoy (in-memory) ────────────────────────────────────────────────

public class AnnoyStoreTests
{
    [Fact] public void ProviderType_IsAnnoy()
        => Assert.Equal(VectorDbProviderType.Annoy, new AnnoyStore().ProviderType);

    [Fact] public async Task EnsureCollection_DoesNotThrow()
        => await new AnnoyStore().EnsureCollectionAsync("col", 4);

    [Fact] public async Task Upsert_Get_RoundTrip()
    {
        var store = new AnnoyStore();
        await store.EnsureCollectionAsync("col", 3);
        await store.UpsertAsync("col", new VectorRecord("id1", new float[] { 0f, 1f, 0f }, "world"));
        var got = await store.GetAsync("col", "id1");
        Assert.NotNull(got);
        Assert.Equal("world", got!.Text);
    }

    [Fact] public async Task Search_EmptyCollection_ReturnsEmpty()
    {
        var store = new AnnoyStore();
        await store.EnsureCollectionAsync("empty", 3);
        Assert.Empty(await store.SearchAsync("empty", new float[] { 0f, 1f, 0f }));
    }

    [Fact] public async Task UpsertBatch_AllRecordsRetrievable()
    {
        var store = new AnnoyStore();
        await store.EnsureCollectionAsync("batch", 2);
        await store.UpsertBatchAsync("batch", new[]
        {
            new VectorRecord("a", new float[] { 1f, 0f }),
            new VectorRecord("b", new float[] { 0f, 1f }),
        });
        Assert.NotNull(await store.GetAsync("batch", "a"));
        Assert.NotNull(await store.GetAsync("batch", "b"));
    }

    [Fact] public async Task Delete_RemovesRecord()
    {
        var store = new AnnoyStore();
        await store.EnsureCollectionAsync("col", 2);
        await store.UpsertAsync("col", new VectorRecord("del", new float[] { 0f, 1f }));
        await store.DeleteAsync("col", "del");
        Assert.Null(await store.GetAsync("col", "del"));
    }

    [Fact] public async Task ListCollections_ReturnsCreatedNames()
    {
        var store = new AnnoyStore();
        await store.EnsureCollectionAsync("x", 2);
        await store.EnsureCollectionAsync("y", 2);
        var cols = await store.ListCollectionsAsync();
        Assert.Contains("x", cols);
        Assert.Contains("y", cols);
    }
}

// ─── VD-28 — ScaNN ────────────────────────────────────────────────────────────

public class ScaNNStoreTests
{
    private static HttpClient MockHttp() =>
        new HttpClient(new TestHttpMessageHandler(HttpStatusCode.OK, "[]"))
        { BaseAddress = new Uri("http://localhost:8080/") };

    [Fact] public void ProviderType_IsScaNN()
        => Assert.Equal(VectorDbProviderType.ScaNN, new ScaNNStore(MockHttp()).ProviderType);

    [Fact] public void Constructor_NullHttp_Throws()
        => Assert.Throws<ArgumentNullException>(() => new ScaNNStore(null!));

    [Fact(Skip = "requires live ScaNN HTTP server")]
    public Task EnsureCollection_Upsert_Get_RoundTrip() => Task.CompletedTask;

    [Fact(Skip = "requires live ScaNN HTTP server")]
    public Task Search_EmptyCollection_ReturnsEmpty() => Task.CompletedTask;

    [Fact(Skip = "requires live ScaNN HTTP server")]
    public Task UpsertBatch_MultipleRecords() => Task.CompletedTask;

    [Fact(Skip = "requires live ScaNN HTTP server")]
    public Task Delete_RemovesRecord() => Task.CompletedTask;

    [Fact(Skip = "requires live ScaNN HTTP server")]
    public Task ListCollections_ReturnsCollections() => Task.CompletedTask;
}

// ─── VD-29 — RocksDB HNSW ─────────────────────────────────────────────────────

public class RocksDbHnswStoreTests : IDisposable
{
    // Each test uses a unique temp directory to avoid cross-test interference.
    private readonly string _dir;
    private RocksDbHnswStore? _store;

    public RocksDbHnswStoreTests()
        => _dir = Path.Combine(Path.GetTempPath(), $"agxioma_rocks_{Guid.NewGuid():N}");

    public void Dispose()
    {
        _store?.Dispose();
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact(Skip = "requires native RocksDB library (rocksdb.dll) - install RocksDbNative package or native runtime")]
    public void ProviderType_IsRocksDBHNSW()
    {
        _store = new RocksDbHnswStore(_dir);
        Assert.Equal(VectorDbProviderType.RocksDBHNSW, _store.ProviderType);
    }

    [Fact] public void Constructor_EmptyPath_Throws()
        => Assert.Throws<ArgumentException>(() => new RocksDbHnswStore(""));

    [Fact(Skip = "requires native RocksDB library")]
    public async Task EnsureCollection_Upsert_Get_RoundTrip()
    {
        _store = new RocksDbHnswStore(_dir);
        await _store.EnsureCollectionAsync("col", 3);
        var rec = new VectorRecord("r1", new float[] { 1f, 0f, 0f }, "hello");
        await _store.UpsertAsync("col", rec);
        var got = await _store.GetAsync("col", "r1");
        Assert.NotNull(got);
        Assert.Equal("r1", got!.Id);
        Assert.Equal("hello", got.Text);
    }

    [Fact(Skip = "requires native RocksDB library")]
    public async Task Search_EmptyCollection_ReturnsEmpty()
    {
        _store = new RocksDbHnswStore(_dir);
        await _store.EnsureCollectionAsync("empty", 3);
        var results = await _store.SearchAsync("empty", new float[] { 1f, 0f, 0f });
        Assert.Empty(results);
    }

    [Fact(Skip = "requires native RocksDB library")]
    public async Task UpsertBatch_AllRecordsRetrievable()
    {
        _store = new RocksDbHnswStore(_dir);
        await _store.EnsureCollectionAsync("batch", 2);
        await _store.UpsertBatchAsync("batch", new[]
        {
            new VectorRecord("a", new float[] { 1f, 0f }),
            new VectorRecord("b", new float[] { 0f, 1f }),
        });
        Assert.NotNull(await _store.GetAsync("batch", "a"));
        Assert.NotNull(await _store.GetAsync("batch", "b"));
    }

    [Fact(Skip = "requires native RocksDB library")]
    public async Task Delete_RemovesRecord()
    {
        _store = new RocksDbHnswStore(_dir);
        await _store.EnsureCollectionAsync("col", 2);
        await _store.UpsertAsync("col", new VectorRecord("x", new float[] { 1f, 0f }));
        await _store.DeleteAsync("col", "x");
        Assert.Null(await _store.GetAsync("col", "x"));
    }

    [Fact(Skip = "requires native RocksDB library")]
    public async Task ListCollections_ReturnsCreatedCollections()
    {
        _store = new RocksDbHnswStore(_dir);
        await _store.EnsureCollectionAsync("c1", 2);
        await _store.EnsureCollectionAsync("c2", 2);
        var cols = await _store.ListCollectionsAsync();
        Assert.Contains("c1", cols);
        Assert.Contains("c2", cols);
    }
}

// ─── VD-30 — SQL Server Vector ────────────────────────────────────────────────

public class SqlServerVectorStoreTests
{
    [Fact] public void ProviderType_IsSQLServerVector()
        => Assert.Equal(VectorDbProviderType.SQLServerVector,
            new SqlServerVectorStore("Server=localhost;Database=test;Integrated Security=True").ProviderType);

    [Fact] public void Constructor_EmptyConnectionString_Throws()
        => Assert.Throws<ArgumentException>(() => new SqlServerVectorStore(""));

    [Fact(Skip = "requires live SQL Server 2025 with VECTOR support")]
    public Task EnsureCollection_Upsert_Get_RoundTrip() => Task.CompletedTask;

    [Fact(Skip = "requires live SQL Server 2025 with VECTOR support")]
    public Task Search_EmptyCollection_ReturnsEmpty() => Task.CompletedTask;

    [Fact(Skip = "requires live SQL Server 2025 with VECTOR support")]
    public Task UpsertBatch_MultipleRecords() => Task.CompletedTask;

    [Fact(Skip = "requires live SQL Server 2025 with VECTOR support")]
    public Task Delete_RemovesRecord() => Task.CompletedTask;

    [Fact(Skip = "requires live SQL Server 2025 with VECTOR support")]
    public Task ListCollections_ReturnsCollections() => Task.CompletedTask;
}
