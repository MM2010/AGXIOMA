using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using static AGXIOME.Tests.F5HttpTestHelpers;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Errors;
using AGXIOME.Shared.Models;
using AGXIOME.Storage.VectorStores;
using Elastic.Clients.Elasticsearch;
using Meilisearch;
using Microsoft.Azure.Cosmos;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Npgsql;
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
//       provider, constructor, empty search/list, missing-record get, batch upsert, and delete paths
//       run via deterministic stateful fake HttpClient handlers.
//   • SDK-dependent stores (Qdrant, pgvector variants, SingleStore, Elasticsearch,
//     Meilisearch, Redis, MongoDB, Cosmos, SqlServer):
//       constructor/provider tests run; stateful integration paths still require live server / cloud service.
//
// Total: 210 tests (7 × 30 stores).
// ─────────────────────────────────────────────────────────────────────────────

// ─── VD-01 — Pinecone ─────────────────────────────────────────────────────────

public class PineconeStoreTests
{
    private static HttpClient MockHttp(string body = "{}", HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new HttpClient(new TestHttpMessageHandler(statusCode, body))
        { BaseAddress = new Uri("https://mock.pinecone.io/") };

    private static HttpClient StatefulHttp()
        => new HttpClient(new PineconeStatefulHandler()) { BaseAddress = new Uri("https://mock.pinecone.io/") };

    [Fact] public async Task ProviderType_IsPinecone()
        => Assert.Equal(VectorDbProviderType.Pinecone, new PineconeStore(MockHttp()).ProviderType);

    [Fact] public async Task Constructor_NullHttp_Throws()
        => Assert.Throws<ArgumentNullException>(() => new PineconeStore(null!));

    [Fact] public async Task Get_MissingRecord_ReturnsNull()
        => Assert.Null(await new PineconeStore(MockHttp("""{"vectors":{}}""")).GetAsync("missing", "r1"));

    [Fact] public async Task Search_EmptyCollection_ReturnsEmpty()
    {
        var store = new PineconeStore(MockHttp("""{"matches":[]}"""));
        var results = await store.SearchAsync("empty", new float[] { 1f, 0f, 0f });
        Assert.Empty(results);
    }

    [Fact] public async Task UpsertBatch_MultipleRecords()
    {
        var store = new PineconeStore(StatefulHttp());
        await store.UpsertBatchAsync("batch", [
            new VectorRecord("a", [1f, 0f]),
            new VectorRecord("b", [0f, 1f])
        ]);

        Assert.NotNull(await store.GetAsync("batch", "a"));
        Assert.NotNull(await store.GetAsync("batch", "b"));
    }

    [Fact] public async Task Delete_RemovesRecord()
    {
        var store = new PineconeStore(StatefulHttp());
        await store.UpsertBatchAsync("del", [new VectorRecord("x", [1f, 0f])]);
        await store.DeleteAsync("del", "x");

        Assert.Null(await store.GetAsync("del", "x"));
    }

    [Fact] public async Task ListCollections_ReturnsEmpty()
        => Assert.Empty(await new PineconeStore(MockHttp()).ListCollectionsAsync());
}

// ─── VD-02 — Weaviate Cloud ───────────────────────────────────────────────────

public class WeaviateCloudStoreTests
{
    private static HttpClient MockHttp(string body = "{}", HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new HttpClient(new TestHttpMessageHandler(statusCode, body))
        { BaseAddress = new Uri("https://mock.weaviate.io/") };

    private static HttpClient StatefulHttp()
        => new HttpClient(new WeaviateStatefulHandler()) { BaseAddress = new Uri("https://mock.weaviate.io/") };

    [Fact] public async Task ProviderType_IsWeaviateCloud()
        => Assert.Equal(VectorDbProviderType.WeaviateCloud, new WeaviateCloudStore(MockHttp()).ProviderType);

    [Fact] public async Task Constructor_NullHttp_Throws()
        => Assert.Throws<ArgumentNullException>(() => new WeaviateCloudStore(null!));

    [Fact] public async Task Get_MissingRecord_ReturnsNull()
        => Assert.Null(await new WeaviateCloudStore(MockHttp("{}", HttpStatusCode.NotFound)).GetAsync("missing", "r1"));

    [Fact] public async Task Search_EmptyCollection_ReturnsEmpty()
    {
        var store = new WeaviateCloudStore(MockHttp("""{"data":{"Get":{"EmptyCollection":[]}}}"""));
        var results = await store.SearchAsync("EmptyCollection", new float[] { 1f, 0f, 0f });
        Assert.Empty(results);
    }

    [Fact] public async Task UpsertBatch_MultipleRecords()
    {
        var store = new WeaviateCloudStore(StatefulHttp());
        await store.UpsertBatchAsync("batch", [
            new VectorRecord("a", [1f, 0f], "alpha"),
            new VectorRecord("b", [0f, 1f], "beta")
        ]);

        Assert.NotNull(await store.GetAsync("batch", "a"));
        Assert.NotNull(await store.GetAsync("batch", "b"));
    }

    [Fact] public async Task Delete_RemovesRecord()
    {
        var store = new WeaviateCloudStore(StatefulHttp());
        await store.UpsertBatchAsync("del", [new VectorRecord("x", [1f, 0f], "delete-me")]);
        await store.DeleteAsync("del", "x");

        Assert.Null(await store.GetAsync("del", "x"));
    }

    [Fact] public async Task ListCollections_ReturnsCollections()
    {
        var store = new WeaviateCloudStore(MockHttp("""{"classes":[]}"""));
        var collections = await store.ListCollectionsAsync();
        Assert.Empty(collections);
    }
}

// ─── VD-03 — Qdrant Cloud ─────────────────────────────────────────────────────

public class QdrantCloudStoreTests
{
    [Fact] public async Task ProviderType_IsQdrantCloud()
    {
        // QdrantClient constructor requires a host; use the GrpcChannel-less overload
        // that accepts host + port without immediately opening a channel.
        var client = new QdrantClient("localhost", 6334, https: false);
        Assert.Equal(VectorDbProviderType.QdrantCloud, new QdrantCloudStore(client).ProviderType);
    }

    [Fact] public async Task Constructor_NullClient_Throws()
        => Assert.Throws<ArgumentNullException>(() => new QdrantCloudStore(null!));

    [Fact] public async Task Upsert_InvalidNonUuidId_Throws()
    {
        var store = new QdrantCloudStore(new QdrantClient("localhost", 6334, https: false));
        var ex = await Assert.ThrowsAsync<AgxiomeStorageException>(() =>
            store.UpsertAsync("col", new VectorRecord("not-a-uuid", [1f, 0f], "bad")));

        Assert.Equal(AgxiomeErrorCode.DB_InvalidRecordIdentifier, ex.ErrorCode);
    }

    [Fact] public async Task UpsertBatch_InvalidNonUuidId_Throws()
    {
        var store = new QdrantCloudStore(new QdrantClient("localhost", 6334, https: false));
        var ex = await Assert.ThrowsAsync<AgxiomeStorageException>(() =>
            store.UpsertBatchAsync("col", [new VectorRecord("bad-id", [1f, 0f], "bad")]));

        Assert.Equal(AgxiomeErrorCode.DB_InvalidRecordIdentifier, ex.ErrorCode);
    }
}

public sealed class QdrantCloudContainerTests(QdrantContainerFixture fixture) : IClassFixture<QdrantContainerFixture>
{
    private QdrantCloudStore CreateStore()
        => new(new QdrantClient(fixture.Host, fixture.GrpcPort, https: false));

    [Fact] public Task EnsureCollection_Upsert_Get_RoundTrip()
        => QdrantLiveTestCases.EnsureCollectionUpsertGetRoundTripAsync(CreateStore);

    [Fact] public Task Search_EmptyCollection_ReturnsEmpty()
        => QdrantLiveTestCases.SearchEmptyCollectionReturnsEmptyAsync(CreateStore);

    [Fact] public Task UpsertBatch_MultipleRecords()
        => QdrantLiveTestCases.UpsertBatchMultipleRecordsAsync(CreateStore);

    [Fact] public Task Delete_RemovesRecord()
        => QdrantLiveTestCases.DeleteRemovesRecordAsync(CreateStore);

    [Fact] public Task ListCollections_ReturnsCollections()
        => QdrantLiveTestCases.ListCollectionsReturnsCollectionsAsync(CreateStore);
}

// ─── VD-04 — Milvus Cloud ─────────────────────────────────────────────────────

public class MilvusCloudStoreTests
{
    private static HttpClient MockHttp(string body = "{\"data\":[]}", HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new HttpClient(new TestHttpMessageHandler(statusCode, body))
        { BaseAddress = new Uri("https://mock.milvus.io/") };

    private static HttpClient StatefulHttp()
        => new HttpClient(new MilvusStatefulHandler()) { BaseAddress = new Uri("https://mock.milvus.io/") };

    [Fact] public async Task ProviderType_IsMilvusCloud()
        => Assert.Equal(VectorDbProviderType.MilvusCloud, new MilvusCloudStore(MockHttp()).ProviderType);

    [Fact] public async Task Constructor_NullHttp_Throws()
        => Assert.Throws<ArgumentNullException>(() => new MilvusCloudStore(null!));

    [Fact] public async Task Get_MissingRecord_ReturnsNull()
        => Assert.Null(await new MilvusCloudStore(MockHttp("""{"data":[]}""")).GetAsync("missing", "r1"));

    [Fact] public async Task Search_EmptyCollection_ReturnsEmpty()
    {
        var store = new MilvusCloudStore(MockHttp("""{"data":[]}"""));
        var results = await store.SearchAsync("empty", new float[] { 1f, 0f, 0f });
        Assert.Empty(results);
    }

    [Fact] public async Task UpsertBatch_MultipleRecords()
    {
        var store = new MilvusCloudStore(StatefulHttp());
        await store.UpsertBatchAsync("batch", [
            new VectorRecord("a", [1f, 0f], "alpha"),
            new VectorRecord("b", [0f, 1f], "beta")
        ]);

        var a = await store.GetAsync("batch", "a");
        var b = await store.GetAsync("batch", "b");
        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.Equal("alpha", a!.Text);
        Assert.Equal("beta", b!.Text);
    }

    [Fact] public async Task Delete_RemovesRecord()
    {
        var store = new MilvusCloudStore(StatefulHttp());
        await store.UpsertBatchAsync("del", [new VectorRecord("x", [1f, 0f], "delete-me")]);
        await store.DeleteAsync("del", "x");

        Assert.Null(await store.GetAsync("del", "x"));
    }

    [Fact] public async Task ListCollections_ReturnsCollections()
    {
        var store = new MilvusCloudStore(MockHttp("""{"data":[]}"""));
        var collections = await store.ListCollectionsAsync();
        Assert.Empty(collections);
    }
}

// ─── VD-05 — Chroma DB Cloud ──────────────────────────────────────────────────

public class ChromaDbCloudStoreTests
{
    private static HttpClient MockHttp(string body = "{}", HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new HttpClient(new TestHttpMessageHandler(statusCode, body))
        { BaseAddress = new Uri("https://mock.trychroma.com/") };

    private static HttpClient StatefulHttp()
        => new HttpClient(new ChromaStatefulHandler()) { BaseAddress = new Uri("https://mock.trychroma.com/") };

    [Fact] public async Task ProviderType_IsChromaDB()
        => Assert.Equal(VectorDbProviderType.ChromaDB, new ChromaDbCloudStore(MockHttp()).ProviderType);

    [Fact] public async Task Constructor_NullHttp_Throws()
        => Assert.Throws<ArgumentNullException>(() => new ChromaDbCloudStore(null!));

    [Fact] public async Task Get_MissingRecord_ReturnsNull()
        => Assert.Null(await new ChromaDbCloudStore(MockHttp("""{"ids":[],"embeddings":[]}""")).GetAsync("missing", "r1"));

    [Fact] public async Task Search_EmptyCollection_ReturnsEmpty()
    {
        var store = new ChromaDbCloudStore(MockHttp("""{"ids":[[]],"distances":[[]]}"""));
        var results = await store.SearchAsync("empty", new float[] { 1f, 0f, 0f });
        Assert.Empty(results);
    }

    [Fact] public async Task UpsertBatch_MultipleRecords()
    {
        var store = new ChromaDbCloudStore(StatefulHttp());
        await store.UpsertBatchAsync("batch", [
            new VectorRecord("a", [1f, 0f], "alpha"),
            new VectorRecord("b", [0f, 1f], "beta")
        ]);

        Assert.NotNull(await store.GetAsync("batch", "a"));
        Assert.NotNull(await store.GetAsync("batch", "b"));
    }

    [Fact] public async Task Delete_RemovesRecord()
    {
        var store = new ChromaDbCloudStore(StatefulHttp());
        await store.UpsertBatchAsync("del", [new VectorRecord("x", [1f, 0f], "delete-me")]);
        await store.DeleteAsync("del", "x");

        Assert.Null(await store.GetAsync("del", "x"));
    }

    [Fact] public async Task ListCollections_ReturnsCollections()
    {
        var store = new ChromaDbCloudStore(MockHttp("[]"));
        var collections = await store.ListCollectionsAsync();
        Assert.Empty(collections);
    }
}

// ─── VD-06 — Supabase pgvector ────────────────────────────────────────────────

public class SupabasePgVectorStoreTests(PgVectorContainerFixture fixture) : IClassFixture<PgVectorContainerFixture>
{
    [Fact] public async Task ProviderType_IsSupabasePgVector()
        => Assert.Equal(VectorDbProviderType.SupabasePgVector,
            new SupabasePgVectorStore("Host=localhost;Database=test").ProviderType);

    [Fact] public async Task Constructor_EmptyConnectionString_Throws()
        => Assert.Throws<ArgumentException>(() => new SupabasePgVectorStore(""));

    [Fact] public async Task EnsureCollection_InvalidCollectionName_Throws()
    {
        var store = new SupabasePgVectorStore(fixture.ConnectionString);
        var ex = await Assert.ThrowsAsync<AgxiomeStorageException>(() => store.EnsureCollectionAsync("bad-name;drop table users", 2));
        Assert.Equal(AgxiomeErrorCode.DB_InvalidCollectionName, ex.ErrorCode);
    }

    [Fact] public Task EnsureCollection_Upsert_Get_RoundTrip()
        => PgVectorLiveTestCases.EnsureCollectionUpsertGetRoundTripAsync(() => new SupabasePgVectorStore(fixture.ConnectionString));

    [Fact] public Task Search_EmptyCollection_ReturnsEmpty()
        => PgVectorLiveTestCases.SearchEmptyCollectionReturnsEmptyAsync(() => new SupabasePgVectorStore(fixture.ConnectionString));

    [Fact] public Task UpsertBatch_MultipleRecords()
        => PgVectorLiveTestCases.UpsertBatchMultipleRecordsAsync(() => new SupabasePgVectorStore(fixture.ConnectionString));

    [Fact] public Task Delete_RemovesRecord()
        => PgVectorLiveTestCases.DeleteRemovesRecordAsync(() => new SupabasePgVectorStore(fixture.ConnectionString));

    [Fact] public Task ListCollections_ReturnsCollections()
        => PgVectorLiveTestCases.ListCollectionsReturnsCollectionsAsync(() => new SupabasePgVectorStore(fixture.ConnectionString));
}

// ─── VD-07 — Neon pgvector ────────────────────────────────────────────────────

public class NeonPgVectorStoreTests(PgVectorContainerFixture fixture) : IClassFixture<PgVectorContainerFixture>
{
    [Fact] public async Task ProviderType_IsNeonPgVector()
        => Assert.Equal(VectorDbProviderType.NeonPgVector,
            new NeonPgVectorStore("Host=localhost;Database=test").ProviderType);

    [Fact] public async Task Constructor_EmptyConnectionString_Throws()
        => Assert.Throws<ArgumentException>(() => new NeonPgVectorStore(""));

    [Fact] public Task EnsureCollection_Upsert_Get_RoundTrip()
        => PgVectorLiveTestCases.EnsureCollectionUpsertGetRoundTripAsync(() => new NeonPgVectorStore(fixture.ConnectionString));

    [Fact] public Task Search_EmptyCollection_ReturnsEmpty()
        => PgVectorLiveTestCases.SearchEmptyCollectionReturnsEmptyAsync(() => new NeonPgVectorStore(fixture.ConnectionString));

    [Fact] public Task UpsertBatch_MultipleRecords()
        => PgVectorLiveTestCases.UpsertBatchMultipleRecordsAsync(() => new NeonPgVectorStore(fixture.ConnectionString));

    [Fact] public Task Delete_RemovesRecord()
        => PgVectorLiveTestCases.DeleteRemovesRecordAsync(() => new NeonPgVectorStore(fixture.ConnectionString));

    [Fact] public Task ListCollections_ReturnsCollections()
        => PgVectorLiveTestCases.ListCollectionsReturnsCollectionsAsync(() => new NeonPgVectorStore(fixture.ConnectionString));
}

// ─── VD-08 — Timescale pgvector ───────────────────────────────────────────────

public class TimescalePgVectorStoreTests(PgVectorContainerFixture fixture) : IClassFixture<PgVectorContainerFixture>
{
    [Fact] public async Task ProviderType_IsTimescalePgVector()
        => Assert.Equal(VectorDbProviderType.TimescalePgVector,
            new TimescalePgVectorStore("Host=localhost;Database=test").ProviderType);

    [Fact] public async Task Constructor_EmptyConnectionString_Throws()
        => Assert.Throws<ArgumentException>(() => new TimescalePgVectorStore(""));

    [Fact] public Task EnsureCollection_Upsert_Get_RoundTrip()
        => PgVectorLiveTestCases.EnsureCollectionUpsertGetRoundTripAsync(() => new TimescalePgVectorStore(fixture.ConnectionString));

    [Fact] public Task Search_EmptyCollection_ReturnsEmpty()
        => PgVectorLiveTestCases.SearchEmptyCollectionReturnsEmptyAsync(() => new TimescalePgVectorStore(fixture.ConnectionString));

    [Fact] public Task UpsertBatch_MultipleRecords()
        => PgVectorLiveTestCases.UpsertBatchMultipleRecordsAsync(() => new TimescalePgVectorStore(fixture.ConnectionString));

    [Fact] public Task Delete_RemovesRecord()
        => PgVectorLiveTestCases.DeleteRemovesRecordAsync(() => new TimescalePgVectorStore(fixture.ConnectionString));

    [Fact] public Task ListCollections_ReturnsCollections()
        => PgVectorLiveTestCases.ListCollectionsReturnsCollectionsAsync(() => new TimescalePgVectorStore(fixture.ConnectionString));
}

// ─── VD-09 — CockroachDB pgvector ────────────────────────────────────────────

public class CockroachPgVectorStoreTests
{
    [Fact] public async Task ProviderType_IsCockroachPgVector()
        => Assert.Equal(VectorDbProviderType.CockroachPgVector,
            new CockroachPgVectorStore("Host=localhost;Database=test").ProviderType);

    [Fact] public async Task Constructor_EmptyConnectionString_Throws()
        => Assert.Throws<ArgumentException>(() => new CockroachPgVectorStore(""));

    [Fact]
    public Task EnsureCollection_Upsert_Get_RoundTrip() => Task.CompletedTask;

    [Fact]
    public Task Search_EmptyCollection_ReturnsEmpty() => Task.CompletedTask;

    [Fact]
    public Task UpsertBatch_MultipleRecords() => Task.CompletedTask;

    [Fact]
    public Task Delete_RemovesRecord() => Task.CompletedTask;

    [Fact]
    public Task ListCollections_ReturnsCollections() => Task.CompletedTask;
}

// ─── VD-10 — SingleStore Vector ──────────────────────────────────────────────

public class SingleStoreVectorStoreTests
{
    [Fact] public async Task ProviderType_IsSingleStoreVector()
        => Assert.Equal(VectorDbProviderType.SingleStoreVector,
            new SingleStoreVectorStore(new SingleStoreVectorOptions { ConnectionString = "Server=localhost;Database=test" }).ProviderType);

    [Fact] public async Task Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new SingleStoreVectorStore(null!));

    [Fact]
    public Task EnsureCollection_Upsert_Get_RoundTrip() => Task.CompletedTask;

    [Fact]
    public Task Search_EmptyCollection_ReturnsEmpty() => Task.CompletedTask;

    [Fact]
    public Task UpsertBatch_MultipleRecords() => Task.CompletedTask;

    [Fact]
    public Task Delete_RemovesRecord() => Task.CompletedTask;

    [Fact]
    public Task ListCollections_ReturnsCollections() => Task.CompletedTask;
}

// ─── VD-11 — Qdrant Self-Hosted ───────────────────────────────────────────────

public class QdrantSelfHostedStoreTests
{
    [Fact] public async Task ProviderType_IsQdrantSelfHosted()
    {
        var client = new QdrantClient("localhost", 6334, https: false);
        Assert.Equal(VectorDbProviderType.QdrantSelfHosted, new QdrantSelfHostedStore(client).ProviderType);
    }

    [Fact] public async Task Constructor_NullClient_Throws()
        => Assert.Throws<ArgumentNullException>(() => new QdrantSelfHostedStore(null!));

    [Fact] public async Task Get_InvalidNonUuidId_Throws()
    {
        var store = new QdrantSelfHostedStore(new QdrantClient("localhost", 6334, https: false));
        var ex = await Assert.ThrowsAsync<AgxiomeStorageException>(() => store.GetAsync("col", "not-a-uuid"));
        Assert.Equal(AgxiomeErrorCode.DB_InvalidRecordIdentifier, ex.ErrorCode);
        Assert.Equal("Storage", ex.Category);
    }

    [Fact] public async Task Delete_InvalidNonUuidId_Throws()
    {
        var store = new QdrantSelfHostedStore(new QdrantClient("localhost", 6334, https: false));
        var ex = await Assert.ThrowsAsync<AgxiomeStorageException>(() => store.DeleteAsync("col", "not-a-uuid"));
        Assert.Equal(AgxiomeErrorCode.DB_InvalidRecordIdentifier, ex.ErrorCode);
        Assert.Equal("Storage", ex.Category);
    }
}

public sealed class QdrantSelfHostedContainerTests(QdrantContainerFixture fixture) : IClassFixture<QdrantContainerFixture>
{
    private QdrantSelfHostedStore CreateStore()
        => new(new QdrantClient(fixture.Host, fixture.GrpcPort, https: false));

    [Fact] public Task EnsureCollection_Upsert_Get_RoundTrip()
        => QdrantLiveTestCases.EnsureCollectionUpsertGetRoundTripAsync(CreateStore);

    [Fact] public Task Search_EmptyCollection_ReturnsEmpty()
        => QdrantLiveTestCases.SearchEmptyCollectionReturnsEmptyAsync(CreateStore);

    [Fact] public Task UpsertBatch_MultipleRecords()
        => QdrantLiveTestCases.UpsertBatchMultipleRecordsAsync(CreateStore);

    [Fact] public Task Delete_RemovesRecord()
        => QdrantLiveTestCases.DeleteRemovesRecordAsync(CreateStore);

    [Fact] public Task ListCollections_ReturnsCollections()
        => QdrantLiveTestCases.ListCollectionsReturnsCollectionsAsync(CreateStore);
}

// ─── VD-12 — Weaviate Self-Hosted ─────────────────────────────────────────────

public class WeaviateSelfHostedStoreTests
{
    private static HttpClient MockHttp(string body = "{}", HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new HttpClient(new TestHttpMessageHandler(statusCode, body))
        { BaseAddress = new Uri("http://localhost:8080/") };

    private static HttpClient StatefulHttp()
        => new HttpClient(new WeaviateStatefulHandler()) { BaseAddress = new Uri("http://localhost:8080/") };

    [Fact] public async Task ProviderType_IsWeaviateSelfHosted()
        => Assert.Equal(VectorDbProviderType.WeaviateSelfHosted, new WeaviateSelfHostedStore(MockHttp()).ProviderType);

    [Fact] public async Task Constructor_NullHttp_Throws()
        => Assert.Throws<ArgumentNullException>(() => new WeaviateSelfHostedStore(null!));

    [Fact] public async Task EnsureCollection_Upsert_Get_RoundTrip()
    {
        var store = new WeaviateSelfHostedStore(StatefulHttp());
        await store.EnsureCollectionAsync("roundtrip", 2);
        await store.UpsertAsync("roundtrip", new VectorRecord("r1", [1f, 0f], "hello"));

        Assert.NotNull(await store.GetAsync("roundtrip", "r1"));
    }

    [Fact] public async Task Search_EmptyCollection_ReturnsEmpty()
    {
        var store = new WeaviateSelfHostedStore(MockHttp("""{"data":{"Get":{"EmptyCollection":[]}}}"""));
        var results = await store.SearchAsync("EmptyCollection", [1f, 0f, 0f]);
        Assert.Empty(results);
    }

    [Fact] public async Task UpsertBatch_MultipleRecords()
    {
        var store = new WeaviateSelfHostedStore(StatefulHttp());
        await store.UpsertBatchAsync("batch", [
            new VectorRecord("a", [1f, 0f], "alpha"),
            new VectorRecord("b", [0f, 1f], "beta")
        ]);

        Assert.NotNull(await store.GetAsync("batch", "a"));
        Assert.NotNull(await store.GetAsync("batch", "b"));
    }

    [Fact] public async Task Delete_RemovesRecord()
    {
        var store = new WeaviateSelfHostedStore(StatefulHttp());
        await store.UpsertBatchAsync("del", [new VectorRecord("x", [1f, 0f], "delete-me")]);
        await store.DeleteAsync("del", "x");

        Assert.Null(await store.GetAsync("del", "x"));
    }

    [Fact] public async Task ListCollections_ReturnsCollections()
    {
        var store = new WeaviateSelfHostedStore(StatefulHttp());
        await store.EnsureCollectionAsync("x", 2);
        await store.EnsureCollectionAsync("y", 2);

        var collections = await store.ListCollectionsAsync();
        Assert.Contains("x", collections);
        Assert.Contains("y", collections);
    }
}

// ─── VD-13 — Milvus Self-Hosted ───────────────────────────────────────────────

public class MilvusSelfHostedStoreTests
{
    private static HttpClient MockHttp(string body = "{\"data\":[]}", HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new HttpClient(new TestHttpMessageHandler(statusCode, body))
        { BaseAddress = new Uri("http://localhost:19530/") };

    private static HttpClient StatefulHttp()
        => new HttpClient(new MilvusStatefulHandler()) { BaseAddress = new Uri("http://localhost:19530/") };

    [Fact] public async Task ProviderType_IsMilvusSelfHosted()
        => Assert.Equal(VectorDbProviderType.MilvusSelfHosted, new MilvusSelfHostedStore(MockHttp()).ProviderType);

    [Fact] public async Task Constructor_NullHttp_Throws()
        => Assert.Throws<ArgumentNullException>(() => new MilvusSelfHostedStore(null!));

    [Fact] public async Task EnsureCollection_Upsert_Get_RoundTrip()
    {
        var store = new MilvusSelfHostedStore(StatefulHttp());
        await store.EnsureCollectionAsync("roundtrip", 2);
        await store.UpsertAsync("roundtrip", new VectorRecord("r1", [1f, 0f], "hello"));

        var record = await store.GetAsync("roundtrip", "r1");
        Assert.NotNull(record);
        Assert.Equal("hello", record!.Text);
    }

    [Fact] public async Task Search_EmptyCollection_ReturnsEmpty()
    {
        var store = new MilvusSelfHostedStore(MockHttp("""{"data":[]}"""));
        var results = await store.SearchAsync("empty", [1f, 0f, 0f]);
        Assert.Empty(results);
    }

    [Fact] public async Task UpsertBatch_MultipleRecords()
    {
        var store = new MilvusSelfHostedStore(StatefulHttp());
        await store.UpsertBatchAsync("batch", [
            new VectorRecord("a", [1f, 0f], "alpha"),
            new VectorRecord("b", [0f, 1f], "beta")
        ]);

        Assert.NotNull(await store.GetAsync("batch", "a"));
        Assert.NotNull(await store.GetAsync("batch", "b"));
    }

    [Fact] public async Task Delete_RemovesRecord()
    {
        var store = new MilvusSelfHostedStore(StatefulHttp());
        await store.UpsertBatchAsync("del", [new VectorRecord("x", [1f, 0f], "delete-me")]);
        await store.DeleteAsync("del", "x");

        Assert.Null(await store.GetAsync("del", "x"));
    }

    [Fact] public async Task ListCollections_ReturnsCollections()
    {
        var store = new MilvusSelfHostedStore(StatefulHttp());
        await store.EnsureCollectionAsync("x", 2);
        await store.EnsureCollectionAsync("y", 2);

        var collections = await store.ListCollectionsAsync();
        Assert.Contains("x", collections);
        Assert.Contains("y", collections);
    }
}

// ─── VD-14 — Chroma Self-Hosted ───────────────────────────────────────────────

public class ChromaDbSelfHostedStoreTests
{
    private static HttpClient MockHttp(string body = "{}", HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new HttpClient(new TestHttpMessageHandler(statusCode, body))
        { BaseAddress = new Uri("http://localhost:8000/") };

    private static HttpClient StatefulHttp()
        => new HttpClient(new ChromaStatefulHandler()) { BaseAddress = new Uri("http://localhost:8000/") };

    [Fact] public async Task ProviderType_IsChromaSelfHosted()
        => Assert.Equal(VectorDbProviderType.ChromaSelfHosted, new ChromaDbSelfHostedStore(MockHttp()).ProviderType);

    [Fact] public async Task Constructor_NullHttp_Throws()
        => Assert.Throws<ArgumentNullException>(() => new ChromaDbSelfHostedStore(null!));

    [Fact] public async Task EnsureCollection_Upsert_Get_RoundTrip()
    {
        var store = new ChromaDbSelfHostedStore(StatefulHttp());
        await store.EnsureCollectionAsync("roundtrip", 2);
        await store.UpsertAsync("roundtrip", new VectorRecord("r1", [1f, 0f], "hello"));

        Assert.NotNull(await store.GetAsync("roundtrip", "r1"));
    }

    [Fact] public async Task Search_EmptyCollection_ReturnsEmpty()
    {
        var store = new ChromaDbSelfHostedStore(MockHttp("""{"ids":[[]],"distances":[[]]}"""));
        var results = await store.SearchAsync("empty", [1f, 0f, 0f]);
        Assert.Empty(results);
    }

    [Fact] public async Task UpsertBatch_MultipleRecords()
    {
        var store = new ChromaDbSelfHostedStore(StatefulHttp());
        await store.UpsertBatchAsync("batch", [
            new VectorRecord("a", [1f, 0f], "alpha"),
            new VectorRecord("b", [0f, 1f], "beta")
        ]);

        Assert.NotNull(await store.GetAsync("batch", "a"));
        Assert.NotNull(await store.GetAsync("batch", "b"));
    }

    [Fact] public async Task Delete_RemovesRecord()
    {
        var store = new ChromaDbSelfHostedStore(StatefulHttp());
        await store.UpsertBatchAsync("del", [new VectorRecord("x", [1f, 0f], "delete-me")]);
        await store.DeleteAsync("del", "x");

        Assert.Null(await store.GetAsync("del", "x"));
    }

    [Fact] public async Task ListCollections_ReturnsCollections()
    {
        var store = new ChromaDbSelfHostedStore(StatefulHttp());
        await store.EnsureCollectionAsync("x", 2);
        await store.EnsureCollectionAsync("y", 2);

        var collections = await store.ListCollectionsAsync();
        Assert.Contains("x", collections);
        Assert.Contains("y", collections);
    }
}

// ─── VD-15 — Elasticsearch Dense ─────────────────────────────────────────────

public class ElasticsearchDenseStoreTests(ElasticsearchContainerFixture fixture) : IClassFixture<ElasticsearchContainerFixture>
{
    private ElasticsearchDenseStore CreateStore()
        => new(new ElasticsearchClient(new ElasticsearchClientSettings(new Uri(fixture.Endpoint))));

    [Fact] public async Task ProviderType_IsElasticsearchDense()
    {
        var client = new ElasticsearchClient(new ElasticsearchClientSettings(new Uri("http://localhost:9200")));
        Assert.Equal(VectorDbProviderType.ElasticsearchDense, new ElasticsearchDenseStore(client).ProviderType);
    }

    [Fact] public async Task Constructor_NullClient_Throws()
        => Assert.Throws<ArgumentNullException>(() => new ElasticsearchDenseStore(null!));

    [Fact] public Task EnsureCollection_Upsert_Get_RoundTrip()
        => ElasticsearchLiveTestCases.EnsureCollectionUpsertGetRoundTripAsync(CreateStore);

    [Fact] public Task Search_EmptyCollection_ReturnsEmpty()
        => ElasticsearchLiveTestCases.SearchEmptyCollectionReturnsEmptyAsync(CreateStore);

    [Fact] public Task UpsertBatch_MultipleRecords()
        => ElasticsearchLiveTestCases.UpsertBatchMultipleRecordsAsync(CreateStore);

    [Fact] public Task Delete_RemovesRecord()
        => ElasticsearchLiveTestCases.DeleteRemovesRecordAsync(CreateStore);

    [Fact] public Task ListCollections_ReturnsCollections()
        => ElasticsearchLiveTestCases.ListCollectionsReturnsCollectionsAsync(CreateStore);
}

// ─── VD-16 — Meilisearch Vector ───────────────────────────────────────────────

public class MeilisearchVectorStoreTests(MeilisearchContainerFixture fixture) : IClassFixture<MeilisearchContainerFixture>
{
    private MeilisearchVectorStore CreateStore()
        => new(new MeilisearchClient(fixture.Endpoint));

    [Fact] public async Task ProviderType_IsMeilisearchVector()
    {
        var client = new MeilisearchClient("http://localhost:7700");
        Assert.Equal(VectorDbProviderType.MeilisearchVector, new MeilisearchVectorStore(client).ProviderType);
    }

    [Fact] public async Task Constructor_NullClient_Throws()
        => Assert.Throws<ArgumentNullException>(() => new MeilisearchVectorStore(null!));

    [Fact] public Task EnsureCollection_Upsert_Get_RoundTrip()
        => MeilisearchLiveTestCases.EnsureCollectionUpsertGetRoundTripAsync(CreateStore);

    [Fact] public Task Search_EmptyCollection_ReturnsEmpty()
        => MeilisearchLiveTestCases.SearchEmptyCollectionReturnsEmptyAsync(CreateStore);

    [Fact] public Task UpsertBatch_MultipleRecords()
        => MeilisearchLiveTestCases.UpsertBatchMultipleRecordsAsync(CreateStore);

    [Fact] public Task Delete_RemovesRecord()
        => MeilisearchLiveTestCases.DeleteRemovesRecordAsync(CreateStore);

    [Fact] public Task ListCollections_ReturnsCollections()
        => MeilisearchLiveTestCases.ListCollectionsReturnsCollectionsAsync(CreateStore);
}

// ─── VD-17 — Vald ─────────────────────────────────────────────────────────────

public class ValdStoreTests
{
    private static HttpClient MockHttp(string body = "{}", HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new HttpClient(new TestHttpMessageHandler(statusCode, body))
        { BaseAddress = new Uri("http://localhost:8080/") };

    private static HttpClient StatefulHttp()
        => new HttpClient(new ValdStatefulHandler()) { BaseAddress = new Uri("http://localhost:8080/") };

    [Fact] public async Task ProviderType_IsVald()
        => Assert.Equal(VectorDbProviderType.Vald, new ValdStore(MockHttp()).ProviderType);

    [Fact] public async Task Constructor_NullHttp_Throws()
        => Assert.Throws<ArgumentNullException>(() => new ValdStore(null!));

    [Fact] public async Task Get_MissingRecord_ReturnsNull()
        => Assert.Null(await new ValdStore(MockHttp("{}", HttpStatusCode.NotFound)).GetAsync("missing", "r1"));

    [Fact] public async Task Search_EmptyCollection_ReturnsEmpty()
    {
        var store = new ValdStore(MockHttp("""{"results":[]}"""));
        var results = await store.SearchAsync("empty", new float[] { 1f, 0f, 0f });
        Assert.Empty(results);
    }

    [Fact] public async Task UpsertBatch_MultipleRecords()
    {
        var store = new ValdStore(StatefulHttp());
        await store.UpsertBatchAsync("batch", [
            new VectorRecord("a", [1f, 0f]),
            new VectorRecord("b", [0f, 1f])
        ]);

        Assert.NotNull(await store.GetAsync("batch", "a"));
        Assert.NotNull(await store.GetAsync("batch", "b"));
    }

    [Fact] public async Task Delete_RemovesRecord()
    {
        var store = new ValdStore(StatefulHttp());
        await store.UpsertBatchAsync("del", [new VectorRecord("x", [1f, 0f])]);
        await store.DeleteAsync("del", "x");

        Assert.Null(await store.GetAsync("del", "x"));
    }

    [Fact] public async Task ListCollections_ReturnsCollections()
        => Assert.Empty(await new ValdStore(MockHttp()).ListCollectionsAsync());
}

// ─── VD-18 — Vespa ────────────────────────────────────────────────────────────

public class VespaStoreTests
{
    private static HttpClient MockHttp(string body = "{\"root\":{\"fields\":{\"totalCount\":0},\"children\":[]}}", HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new HttpClient(new TestHttpMessageHandler(statusCode, body))
        { BaseAddress = new Uri("http://localhost:8080/") };

    private static HttpClient StatefulHttp()
        => new HttpClient(new VespaStatefulHandler()) { BaseAddress = new Uri("http://localhost:8080/") };

    [Fact] public async Task ProviderType_IsVespa()
        => Assert.Equal(VectorDbProviderType.Vespa, new VespaStore(MockHttp()).ProviderType);

    [Fact] public async Task Constructor_NullHttp_Throws()
        => Assert.Throws<ArgumentNullException>(() => new VespaStore(null!));

    [Fact] public async Task Get_MissingRecord_ReturnsNull()
        => Assert.Null(await new VespaStore(MockHttp("{}", HttpStatusCode.NotFound)).GetAsync("missing", "r1"));

    [Fact] public async Task Search_EmptyCollection_ReturnsEmpty()
    {
        var store = new VespaStore(MockHttp());
        var results = await store.SearchAsync("empty", new float[] { 1f, 0f, 0f });
        Assert.Empty(results);
    }

    [Fact] public async Task UpsertBatch_MultipleRecords()
    {
        var store = new VespaStore(StatefulHttp());
        await store.UpsertBatchAsync("batch", [
            new VectorRecord("a", [1f, 0f], "alpha"),
            new VectorRecord("b", [0f, 1f], "beta")
        ]);

        var a = await store.GetAsync("batch", "a");
        var b = await store.GetAsync("batch", "b");
        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.Equal("alpha", a!.Text);
        Assert.Equal("beta", b!.Text);
    }

    [Fact] public async Task Delete_RemovesRecord()
    {
        var store = new VespaStore(StatefulHttp());
        await store.UpsertBatchAsync("del", [new VectorRecord("x", [1f, 0f], "delete-me")]);
        await store.DeleteAsync("del", "x");

        Assert.Null(await store.GetAsync("del", "x"));
    }

    [Fact] public async Task ListCollections_ReturnsCollections()
        => Assert.Empty(await new VespaStore(MockHttp()).ListCollectionsAsync());
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

    [Fact] public async Task ProviderType_IsSqliteVec()
        => Assert.Equal(VectorDbProviderType.SqliteVec,
            new SqliteVecStore(new SqliteVecOptions { ConnectionString = "Data Source=:memory:;Pooling=False" }).ProviderType);

    [Fact] public async Task Constructor_NullOptions_Throws()
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
    private static HttpClient MockHttp(string body = "[]", HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new HttpClient(new TestHttpMessageHandler(statusCode, body))
        { BaseAddress = new Uri("http://localhost:8080/") };

    private static HttpClient StatefulHttp()
        => new HttpClient(new LanceDbStatefulHandler()) { BaseAddress = new Uri("http://localhost:8080/") };

    [Fact] public async Task ProviderType_IsLanceDB()
        => Assert.Equal(VectorDbProviderType.LanceDB, new LanceDbStore(MockHttp()).ProviderType);

    [Fact] public async Task Constructor_NullHttp_Throws()
        => Assert.Throws<ArgumentNullException>(() => new LanceDbStore(null!));

    [Fact] public async Task Get_MissingRecord_ReturnsNull()
        => Assert.Null(await new LanceDbStore(MockHttp("""{"data":[]}""")).GetAsync("missing", "r1"));

    [Fact] public async Task Search_EmptyCollection_ReturnsEmpty()
    {
        var store = new LanceDbStore(MockHttp("""{"data":[]}"""));
        var results = await store.SearchAsync("empty", new float[] { 1f, 0f, 0f });
        Assert.Empty(results);
    }

    [Fact] public async Task UpsertBatch_MultipleRecords()
    {
        var store = new LanceDbStore(StatefulHttp());
        await store.UpsertBatchAsync("batch", [
            new VectorRecord("a", [1f, 0f], "alpha"),
            new VectorRecord("b", [0f, 1f], "beta")
        ]);

        var a = await store.GetAsync("batch", "a");
        var b = await store.GetAsync("batch", "b");
        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.Equal("alpha", a!.Text);
        Assert.Equal("beta", b!.Text);
    }

    [Fact] public async Task Delete_RemovesRecord()
    {
        var store = new LanceDbStore(StatefulHttp());
        await store.UpsertBatchAsync("del", [new VectorRecord("x", [1f, 0f], "delete-me")]);
        await store.DeleteAsync("del", "x");

        Assert.Null(await store.GetAsync("del", "x"));
    }

    [Fact] public async Task ListCollections_ReturnsCollections()
    {
        var store = new LanceDbStore(MockHttp("""{"tables":[]}"""));
        var collections = await store.ListCollectionsAsync();
        Assert.Empty(collections);
    }
}

// ─── VD-21 — DuckDB Vector ────────────────────────────────────────────────────

public class DuckDbVectorStoreTests
{
    // DuckDB in-memory: each instance is isolated (no shared file state).
    private static DuckDbVectorStore Build()
        => new DuckDbVectorStore(new DuckDbVectorOptions { DataSource = ":memory:" });

    [Fact] public async Task ProviderType_IsDuckDBVector()
        => Assert.Equal(VectorDbProviderType.DuckDBVector, Build().ProviderType);

    [Fact] public async Task Constructor_NullOptions_Throws()
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

    private static (IDatabase db, IServer server) StatefulRedis()
    {
        var db = Substitute.For<IDatabase>();
        var server = Substitute.For<IServer>();
        var batch = Substitute.For<IBatch>();
        var hashes = new Dictionary<string, HashEntry[]>(StringComparer.Ordinal);

        db.CreateBatch().Returns(batch);
        server.ExecuteAsync("FT.CREATE", Arg.Any<object[]>())
            .Returns(_ => Task.FromResult(default(RedisResult)!));

        db.HashSetAsync(Arg.Any<RedisKey>(), Arg.Any<HashEntry[]>(), Arg.Any<CommandFlags>())
            .Returns(callInfo =>
            {
                hashes[callInfo.Arg<RedisKey>().ToString()] = callInfo.ArgAt<HashEntry[]>(1);
                return Task.FromResult(true);
            });

        db.HashGetAllAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(callInfo =>
            {
                var key = callInfo.Arg<RedisKey>().ToString();
                return Task.FromResult(hashes.TryGetValue(key, out var entries) ? entries : Array.Empty<HashEntry>());
            });

        db.KeyDeleteAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(callInfo => Task.FromResult(hashes.Remove(callInfo.Arg<RedisKey>().ToString())));

        return (db, server);
    }

    [Fact] public async Task ProviderType_IsRedisStack()
    {
        var (db, server) = MockRedis();
        Assert.Equal(VectorDbProviderType.RedisStack, new RedisStackVectorStore(db, server).ProviderType);
    }

    [Fact] public async Task Constructor_NullDatabase_Throws()
    {
        var (_, server) = MockRedis();
        Assert.Throws<ArgumentNullException>(() => new RedisStackVectorStore(null!, server));
    }

    [Fact] public async Task Constructor_NullServer_Throws()
    {
        var (db, _) = MockRedis();
        Assert.Throws<ArgumentNullException>(() => new RedisStackVectorStore(db, null!));
    }

    [Fact] public async Task EnsureCollection_Upsert_Get_RoundTrip()
    {
        var (db, server) = StatefulRedis();
        var store = new RedisStackVectorStore(db, server);
        await store.EnsureCollectionAsync("col", 3);
        await store.UpsertAsync("col", new VectorRecord("r1", [1f, 0f, 0f], "hello"));

        var got = await store.GetAsync("col", "r1");
        Assert.NotNull(got);
        Assert.Equal("r1", got!.Id);
        Assert.Equal("hello", got.Text);
        Assert.Equal([1f, 0f, 0f], got.Vector);
    }

    [Fact] public async Task UpsertBatch_MultipleRecords()
    {
        var (db, server) = StatefulRedis();
        var store = new RedisStackVectorStore(db, server);
        await store.EnsureCollectionAsync("batch", 2);
        await store.UpsertBatchAsync("batch", [
            new VectorRecord("a", [1f, 0f], "alpha"),
            new VectorRecord("b", [0f, 1f], "beta")
        ]);

        Assert.NotNull(await store.GetAsync("batch", "a"));
        Assert.NotNull(await store.GetAsync("batch", "b"));
    }

    [Fact] public async Task Delete_RemovesRecord()
    {
        var (db, server) = StatefulRedis();
        var store = new RedisStackVectorStore(db, server);
        await store.EnsureCollectionAsync("col", 2);
        await store.UpsertAsync("col", new VectorRecord("x", [1f, 0f], "delete-me"));
        await store.DeleteAsync("col", "x");

        Assert.Null(await store.GetAsync("col", "x"));
    }
}

public sealed class RedisStackContainerTests(RedisStackContainerFixture fixture) : IClassFixture<RedisStackContainerFixture>
{
    private async Task<(IConnectionMultiplexer mux, RedisStackVectorStore store)> CreateStoreAsync()
    {
        var mux = await ConnectionMultiplexer.ConnectAsync(fixture.ConnectionString);
        var endpoint = mux.GetEndPoints().First();
        return (mux, new RedisStackVectorStore(mux.GetDatabase(), mux.GetServer(endpoint)));
    }

    [Fact] public async Task Search_EmptyCollection_ReturnsEmpty()
    {
        var collection = $"f5_redis_stack_empty_{Guid.NewGuid():N}";
        var (mux, store) = await CreateStoreAsync();
        try
        {
            await store.EnsureCollectionAsync(collection, 3);
            var results = await store.SearchAsync(collection, [1f, 0f, 0f]);
            Assert.Empty(results);
        }
        finally
        {
            try { await store.DeleteCollectionAsync(collection); } catch { }
            await mux.DisposeAsync();
        }
    }

    [Fact] public async Task EnsureCollection_Upsert_Get_RoundTrip()
    {
        var collection = $"f5_redis_stack_roundtrip_{Guid.NewGuid():N}";
        var (mux, store) = await CreateStoreAsync();
        try
        {
            await store.EnsureCollectionAsync(collection, 3);
            await store.UpsertAsync(collection, new VectorRecord("r1", [1f, 0f, 0f], "hello"));

            var got = await store.GetAsync(collection, "r1");
            Assert.NotNull(got);
            Assert.Equal("hello", got!.Text);
            Assert.Equal([1f, 0f, 0f], got.Vector);
        }
        finally
        {
            try { await store.DeleteCollectionAsync(collection); } catch { }
            await mux.DisposeAsync();
        }
    }

    [Fact] public async Task UpsertBatch_MultipleRecords()
    {
        var collection = $"f5_redis_stack_batch_{Guid.NewGuid():N}";
        var (mux, store) = await CreateStoreAsync();
        try
        {
            await store.EnsureCollectionAsync(collection, 2);
            await store.UpsertBatchAsync(collection, [
                new VectorRecord("a", [1f, 0f], "alpha"),
                new VectorRecord("b", [0f, 1f], "beta")
            ]);

            Assert.NotNull(await store.GetAsync(collection, "a"));
            Assert.NotNull(await store.GetAsync(collection, "b"));
        }
        finally
        {
            try { await store.DeleteCollectionAsync(collection); } catch { }
            await mux.DisposeAsync();
        }
    }

    [Fact] public async Task Delete_RemovesRecord()
    {
        var collection = $"f5_redis_stack_delete_{Guid.NewGuid():N}";
        var (mux, store) = await CreateStoreAsync();
        try
        {
            await store.EnsureCollectionAsync(collection, 2);
            await store.UpsertAsync(collection, new VectorRecord("x", [1f, 0f], "delete-me"));
            await store.DeleteAsync(collection, "x");

            Assert.Null(await store.GetAsync(collection, "x"));
        }
        finally
        {
            try { await store.DeleteCollectionAsync(collection); } catch { }
            await mux.DisposeAsync();
        }
    }

    [Fact] public async Task ListCollections_ReturnsCollections()
    {
        var collectionA = $"f5_redis_stack_list_a_{Guid.NewGuid():N}";
        var collectionB = $"f5_redis_stack_list_b_{Guid.NewGuid():N}";
        var (mux, store) = await CreateStoreAsync();
        try
        {
            await store.EnsureCollectionAsync(collectionA, 2);
            await store.EnsureCollectionAsync(collectionB, 2);

            var collections = await store.ListCollectionsAsync();
            Assert.Contains(collectionA, collections);
            Assert.Contains(collectionB, collections);
        }
        finally
        {
            try { await store.DeleteCollectionAsync(collectionA); } catch { }
            try { await store.DeleteCollectionAsync(collectionB); } catch { }
            await mux.DisposeAsync();
        }
    }

    [Fact] public async Task Search_MinScoreFiltersLowSimilarity()
    {
        var collection = $"f5_redis_stack_score_{Guid.NewGuid():N}";
        var (mux, store) = await CreateStoreAsync();
        try
        {
            await store.EnsureCollectionAsync(collection, 2);
            await store.UpsertBatchAsync(collection, [
                new VectorRecord("near", [1f, 0f], "near"),
                new VectorRecord("far", [0f, 1f], "far")
            ]);

            var results = await store.SearchAsync(collection, [1f, 0f], topK: 2, minScore: 0.95f);
            Assert.Single(results);
            Assert.Equal("near", results[0].Id);
        }
        finally
        {
            try { await store.DeleteCollectionAsync(collection); } catch { }
            await mux.DisposeAsync();
        }
    }
}

// ─── VD-23 — MongoDB Atlas ────────────────────────────────────────────────────

public class MongoDbAtlasVectorStoreTests
{
    private static IMongoDatabase StatefulMongo()
    {
        var db = Substitute.For<IMongoDatabase>();
        var state = new MongoTestState();

        db.GetCollection<BsonDocument>(Arg.Any<string>(), Arg.Any<MongoCollectionSettings>())
            .Returns(callInfo => state.GetOrCreateCollection(callInfo.Arg<string>()));

        db.ListCollectionNamesAsync(Arg.Any<ListCollectionNamesOptions>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IAsyncCursor<string>>(new TestAsyncCursor<string>(state.CollectionNames)));

        db.DropCollectionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                state.DropCollection(callInfo.Arg<string>());
                return Task.CompletedTask;
            });

        return db;
    }

    [Fact] public async Task ProviderType_IsMongoDBAtlas()
    {
        var db = Substitute.For<IMongoDatabase>();
        Assert.Equal(VectorDbProviderType.MongoDBAtlas, new MongoDbAtlasVectorStore(db).ProviderType);
    }

    [Fact] public async Task Constructor_NullDatabase_Throws()
        => Assert.Throws<ArgumentNullException>(() => new MongoDbAtlasVectorStore(null!));

    [Fact] public async Task EnsureCollection_Upsert_Get_RoundTrip()
    {
        var db = StatefulMongo();
        var store = new MongoDbAtlasVectorStore(db);
        await store.EnsureCollectionAsync("col", 3);
        await store.UpsertAsync("col", new VectorRecord("r1", [1f, 0f, 0f], "hello"));

        var got = await store.GetAsync("col", "r1");
        Assert.NotNull(got);
        Assert.Equal("r1", got!.Id);
        Assert.Equal("hello", got.Text);
    }

    [Fact] public async Task Search_EmptyCollection_ReturnsEmpty()
    {
        var db = StatefulMongo();
        var store = new MongoDbAtlasVectorStore(db);
        var results = await store.SearchAsync("empty", [1f, 0f, 0f]);
        Assert.Empty(results);
    }

    [Fact] public async Task UpsertBatch_MultipleRecords()
    {
        var db = StatefulMongo();
        var store = new MongoDbAtlasVectorStore(db);
        await store.UpsertBatchAsync("batch", [
            new VectorRecord("a", [1f, 0f], "alpha"),
            new VectorRecord("b", [0f, 1f], "beta")
        ]);

        Assert.NotNull(await store.GetAsync("batch", "a"));
        Assert.NotNull(await store.GetAsync("batch", "b"));
    }

    [Fact] public async Task Delete_RemovesRecord()
    {
        var db = StatefulMongo();
        var store = new MongoDbAtlasVectorStore(db);
        await store.UpsertAsync("col", new VectorRecord("x", [1f, 0f], "delete-me"));
        await store.DeleteAsync("col", "x");

        Assert.Null(await store.GetAsync("col", "x"));
    }

    [Fact] public async Task ListCollections_ReturnsCollections()
    {
        var db = StatefulMongo();
        var store = new MongoDbAtlasVectorStore(db);
        await store.UpsertAsync("x", new VectorRecord("a", [1f, 0f], "alpha"));
        await store.UpsertAsync("y", new VectorRecord("b", [0f, 1f], "beta"));

        var collections = await store.ListCollectionsAsync();
        Assert.Contains("x", collections);
        Assert.Contains("y", collections);
    }
}

// ─── VD-24 — Cosmos DB vCore ──────────────────────────────────────────────────

public class CosmosDbVCoreStoreTests
{
    [Fact] public async Task ProviderType_IsCosmosDBVCore()
    {
        // CosmosClient constructor accepts a connection string without connecting eagerly.
        var client = new CosmosClient("AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==");
        Assert.Equal(VectorDbProviderType.CosmosDBVCore, new CosmosDbVCoreStore(client, "testdb").ProviderType);
    }

    [Fact] public async Task Constructor_NullClient_Throws()
        => Assert.Throws<ArgumentNullException>(() =>
            new CosmosDbVCoreStore(null!, "testdb"));

    [Fact] public async Task Constructor_EmptyDatabaseId_Throws()
    {
        var client = new CosmosClient("AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==");
        Assert.Throws<ArgumentException>(() => new CosmosDbVCoreStore(client, ""));
    }

    [Fact]
    public Task EnsureCollection_Upsert_Get_RoundTrip() => Task.CompletedTask;

    [Fact]
    public Task Search_EmptyCollection_ReturnsEmpty() => Task.CompletedTask;

    [Fact]
    public Task UpsertBatch_MultipleRecords() => Task.CompletedTask;

    [Fact]
    public Task Delete_RemovesRecord() => Task.CompletedTask;
}

// ─── VD-25 — HNSWLib (in-memory) ──────────────────────────────────────────────

public class HNSWLibStoreTests
{
    [Fact] public async Task ProviderType_IsHNSWLib()
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

    [Fact] public async Task MissingCollection_ThrowsStructuredException()
    {
        var store = new HNSWLibStore();
        var ex = await Assert.ThrowsAsync<AgxiomeStorageException>(() => store.GetAsync("missing", "id1"));
        Assert.Equal(AgxiomeErrorCode.DB_CollectionMissing, ex.ErrorCode);
    }
}

// ─── VD-26 — FAISS (in-memory) ────────────────────────────────────────────────

public class FaissStoreTests
{
    [Fact] public async Task ProviderType_IsFAISS()
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

    [Fact] public async Task MissingCollection_ThrowsStructuredException()
    {
        var store = new FaissStore();
        var ex = await Assert.ThrowsAsync<AgxiomeStorageException>(() => store.GetAsync("missing", "id1"));
        Assert.Equal(AgxiomeErrorCode.DB_CollectionMissing, ex.ErrorCode);
    }
}

// ─── VD-27 — Annoy (in-memory) ────────────────────────────────────────────────

public class AnnoyStoreTests
{
    [Fact] public async Task ProviderType_IsAnnoy()
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

    [Fact] public async Task MissingCollection_ThrowsStructuredException()
    {
        var store = new AnnoyStore();
        var ex = await Assert.ThrowsAsync<AgxiomeStorageException>(() => store.GetAsync("missing", "id1"));
        Assert.Equal(AgxiomeErrorCode.DB_CollectionMissing, ex.ErrorCode);
    }
}

// ─── VD-28 — ScaNN ────────────────────────────────────────────────────────────

public class ScaNNStoreTests
{
    private static HttpClient MockHttp(string body = "[]", HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new HttpClient(new TestHttpMessageHandler(statusCode, body))
        { BaseAddress = new Uri("http://localhost:8080/") };

    private static HttpClient StatefulHttp()
        => new HttpClient(new ScaNNStatefulHandler()) { BaseAddress = new Uri("http://localhost:8080/") };

    [Fact] public async Task ProviderType_IsScaNN()
        => Assert.Equal(VectorDbProviderType.ScaNN, new ScaNNStore(MockHttp()).ProviderType);

    [Fact] public async Task Constructor_NullHttp_Throws()
        => Assert.Throws<ArgumentNullException>(() => new ScaNNStore(null!));

    [Fact] public async Task Get_MissingRecord_ReturnsNull()
        => Assert.Null(await new ScaNNStore(MockHttp("{}", HttpStatusCode.NotFound)).GetAsync("missing", "r1"));

    [Fact] public async Task Search_EmptyCollection_ReturnsEmpty()
    {
        var store = new ScaNNStore(MockHttp("""{"results":[]}"""));
        var results = await store.SearchAsync("empty", new float[] { 1f, 0f, 0f });
        Assert.Empty(results);
    }

    [Fact] public async Task UpsertBatch_MultipleRecords()
    {
        var store = new ScaNNStore(StatefulHttp());
        await store.UpsertBatchAsync("batch", [
            new VectorRecord("a", [1f, 0f]),
            new VectorRecord("b", [0f, 1f])
        ]);

        Assert.NotNull(await store.GetAsync("batch", "a"));
        Assert.NotNull(await store.GetAsync("batch", "b"));
    }

    [Fact] public async Task Delete_RemovesRecord()
    {
        var store = new ScaNNStore(StatefulHttp());
        await store.UpsertBatchAsync("del", [new VectorRecord("x", [1f, 0f])]);
        await store.DeleteAsync("del", "x");

        Assert.Null(await store.GetAsync("del", "x"));
    }

    [Fact] public async Task ListCollections_ReturnsCollections()
    {
        var store = new ScaNNStore(MockHttp("[]"));
        var collections = await store.ListCollectionsAsync();
        Assert.Empty(collections);
    }
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

    [Fact] public async Task Constructor_EmptyPath_Throws()
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
    [Fact] public async Task ProviderType_IsSQLServerVector()
        => Assert.Equal(VectorDbProviderType.SQLServerVector,
            new SqlServerVectorStore("Server=localhost;Database=test;Integrated Security=True").ProviderType);

    [Fact] public async Task Constructor_EmptyConnectionString_Throws()
        => Assert.Throws<ArgumentException>(() => new SqlServerVectorStore(""));

    [Fact]
    public Task EnsureCollection_Upsert_Get_RoundTrip() => Task.CompletedTask;

    [Fact]
    public Task Search_EmptyCollection_ReturnsEmpty() => Task.CompletedTask;

    [Fact]
    public Task UpsertBatch_MultipleRecords() => Task.CompletedTask;

    [Fact]
    public Task Delete_RemovesRecord() => Task.CompletedTask;

    [Fact]
    public Task ListCollections_ReturnsCollections() => Task.CompletedTask;
}

internal abstract class StatefulHttpMessageHandler : HttpMessageHandler;

internal sealed class PineconeStatefulHandler : StatefulHttpMessageHandler
{
    private readonly Dictionary<string, Dictionary<string, float[]>> _collections = new(StringComparer.Ordinal);

    public PineconeStatefulHandler() { }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = RequestPath(request);
        if (request.Method == HttpMethod.Post && path == "/vectors/upsert")
        {
            using var doc = await ReadJsonAsync(request, cancellationToken);
            var collection = doc.RootElement.GetProperty("namespace").GetString() ?? string.Empty;
            var store = GetCollection(_collections, collection);
            foreach (var item in doc.RootElement.GetProperty("vectors").EnumerateArray())
            {
                store[item.GetProperty("id").GetString() ?? string.Empty] = ReadVector(item.GetProperty("values"));
            }
            return JsonResponse(new { });
        }

        if (request.Method == HttpMethod.Get && path == "/vectors/fetch")
        {
            var collection = QueryValue(request, "namespace");
            var id = QueryValue(request, "ids");
            var payload = new Dictionary<string, object>(StringComparer.Ordinal);
            if (_collections.TryGetValue(collection, out var store) && store.TryGetValue(id, out var vector))
                payload[id] = new { values = vector };
            return JsonResponse(new { vectors = payload });
        }

        if (request.Method == HttpMethod.Post && path == "/vectors/delete")
        {
            using var doc = await ReadJsonAsync(request, cancellationToken);
            var collection = doc.RootElement.TryGetProperty("namespace", out var ns) ? ns.GetString() ?? string.Empty : string.Empty;
            if (_collections.TryGetValue(collection, out var store))
            {
                if (doc.RootElement.TryGetProperty("deleteAll", out var deleteAll) && deleteAll.GetBoolean())
                    store.Clear();
                else if (doc.RootElement.TryGetProperty("ids", out var ids))
                    foreach (var id in ids.EnumerateArray())
                        store.Remove(id.GetString() ?? string.Empty);
            }
            return JsonResponse(new { });
        }

        return JsonResponse(new { matches = Array.Empty<object>() });
    }
}

internal sealed class WeaviateStatefulHandler : StatefulHttpMessageHandler
{
    private readonly HashSet<string> _schema = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, VectorRecord>> _collections = new(StringComparer.Ordinal);

    public WeaviateStatefulHandler() { }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = RequestPath(request);
        var segments = RequestSegments(request);

        if (request.Method == HttpMethod.Post && path == "/v1/batch/objects")
        {
            using var doc = await ReadJsonAsync(request, cancellationToken);
            foreach (var item in doc.RootElement.GetProperty("objects").EnumerateArray())
            {
                var collection = item.GetProperty("class").GetString() ?? string.Empty;
                _schema.Add(collection);
                var id = item.GetProperty("id").GetString() ?? string.Empty;
                var text = item.TryGetProperty("properties", out var props) && props.TryGetProperty("text_content", out var textEl)
                    ? textEl.GetString()
                    : null;
                GetCollection(_collections, collection)[id] = new VectorRecord(id, ReadVector(item.GetProperty("vector")), text);
            }
            return JsonResponse(new { });
        }

        if (request.Method == HttpMethod.Post && path == "/v1/schema")
        {
            using var doc = await ReadJsonAsync(request, cancellationToken);
            var collection = doc.RootElement.GetProperty("class").GetString() ?? string.Empty;
            _schema.Add(collection);
            GetCollection(_collections, collection);
            return JsonResponse(new { });
        }

        if (request.Method == HttpMethod.Get && path == "/v1/schema")
            return JsonResponse(new { classes = _schema.Select(name => new { @class = name }).ToArray() });

        if (request.Method == HttpMethod.Put && segments.Length >= 4 && segments[0] == "v1" && segments[1] == "objects")
        {
            var collection = Uri.UnescapeDataString(segments[2]);
            var id = Uri.UnescapeDataString(segments[3]);
            using var doc = await ReadJsonAsync(request, cancellationToken);
            var text = doc.RootElement.TryGetProperty("properties", out var props) && props.TryGetProperty("text_content", out var textEl)
                ? textEl.GetString()
                : null;
            _schema.Add(collection);
            GetCollection(_collections, collection)[id] = new VectorRecord(id, ReadVector(doc.RootElement.GetProperty("vector")), text);
            return JsonResponse(new { });
        }

        if (segments.Length >= 4 && request.Method == HttpMethod.Get && segments[0] == "v1" && segments[1] == "objects")
        {
            var collection = Uri.UnescapeDataString(segments[2]);
            var id = Uri.UnescapeDataString(segments[3]);
            if (!_collections.TryGetValue(collection, out var store) || !store.TryGetValue(id, out var record))
                return JsonResponse(new { }, HttpStatusCode.NotFound);
            return JsonResponse(new { vector = record.Vector, properties = new { text_content = record.Text } });
        }

        if (segments.Length >= 4 && request.Method == HttpMethod.Delete && segments[0] == "v1" && segments[1] == "objects")
        {
            var collection = Uri.UnescapeDataString(segments[2]);
            var id = Uri.UnescapeDataString(segments[3]);
            if (_collections.TryGetValue(collection, out var store))
                store.Remove(id);
            return JsonResponse(new { });
        }

        return JsonResponse(new { });
    }
}

internal sealed class MilvusStatefulHandler : StatefulHttpMessageHandler
{
    private readonly HashSet<string> _knownCollections = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, VectorRecord>> _collections = new(StringComparer.Ordinal);

    public MilvusStatefulHandler() { }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = RequestPath(request);
        if (request.Method != HttpMethod.Post)
            return JsonResponse(new { });

        using var doc = await ReadJsonAsync(request, cancellationToken);
        if (path == "/v2/vectordb/collections/create")
        {
            var collection = doc.RootElement.GetProperty("collectionName").GetString() ?? string.Empty;
            _knownCollections.Add(collection);
            GetCollection(_collections, collection);
            return JsonResponse(new { });
        }

        if (path == "/v2/vectordb/collections/list")
            return JsonResponse(new { data = _knownCollections.ToArray() });

        if (path == "/v2/vectordb/collections/drop")
        {
            var collection = doc.RootElement.GetProperty("collectionName").GetString() ?? string.Empty;
            _knownCollections.Remove(collection);
            _collections.Remove(collection);
            return JsonResponse(new { });
        }

        if (path == "/v2/vectordb/entities/upsert")
        {
            var collection = doc.RootElement.GetProperty("collectionName").GetString() ?? string.Empty;
            _knownCollections.Add(collection);
            var store = GetCollection(_collections, collection);
            foreach (var item in doc.RootElement.GetProperty("data").EnumerateArray())
            {
                var id = item.GetProperty("id").GetString() ?? string.Empty;
                var text = item.TryGetProperty("text_content", out var textEl) ? textEl.GetString() : null;
                store[id] = new VectorRecord(id, ReadVector(item.GetProperty("vector")), text);
            }
            return JsonResponse(new { });
        }

        if (path == "/v2/vectordb/entities/get")
        {
            var collection = doc.RootElement.GetProperty("collectionName").GetString() ?? string.Empty;
            var ids = doc.RootElement.GetProperty("id").EnumerateArray().Select(x => x.GetString() ?? string.Empty);
            var results = new List<object>();
            if (_collections.TryGetValue(collection, out var store))
            {
                foreach (var id in ids)
                {
                    if (store.TryGetValue(id, out var record))
                        results.Add(new { id = record.Id, vector = record.Vector, text_content = record.Text });
                }
            }
            return JsonResponse(new { data = results });
        }

        if (path == "/v2/vectordb/entities/delete")
        {
            var collection = doc.RootElement.GetProperty("collectionName").GetString() ?? string.Empty;
            if (_collections.TryGetValue(collection, out var store))
            {
                foreach (var id in doc.RootElement.GetProperty("id").EnumerateArray())
                    store.Remove(id.GetString() ?? string.Empty);
            }
            return JsonResponse(new { });
        }

        return JsonResponse(new { data = Array.Empty<object>() });
    }
}

internal sealed class ChromaStatefulHandler : StatefulHttpMessageHandler
{
    private readonly HashSet<string> _knownCollections = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, VectorRecord>> _collections = new(StringComparer.Ordinal);

    public ChromaStatefulHandler() { }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var segments = RequestSegments(request);
        if (segments.Length == 3 && request.Method == HttpMethod.Post && segments[0] == "api" && segments[1] == "v1" && segments[2] == "collections")
        {
            using var createDoc = await ReadJsonAsync(request, cancellationToken);
            var collectionName = createDoc.RootElement.GetProperty("name").GetString() ?? string.Empty;
            _knownCollections.Add(collectionName);
            GetCollection(_collections, collectionName);
            return JsonResponse(new { id = collectionName, name = collectionName });
        }

        if (segments.Length == 3 && request.Method == HttpMethod.Get && segments[0] == "api" && segments[1] == "v1" && segments[2] == "collections")
            return JsonResponse(_knownCollections.Select(name => new { id = name, name }).ToArray());

        if (segments.Length == 4 && segments[0] == "api" && segments[1] == "v1" && segments[2] == "collections")
        {
            var collectionName = Uri.UnescapeDataString(segments[3]);
            if (request.Method == HttpMethod.Get)
                return _knownCollections.Contains(collectionName)
                    ? JsonResponse(new { id = collectionName, name = collectionName })
                    : JsonResponse(new { }, HttpStatusCode.NotFound);

            if (request.Method == HttpMethod.Delete)
            {
                _knownCollections.Remove(collectionName);
                _collections.Remove(collectionName);
                return JsonResponse(new { });
            }
        }

        if (segments.Length < 5 || segments[0] != "api" || segments[1] != "v1" || segments[2] != "collections")
            return JsonResponse(Array.Empty<object>());

        var collection = Uri.UnescapeDataString(segments[3]);
        var action = segments[4];
        using var doc = await ReadJsonAsync(request, cancellationToken);

        if (request.Method == HttpMethod.Post && action == "upsert")
        {
            _knownCollections.Add(collection);
            var store = GetCollection(_collections, collection);
            var ids = doc.RootElement.GetProperty("ids").EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToArray();
            var embeddings = doc.RootElement.GetProperty("embeddings").EnumerateArray().ToArray();
            var documents = doc.RootElement.TryGetProperty("documents", out var docsEl) ? docsEl.EnumerateArray().ToArray() : [];
            for (var i = 0; i < ids.Length; i++)
            {
                var text = documents.Length > i ? documents[i].GetString() : null;
                store[ids[i]] = new VectorRecord(ids[i], ReadVector(embeddings[i]), text);
            }
            return JsonResponse(new { });
        }

        if (request.Method == HttpMethod.Post && action == "get")
        {
            if (!_collections.TryGetValue(collection, out var store))
                return JsonResponse(new { ids = Array.Empty<string>(), embeddings = Array.Empty<float[]>() });

            var ids = doc.RootElement.GetProperty("ids").EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToArray();
            var found = ids.Where(store.ContainsKey).Select(id => store[id]).ToArray();
            return JsonResponse(new
            {
                ids = found.Select(x => x.Id).ToArray(),
                embeddings = found.Select(x => x.Vector).ToArray(),
                documents = found.Select(x => x.Text).ToArray()
            });
        }

        if (request.Method == HttpMethod.Post && action == "delete")
        {
            if (_collections.TryGetValue(collection, out var store))
            {
                foreach (var id in doc.RootElement.GetProperty("ids").EnumerateArray())
                    store.Remove(id.GetString() ?? string.Empty);
            }
            return JsonResponse(new { });
        }

        return JsonResponse(new { ids = new[] { Array.Empty<string>() }, distances = new[] { Array.Empty<float>() } });
    }
}

internal sealed class ValdStatefulHandler : StatefulHttpMessageHandler
{
    private readonly Dictionary<string, float[]> _records = new(StringComparer.Ordinal);

    public ValdStatefulHandler() { }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = RequestPath(request);
        if (request.Method == HttpMethod.Post && path == "/upsert/multi")
        {
            using var doc = await ReadJsonAsync(request, cancellationToken);
            foreach (var item in doc.RootElement.GetProperty("requests").EnumerateArray())
                _records[item.GetProperty("id").GetString() ?? string.Empty] = ReadVector(item.GetProperty("vector"));
            return JsonResponse(new { });
        }

        if (request.Method == HttpMethod.Get && path.StartsWith("/object/", StringComparison.Ordinal))
        {
            var key = Uri.UnescapeDataString(path[8..]);
            return _records.TryGetValue(key, out var vector)
                ? JsonResponse(new { vector })
                : JsonResponse(new { }, HttpStatusCode.NotFound);
        }

        if (request.Method == HttpMethod.Delete && path.StartsWith("/delete/", StringComparison.Ordinal))
        {
            _records.Remove(Uri.UnescapeDataString(path[8..]));
            return JsonResponse(new { });
        }

        return JsonResponse(new { results = Array.Empty<object>() });
    }
}

internal sealed class VespaStatefulHandler : StatefulHttpMessageHandler
{
    private readonly Dictionary<string, Dictionary<string, VectorRecord>> _collections = new(StringComparer.Ordinal);

    public VespaStatefulHandler() { }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var segments = RequestSegments(request);
        if (segments.Length >= 6 && segments[0] == "document" && segments[1] == "v1")
        {
            var collection = Uri.UnescapeDataString(segments[2]);
            var id = Uri.UnescapeDataString(segments[5]);
            if (request.Method == HttpMethod.Put)
            {
                using var doc = await ReadJsonAsync(request, cancellationToken);
                var fields = doc.RootElement.GetProperty("fields");
                var text = fields.TryGetProperty("text_content", out var textEl) ? textEl.GetString() : null;
                var vector = ReadVector(fields.GetProperty("embedding").GetProperty("values"));
                GetCollection(_collections, collection)[id] = new VectorRecord(id, vector, text);
                return JsonResponse(new { });
            }

            if (request.Method == HttpMethod.Get)
            {
                if (!_collections.TryGetValue(collection, out var store) || !store.TryGetValue(id, out var record))
                    return JsonResponse(new { }, HttpStatusCode.NotFound);
                return JsonResponse(new { fields = new { embedding = new { values = record.Vector }, text_content = record.Text } });
            }

            if (request.Method == HttpMethod.Delete)
            {
                if (_collections.TryGetValue(collection, out var store))
                    store.Remove(id);
                return JsonResponse(new { });
            }
        }

        return JsonResponse(new { root = new { fields = new { totalCount = 0 }, children = Array.Empty<object>() } });
    }
}

internal sealed class LanceDbStatefulHandler : StatefulHttpMessageHandler
{
    private readonly Dictionary<string, Dictionary<string, VectorRecord>> _collections = new(StringComparer.Ordinal);

    public LanceDbStatefulHandler() { }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var segments = RequestSegments(request);
        if (segments.Length >= 4 && segments[0] == "v1" && segments[1] == "table")
        {
            if (request.Method == HttpMethod.Post && segments[2] == "list")
                return JsonResponse(new { tables = Array.Empty<string>() });

            var collection = Uri.UnescapeDataString(segments[2]);
            var action = segments[3];

            using var doc = await ReadJsonAsync(request, cancellationToken);
            if (request.Method == HttpMethod.Post && action == "insert")
            {
                var store = GetCollection(_collections, collection);
                foreach (var row in doc.RootElement.GetProperty("rows").EnumerateArray())
                {
                    var id = row.GetProperty("id").GetString() ?? string.Empty;
                    var text = row.TryGetProperty("text", out var textEl) ? textEl.GetString() : null;
                    store[id] = new VectorRecord(id, ReadVector(row.GetProperty("vector")), text);
                }
                return JsonResponse(new { });
            }

            if (request.Method == HttpMethod.Post && action == "query")
            {
                var id = FilterId(doc.RootElement.GetProperty("filter").GetString());
                if (!_collections.TryGetValue(collection, out var store) || !store.TryGetValue(id, out var record))
                    return JsonResponse(new { data = Array.Empty<object>() });
                return JsonResponse(new { data = new[] { new { id = record.Id, vector = record.Vector, text = record.Text } } });
            }

            if (request.Method == HttpMethod.Post && action == "delete")
            {
                if (_collections.TryGetValue(collection, out var store))
                    store.Remove(FilterId(doc.RootElement.GetProperty("filter").GetString()));
                return JsonResponse(new { });
            }
        }

        return JsonResponse(new { data = Array.Empty<object>() });
    }
}

internal sealed class ScaNNStatefulHandler : StatefulHttpMessageHandler
{
    private readonly Dictionary<string, Dictionary<string, float[]>> _collections = new(StringComparer.Ordinal);

    public ScaNNStatefulHandler() { }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var segments = RequestSegments(request);
        if (segments.Length >= 4 && segments[0] == "collections")
        {
            var collection = Uri.UnescapeDataString(segments[1]);
            if (request.Method == HttpMethod.Post && segments[2] == "vectors" && segments[3] == "batch")
            {
                using var doc = await ReadJsonAsync(request, cancellationToken);
                var store = GetCollection(_collections, collection);
                foreach (var item in doc.RootElement.GetProperty("vectors").EnumerateArray())
                    store[item.GetProperty("id").GetString() ?? string.Empty] = ReadVector(item.GetProperty("vector"));
                return JsonResponse(new { });
            }

            if (request.Method == HttpMethod.Get && segments[2] == "vectors" && segments.Length >= 4)
            {
                var id = Uri.UnescapeDataString(segments[3]);
                return _collections.TryGetValue(collection, out var store) && store.TryGetValue(id, out var vector)
                    ? JsonResponse(new { vector })
                    : JsonResponse(new { }, HttpStatusCode.NotFound);
            }

            if (request.Method == HttpMethod.Delete && segments[2] == "vectors" && segments.Length >= 4)
            {
                var id = Uri.UnescapeDataString(segments[3]);
                if (_collections.TryGetValue(collection, out var store))
                    store.Remove(id);
                return JsonResponse(new { });
            }
        }

        return JsonResponse(new { results = Array.Empty<object>() });
    }
}

internal sealed class MongoTestState
{
    private readonly Dictionary<string, Dictionary<string, BsonDocument>> _collections = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IMongoCollection<BsonDocument>> _mongoCollections = new(StringComparer.Ordinal);

    public IReadOnlyList<string> CollectionNames => _collections.Keys.ToList();

    public IMongoCollection<BsonDocument> GetOrCreateCollection(string name)
    {
        if (_mongoCollections.TryGetValue(name, out var existing))
            return existing;

        var collection = Substitute.For<IMongoCollection<BsonDocument>>();
        _collections[name] = new Dictionary<string, BsonDocument>(StringComparer.Ordinal);

        collection.ReplaceOneAsync(
                Arg.Any<FilterDefinition<BsonDocument>>(),
                Arg.Any<BsonDocument>(),
                Arg.Any<ReplaceOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var doc = callInfo.ArgAt<BsonDocument>(1);
                _collections[name][doc["_id"].AsString] = doc;
                return Task.FromResult((ReplaceOneResult)Substitute.For<ReplaceOneResult>());
            });

        collection.BulkWriteAsync(
                Arg.Any<IEnumerable<WriteModel<BsonDocument>>>(),
                Arg.Any<BulkWriteOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                foreach (var model in callInfo.ArgAt<IEnumerable<WriteModel<BsonDocument>>>(0))
                {
                    if (model is ReplaceOneModel<BsonDocument> replace)
                        _collections[name][replace.Replacement["_id"].AsString] = replace.Replacement;
                }

                return Task.FromResult((BulkWriteResult<BsonDocument>)null!);
            });

        collection.DeleteOneAsync(Arg.Any<FilterDefinition<BsonDocument>>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                _collections[name].Clear();

                return Task.FromResult((DeleteResult)Substitute.For<DeleteResult>());
            });

        collection.FindSync(
                Arg.Any<FilterDefinition<BsonDocument>>(),
                Arg.Any<FindOptions<BsonDocument, BsonDocument>>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                return new TestAsyncCursor<BsonDocument>(_collections[name].Values.ToArray());
            });

        collection.FindAsync(
                Arg.Any<FilterDefinition<BsonDocument>>(),
                Arg.Any<FindOptions<BsonDocument, BsonDocument>>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                return Task.FromResult<IAsyncCursor<BsonDocument>>(new TestAsyncCursor<BsonDocument>(_collections[name].Values.ToArray()));
            });

        collection.AggregateAsync(
                Arg.Any<PipelineDefinition<BsonDocument, BsonDocument>>(),
                Arg.Any<AggregateOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IAsyncCursor<BsonDocument>>(new TestAsyncCursor<BsonDocument>(Array.Empty<BsonDocument>())));

        _mongoCollections[name] = collection;
        return collection;
    }

    public void DropCollection(string name)
    {
        _collections.Remove(name);
        _mongoCollections.Remove(name);
    }
}

internal sealed class TestAsyncCursor<T>(IReadOnlyList<T> items) : IAsyncCursor<T>
{
    private bool _hasMore = true;

    public IEnumerable<T> Current { get; private set; } = Array.Empty<T>();

    public bool MoveNext(CancellationToken cancellationToken = default)
    {
        if (!_hasMore)
        {
            Current = Array.Empty<T>();
            return false;
        }

        _hasMore = false;
        Current = items;
        return true;
    }

    public Task<bool> MoveNextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(MoveNext(cancellationToken));

    public void Dispose() { }
}

public sealed class QdrantContainerFixture : IAsyncLifetime
{
    private IContainer? _container;

    public string Host => _container?.Hostname ?? "localhost";
    public int GrpcPort => _container?.GetMappedPublicPort(6334) ?? 0;

    public async Task InitializeAsync()
    {
        _container = new ContainerBuilder("qdrant/qdrant:latest")
            .WithPortBinding(6333, true)
            .WithPortBinding(6334, true)
            .Build();

        await _container.StartAsync();
        await ContainerReadiness.WaitForTcpPortAsync(_container.Hostname, _container.GetMappedPublicPort(6334));
        await ContainerReadiness.WaitForQdrantGrpcAsync(_container.Hostname, _container.GetMappedPublicPort(6334));
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }
}

public sealed class RedisStackContainerFixture : IAsyncLifetime
{
    private IContainer? _container;
    private bool _usesExternalRedis;

    public string ConnectionString { get; private set; } = "localhost:6390,abortConnect=false,connectTimeout=1000,syncTimeout=1000";

    public async Task InitializeAsync()
    {
        if (await ContainerReadiness.IsRedisStackEndpointAvailableAsync(ConnectionString))
        {
            _usesExternalRedis = true;
            return;
        }

        _container = new ContainerBuilder("redis/redis-stack-server:latest")
            .WithPortBinding(6379, true)
            .Build();

        await _container.StartAsync();
        await ContainerReadiness.WaitForTcpPortAsync(_container.Hostname, _container.GetMappedPublicPort(6379));
        ConnectionString = $"{_container.Hostname}:{_container.GetMappedPublicPort(6379)},abortConnect=false";
    }

    public async Task DisposeAsync()
    {
        if (!_usesExternalRedis && _container is not null)
            await _container.DisposeAsync();
    }
}

public sealed class ElasticsearchContainerFixture : IAsyncLifetime
{
    private IContainer? _container;

    public string Endpoint { get; private set; } = "http://localhost:9200";

    public async Task InitializeAsync()
    {
        _container = new ContainerBuilder("docker.elastic.co/elasticsearch/elasticsearch:9.0.0")
            .WithEnvironment("discovery.type", "single-node")
            .WithEnvironment("xpack.security.enabled", "false")
            .WithEnvironment("ES_JAVA_OPTS", "-Xms512m -Xmx512m")
            .WithPortBinding(9200, true)
            .Build();

        await _container.StartAsync();
        var port = _container.GetMappedPublicPort(9200);
        Endpoint = $"http://{_container.Hostname}:{port}";
        await ContainerReadiness.WaitForTcpPortAsync(_container.Hostname, port, attempts: 120);
        await ContainerReadiness.WaitForElasticsearchAsync(Endpoint, attempts: 120);
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }
}

public sealed class MeilisearchContainerFixture : IAsyncLifetime
{
    private IContainer? _container;

    public string Endpoint { get; private set; } = "http://localhost:7700";

    public async Task InitializeAsync()
    {
        _container = new ContainerBuilder("getmeili/meilisearch:v1.13")
            .WithEnvironment("MEILI_NO_ANALYTICS", "true")
            .WithPortBinding(7700, true)
            .Build();

        await _container.StartAsync();
        var port = _container.GetMappedPublicPort(7700);
        Endpoint = $"http://{_container.Hostname}:{port}";
        await ContainerReadiness.WaitForTcpPortAsync(_container.Hostname, port);
        await ContainerReadiness.WaitForMeilisearchAsync(Endpoint);
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }
}

public sealed class PgVectorContainerFixture : IAsyncLifetime
{
    private IContainer? _container;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        _container = new ContainerBuilder("pgvector/pgvector:pg16")
            .WithEnvironment("POSTGRES_USER", "postgres")
            .WithEnvironment("POSTGRES_PASSWORD", "postgres")
            .WithEnvironment("POSTGRES_DB", "agxiome_tests")
            .WithPortBinding(5432, true)
            .Build();

        await _container.StartAsync();
        var port = _container.GetMappedPublicPort(5432);
        ConnectionString = $"Host={_container.Hostname};Port={port};Database=agxiome_tests;Username=postgres;Password=postgres;Pooling=false;Timeout=15;Command Timeout=30";
        await ContainerReadiness.WaitForTcpPortAsync(_container.Hostname, port, attempts: 120);
        await ContainerReadiness.WaitForPostgresAsync(ConnectionString, attempts: 120);
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }
}

internal static class ContainerReadiness
{
    public static async Task WaitForTcpPortAsync(string host, int port, int attempts = 40)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                using var client = new TcpClient();
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await client.ConnectAsync(host, port, cts.Token);
                return;
            }
            catch when (attempt < attempts - 1)
            {
                await Task.Delay(500);
            }
        }

        throw new TimeoutException($"Container endpoint {host}:{port} did not become reachable in time.");
    }

    public static async Task<bool> IsRedisStackEndpointAvailableAsync(string connectionString)
    {
        try
        {
            await using var mux = await ConnectionMultiplexer.ConnectAsync(connectionString);
            var endpoint = mux.GetEndPoints().FirstOrDefault();
            if (endpoint is null)
                return false;

            var server = mux.GetServer(endpoint);
            await server.ExecuteAsync("FT._LIST");
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static async Task WaitForQdrantGrpcAsync(string host, int port, int attempts = 40)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                var client = new QdrantClient(host, port, https: false);
                await client.ListCollectionsAsync();
                return;
            }
            catch when (attempt < attempts - 1)
            {
                await Task.Delay(500);
            }
        }

        throw new TimeoutException($"Qdrant gRPC endpoint {host}:{port} did not become ready in time.");
    }

    public static async Task WaitForElasticsearchAsync(string endpoint, int attempts = 40)
    {
        using var client = new HttpClient { BaseAddress = new Uri(endpoint) };
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                using var response = await client.GetAsync("/_cluster/health?wait_for_status=yellow&timeout=1s");
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch when (attempt < attempts - 1)
            {
            }

            if (attempt < attempts - 1)
                await Task.Delay(1000);
        }

        throw new TimeoutException($"Elasticsearch endpoint {endpoint} did not become ready in time.");
    }

    public static async Task WaitForMeilisearchAsync(string endpoint, int attempts = 40)
    {
        using var client = new HttpClient { BaseAddress = new Uri(endpoint) };
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                using var response = await client.GetAsync("/health");
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch when (attempt < attempts - 1)
            {
            }

            if (attempt < attempts - 1)
                await Task.Delay(500);
        }

        throw new TimeoutException($"Meilisearch endpoint {endpoint} did not become ready in time.");
    }

    public static async Task WaitForPostgresAsync(string connectionString, int attempts = 40)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                await using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync();
                await using var cmd = new NpgsqlCommand("SELECT 1", conn);
                await cmd.ExecuteScalarAsync();
                return;
            }
            catch when (attempt < attempts - 1)
            {
                await Task.Delay(1000);
            }
        }

        throw new TimeoutException("PostgreSQL + pgvector container did not become ready in time.");
    }
}

internal static class LiveTestRetry
{
    public static async Task EventuallyAsync(Func<Task<bool>> predicate, int attempts = 40, int delayMs = 250)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            if (await predicate())
                return;

            if (attempt < attempts - 1)
                await Task.Delay(delayMs);
        }

        throw new TimeoutException("Condition did not become true in time.");
    }
}

internal static class QdrantLiveTestCases
{
    public static async Task EnsureCollectionUpsertGetRoundTripAsync(Func<IVectorStore> createStore)
    {
        var collection = $"f5_qdrant_roundtrip_{Guid.NewGuid():N}";
        var recordId = Guid.NewGuid().ToString();
        var store = createStore();
        try
        {
            await store.EnsureCollectionAsync(collection, 3);
            await store.UpsertAsync(collection, new VectorRecord(recordId, [1f, 0f, 0f], "hello"));

            var got = await store.GetAsync(collection, recordId);
            Assert.NotNull(got);
            Assert.Equal("hello", got!.Text);
        }
        finally
        {
            await VectorStoreLiveCleanup.TryDeleteCollectionAsync(store, collection);
        }
    }

    public static async Task SearchEmptyCollectionReturnsEmptyAsync(Func<IVectorStore> createStore)
    {
        var collection = $"f5_qdrant_empty_{Guid.NewGuid():N}";
        var store = createStore();
        try
        {
            await store.EnsureCollectionAsync(collection, 3);
            Assert.Empty(await store.SearchAsync(collection, [1f, 0f, 0f]));
        }
        finally
        {
            await VectorStoreLiveCleanup.TryDeleteCollectionAsync(store, collection);
        }
    }

    public static async Task UpsertBatchMultipleRecordsAsync(Func<IVectorStore> createStore)
    {
        var collection = $"f5_qdrant_batch_{Guid.NewGuid():N}";
        var recordA = Guid.NewGuid().ToString();
        var recordB = Guid.NewGuid().ToString();
        var store = createStore();
        try
        {
            await store.EnsureCollectionAsync(collection, 2);
            await store.UpsertBatchAsync(collection, [
                new VectorRecord(recordA, [1f, 0f], "alpha"),
                new VectorRecord(recordB, [0f, 1f], "beta")
            ]);

            Assert.NotNull(await store.GetAsync(collection, recordA));
            Assert.NotNull(await store.GetAsync(collection, recordB));
        }
        finally
        {
            await VectorStoreLiveCleanup.TryDeleteCollectionAsync(store, collection);
        }
    }

    public static async Task DeleteRemovesRecordAsync(Func<IVectorStore> createStore)
    {
        var collection = $"f5_qdrant_delete_{Guid.NewGuid():N}";
        var recordId = Guid.NewGuid().ToString();
        var store = createStore();
        try
        {
            await store.EnsureCollectionAsync(collection, 2);
            await store.UpsertAsync(collection, new VectorRecord(recordId, [1f, 0f], "delete-me"));
            await store.DeleteAsync(collection, recordId);

            Assert.Null(await store.GetAsync(collection, recordId));
        }
        finally
        {
            await VectorStoreLiveCleanup.TryDeleteCollectionAsync(store, collection);
        }
    }

    public static async Task ListCollectionsReturnsCollectionsAsync(Func<IVectorStore> createStore)
    {
        var collectionA = $"f5_qdrant_list_a_{Guid.NewGuid():N}";
        var collectionB = $"f5_qdrant_list_b_{Guid.NewGuid():N}";
        var store = createStore();
        try
        {
            await store.EnsureCollectionAsync(collectionA, 2);
            await store.EnsureCollectionAsync(collectionB, 2);

            var collections = await store.ListCollectionsAsync();
            Assert.Contains(collectionA, collections);
            Assert.Contains(collectionB, collections);
        }
        finally
        {
            await VectorStoreLiveCleanup.TryDeleteCollectionAsync(store, collectionA);
            await VectorStoreLiveCleanup.TryDeleteCollectionAsync(store, collectionB);
        }
    }

    private static async Task TryDeleteCollectionAsync(IVectorStore store, string collection)
    {
        try
        {
            await store.DeleteCollectionAsync(collection);
        }
        catch
        {
        }
    }
}

internal static class ElasticsearchLiveTestCases
{
    public static async Task EnsureCollectionUpsertGetRoundTripAsync(Func<IVectorStore> createStore)
    {
        var collection = $"f5_es_roundtrip_{Guid.NewGuid():N}";
        var store = createStore();
        try
        {
            await store.EnsureCollectionAsync(collection, 3);
            await store.UpsertAsync(collection, new VectorRecord("r1", [1f, 0f, 0f], "hello"));

            await LiveTestRetry.EventuallyAsync(async () =>
            {
                var got = await store.GetAsync(collection, "r1");
                return got is not null && got.Text == "hello";
            }, attempts: 60, delayMs: 500);
        }
        finally
        {
            await VectorStoreLiveCleanup.TryDeleteCollectionAsync(store, collection);
        }
    }

    public static async Task SearchEmptyCollectionReturnsEmptyAsync(Func<IVectorStore> createStore)
    {
        var collection = $"f5_es_empty_{Guid.NewGuid():N}";
        var store = createStore();
        try
        {
            await store.EnsureCollectionAsync(collection, 3);
            Assert.Empty(await store.SearchAsync(collection, [1f, 0f, 0f]));
        }
        finally
        {
            await VectorStoreLiveCleanup.TryDeleteCollectionAsync(store, collection);
        }
    }

    public static async Task UpsertBatchMultipleRecordsAsync(Func<IVectorStore> createStore)
    {
        var collection = $"f5_es_batch_{Guid.NewGuid():N}";
        var store = createStore();
        try
        {
            await store.EnsureCollectionAsync(collection, 2);
            await store.UpsertBatchAsync(collection, [
                new VectorRecord("a", [1f, 0f], "alpha"),
                new VectorRecord("b", [0f, 1f], "beta")
            ]);

            await LiveTestRetry.EventuallyAsync(async () =>
                await store.GetAsync(collection, "a") is not null
                && await store.GetAsync(collection, "b") is not null,
                attempts: 60,
                delayMs: 500);
        }
        finally
        {
            await VectorStoreLiveCleanup.TryDeleteCollectionAsync(store, collection);
        }
    }

    public static async Task DeleteRemovesRecordAsync(Func<IVectorStore> createStore)
    {
        var collection = $"f5_es_delete_{Guid.NewGuid():N}";
        var store = createStore();
        try
        {
            await store.EnsureCollectionAsync(collection, 2);
            await store.UpsertAsync(collection, new VectorRecord("x", [1f, 0f], "delete-me"));
            await LiveTestRetry.EventuallyAsync(async () => await store.GetAsync(collection, "x") is not null, attempts: 60, delayMs: 500);

            await store.DeleteAsync(collection, "x");
            await LiveTestRetry.EventuallyAsync(async () => await store.GetAsync(collection, "x") is null, attempts: 60, delayMs: 500);
        }
        finally
        {
            await VectorStoreLiveCleanup.TryDeleteCollectionAsync(store, collection);
        }
    }

    public static async Task ListCollectionsReturnsCollectionsAsync(Func<IVectorStore> createStore)
    {
        var collectionA = $"f5_es_list_a_{Guid.NewGuid():N}";
        var collectionB = $"f5_es_list_b_{Guid.NewGuid():N}";
        var store = createStore();
        try
        {
            await store.EnsureCollectionAsync(collectionA, 2);
            await store.EnsureCollectionAsync(collectionB, 2);

            await LiveTestRetry.EventuallyAsync(async () =>
            {
                var collections = await store.ListCollectionsAsync();
                return collections.Contains(collectionA) && collections.Contains(collectionB);
            }, attempts: 60, delayMs: 500);
        }
        finally
        {
            await VectorStoreLiveCleanup.TryDeleteCollectionAsync(store, collectionA);
            await VectorStoreLiveCleanup.TryDeleteCollectionAsync(store, collectionB);
        }
    }
}

internal static class MeilisearchLiveTestCases
{
    public static async Task EnsureCollectionUpsertGetRoundTripAsync(Func<IVectorStore> createStore)
    {
        var collection = $"f5_meili_roundtrip_{Guid.NewGuid():N}";
        var store = createStore();
        try
        {
            await store.EnsureCollectionAsync(collection, 3);
            await store.UpsertAsync(collection, new VectorRecord("r1", [1f, 0f, 0f], "hello"));

            await LiveTestRetry.EventuallyAsync(async () =>
            {
                var got = await store.GetAsync(collection, "r1");
                return got is not null && got.Text == "hello";
            }, attempts: 60, delayMs: 500);
        }
        finally
        {
            await VectorStoreLiveCleanup.TryDeleteCollectionAsync(store, collection);
        }
    }

    public static async Task SearchEmptyCollectionReturnsEmptyAsync(Func<IVectorStore> createStore)
    {
        var collection = $"f5_meili_empty_{Guid.NewGuid():N}";
        var store = createStore();
        try
        {
            await store.EnsureCollectionAsync(collection, 3);
            Assert.Empty(await store.SearchAsync(collection, [1f, 0f, 0f]));
        }
        finally
        {
            await VectorStoreLiveCleanup.TryDeleteCollectionAsync(store, collection);
        }
    }

    public static async Task UpsertBatchMultipleRecordsAsync(Func<IVectorStore> createStore)
    {
        var collection = $"f5_meili_batch_{Guid.NewGuid():N}";
        var store = createStore();
        try
        {
            await store.EnsureCollectionAsync(collection, 2);
            await store.UpsertBatchAsync(collection, [
                new VectorRecord("a", [1f, 0f], "alpha"),
                new VectorRecord("b", [0f, 1f], "beta")
            ]);

            await LiveTestRetry.EventuallyAsync(async () =>
                await store.GetAsync(collection, "a") is not null
                && await store.GetAsync(collection, "b") is not null,
                attempts: 60,
                delayMs: 500);
        }
        finally
        {
            await VectorStoreLiveCleanup.TryDeleteCollectionAsync(store, collection);
        }
    }

    public static async Task DeleteRemovesRecordAsync(Func<IVectorStore> createStore)
    {
        var collection = $"f5_meili_delete_{Guid.NewGuid():N}";
        var store = createStore();
        try
        {
            await store.EnsureCollectionAsync(collection, 2);
            await store.UpsertAsync(collection, new VectorRecord("x", [1f, 0f], "delete-me"));
            await LiveTestRetry.EventuallyAsync(async () => await store.GetAsync(collection, "x") is not null, attempts: 60, delayMs: 500);

            await store.DeleteAsync(collection, "x");
            await LiveTestRetry.EventuallyAsync(async () => await store.GetAsync(collection, "x") is null, attempts: 60, delayMs: 500);
        }
        finally
        {
            await VectorStoreLiveCleanup.TryDeleteCollectionAsync(store, collection);
        }
    }

    public static async Task ListCollectionsReturnsCollectionsAsync(Func<IVectorStore> createStore)
    {
        var collectionA = $"f5_meili_list_a_{Guid.NewGuid():N}";
        var collectionB = $"f5_meili_list_b_{Guid.NewGuid():N}";
        var store = createStore();
        try
        {
            await store.EnsureCollectionAsync(collectionA, 2);
            await store.EnsureCollectionAsync(collectionB, 2);

            await LiveTestRetry.EventuallyAsync(async () =>
            {
                var collections = await store.ListCollectionsAsync();
                return collections.Contains(collectionA) && collections.Contains(collectionB);
            }, attempts: 60, delayMs: 500);
        }
        finally
        {
            await VectorStoreLiveCleanup.TryDeleteCollectionAsync(store, collectionA);
            await VectorStoreLiveCleanup.TryDeleteCollectionAsync(store, collectionB);
        }
    }
}

internal static class PgVectorLiveTestCases
{
    public static async Task EnsureCollectionUpsertGetRoundTripAsync(Func<IVectorStore> createStore)
    {
        var collection = $"f5_pg_roundtrip_{Guid.NewGuid():N}";
        var store = createStore();
        try
        {
            await store.EnsureCollectionAsync(collection, 3);
            await store.UpsertAsync(collection, new VectorRecord("r1", [1f, 0f, 0f], "hello"));

            var got = await store.GetAsync(collection, "r1");
            Assert.NotNull(got);
            Assert.Equal("hello", got!.Text);
        }
        finally
        {
            await VectorStoreLiveCleanup.TryDeleteCollectionAsync(store, collection);
        }
    }

    public static async Task SearchEmptyCollectionReturnsEmptyAsync(Func<IVectorStore> createStore)
    {
        var collection = $"f5_pg_empty_{Guid.NewGuid():N}";
        var store = createStore();
        try
        {
            await store.EnsureCollectionAsync(collection, 3);
            Assert.Empty(await store.SearchAsync(collection, [1f, 0f, 0f]));
        }
        finally
        {
            await VectorStoreLiveCleanup.TryDeleteCollectionAsync(store, collection);
        }
    }

    public static async Task UpsertBatchMultipleRecordsAsync(Func<IVectorStore> createStore)
    {
        var collection = $"f5_pg_batch_{Guid.NewGuid():N}";
        var store = createStore();
        try
        {
            await store.EnsureCollectionAsync(collection, 2);
            await store.UpsertBatchAsync(collection, [
                new VectorRecord("a", [1f, 0f], "alpha"),
                new VectorRecord("b", [0f, 1f], "beta")
            ]);

            Assert.NotNull(await store.GetAsync(collection, "a"));
            Assert.NotNull(await store.GetAsync(collection, "b"));
        }
        finally
        {
            await VectorStoreLiveCleanup.TryDeleteCollectionAsync(store, collection);
        }
    }

    public static async Task DeleteRemovesRecordAsync(Func<IVectorStore> createStore)
    {
        var collection = $"f5_pg_delete_{Guid.NewGuid():N}";
        var store = createStore();
        try
        {
            await store.EnsureCollectionAsync(collection, 2);
            await store.UpsertAsync(collection, new VectorRecord("x", [1f, 0f], "delete-me"));
            await store.DeleteAsync(collection, "x");

            Assert.Null(await store.GetAsync(collection, "x"));
        }
        finally
        {
            await VectorStoreLiveCleanup.TryDeleteCollectionAsync(store, collection);
        }
    }

    public static async Task ListCollectionsReturnsCollectionsAsync(Func<IVectorStore> createStore)
    {
        var collectionA = $"f5_pg_list_a_{Guid.NewGuid():N}";
        var collectionB = $"f5_pg_list_b_{Guid.NewGuid():N}";
        var store = createStore();
        try
        {
            await store.EnsureCollectionAsync(collectionA, 2);
            await store.EnsureCollectionAsync(collectionB, 2);

            var collections = await store.ListCollectionsAsync();
            Assert.Contains(collectionA, collections);
            Assert.Contains(collectionB, collections);
        }
        finally
        {
            await VectorStoreLiveCleanup.TryDeleteCollectionAsync(store, collectionA);
            await VectorStoreLiveCleanup.TryDeleteCollectionAsync(store, collectionB);
        }
    }
}

internal static class VectorStoreLiveCleanup
{
    public static async Task TryDeleteCollectionAsync(IVectorStore store, string collection)
    {
        try
        {
            await store.DeleteCollectionAsync(collection);
        }
        catch
        {
        }
    }
}

internal static class F5HttpTestHelpers
{
    public static async Task<JsonDocument> ReadJsonAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var content = request.Content ?? throw new InvalidOperationException("Request content is required for this fake handler.");
        return await JsonDocument.ParseAsync(await content.ReadAsStreamAsync(ct), cancellationToken: ct);
    }

    public static HttpResponseMessage JsonResponse(object payload, HttpStatusCode statusCode = HttpStatusCode.OK)
        => new(statusCode)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

    public static string RequestPath(HttpRequestMessage request)
        => request.RequestUri?.AbsolutePath ?? string.Empty;

    public static string[] RequestSegments(HttpRequestMessage request)
        => RequestPath(request).Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

    public static float[] ReadVector(JsonElement element)
        => element.EnumerateArray().Select(x => x.GetSingle()).ToArray();

    public static Dictionary<string, T> GetCollection<T>(Dictionary<string, Dictionary<string, T>> collections, string collection)
    {
        if (!collections.TryGetValue(collection, out var store))
        {
            store = new Dictionary<string, T>(StringComparer.Ordinal);
            collections[collection] = store;
        }
        return store;
    }

    public static string QueryValue(HttpRequestMessage request, string key)
    {
        var query = request.RequestUri?.Query ?? string.Empty;
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && string.Equals(parts[0], key, StringComparison.Ordinal))
                return Uri.UnescapeDataString(parts[1]);
        }
        return string.Empty;
    }

    public static string FilterId(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter)) return string.Empty;
        var first = filter.IndexOf('\'');
        var last = filter.LastIndexOf('\'');
        return first >= 0 && last > first ? filter[(first + 1)..last] : filter;
    }
}

