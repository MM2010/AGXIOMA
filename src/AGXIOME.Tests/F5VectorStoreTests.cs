using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
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
//   • HTTP stores (Pinecone, Weaviate, Milvus, Chroma, Vespa, LanceDB, ScaNN):
//       provider, constructor, empty search/list, missing-record get, batch upsert, and delete paths
//       run via deterministic stateful fake HttpClient handlers.
//   • Vald uses a deterministic fake transport for unit coverage plus a live standalone
//       agent-ngt fixture because the real runtime is gRPC-first.
//   • SDK-dependent stores (Qdrant, pgvector variants, SingleStore, Elasticsearch,
//     Meilisearch, Redis, MongoDB, Cosmos, SqlServer):
//       constructor/provider tests run; stateful integration paths still require live server / cloud service.
//
// Total: 210 tests (7 × 30 stores).
// ─────────────────────────────────────────────────────────────────────────────

internal static class LiveVectorStoreTestSupport
{
    internal const string MongoAtlasConnectionStringEnv = "AGXIOME_MONGODB_ATLAS_CONNECTION_STRING";
    internal const string MongoAtlasDatabaseEnv = "AGXIOME_MONGODB_ATLAS_DATABASE";
    internal const string MongoAtlasCollectionEnv = "AGXIOME_MONGODB_ATLAS_COLLECTION";
    internal const string CosmosVCoreConnectionStringEnv = "AGXIOME_COSMOS_VCORE_CONNECTION_STRING";
    internal const string CosmosVCoreDatabaseEnv = "AGXIOME_COSMOS_VCORE_DATABASE";
    internal const string CosmosVCoreCollectionEnv = "AGXIOME_COSMOS_VCORE_COLLECTION";
    internal const string SqlServerVectorConnectionStringEnv = "AGXIOME_SQLSERVER_VECTOR_CONNECTION_STRING";
    internal const string ScaNNEndpointEnv = "AGXIOME_SCANN_ENDPOINT";

    internal static string RequireEnvironmentVariable(string envName, string requirement)
    {
        var value = Environment.GetEnvironmentVariable(envName);
        if (!string.IsNullOrWhiteSpace(value))
            return value;

        throw new InvalidOperationException($"Missing required environment variable {envName} for {requirement}.");
    }

    internal static string UniqueName(string prefix)
        => $"{prefix}_{Guid.NewGuid():N}";

    internal static RocksDbHnswStore CreateRocksDbStoreOrSkip(string path)
        => new RocksDbHnswStore(path);

    internal static HttpClient CreateScaNNHttpClientOrSkip()
    {
        var endpoint = RequireEnvironmentVariable(ScaNNEndpointEnv, "a live ScaNN HTTP endpoint");
        return new HttpClient
        {
            BaseAddress = new Uri(EnsureTrailingSlash(endpoint), UriKind.Absolute),
        };
    }

    private static string EnsureTrailingSlash(string value)
        => value.EndsWith("/", StringComparison.Ordinal) ? value : value + "/";

}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class RequiresEnvironmentFactAttribute : FactAttribute
{
    public RequiresEnvironmentFactAttribute(params string[] envNames)
    {
        var missing = envNames
            .Where(envName => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(envName)))
            .ToArray();

        if (missing.Length > 0)
            Skip = $"requires environment variables: {string.Join(", ", missing)}";
    }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class RequiresRocksDbNativeFactAttribute : FactAttribute
{
    private static readonly string[] CandidateDirectories = [
        "",
        "runtimes\\win-x64\\native",
        "native",
        "native\\amd64",
        "amd64"
    ];

    private static readonly string[] CandidateFiles = [
        "rocksdb-6.2.2.dll",
        "rocksdb-6.2.dll",
        "rocksdb-6.dll",
        "rocksdb.dll"
    ];

    public RequiresRocksDbNativeFactAttribute()
    {
        if (!HasNativeLibrary())
            Skip = "requires native RocksDB library (rocksdb*.dll) in the test runtime output";
    }

    private static bool HasNativeLibrary()
    {
        var baseDirectory = AppContext.BaseDirectory;

        foreach (var directory in CandidateDirectories)
        {
            var root = directory.Length == 0 ? baseDirectory : Path.Combine(baseDirectory, directory);

            foreach (var file in CandidateFiles)
            {
                if (File.Exists(Path.Combine(root, file)))
                    return true;
            }
        }

        return false;
    }
}

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
    {
        var store = new ChromaDbCloudStore(StatefulHttp());
        await store.EnsureCollectionAsync("missing", 2);
        Assert.Null(await store.GetAsync("missing", "r1"));
    }

    [Fact] public async Task Search_EmptyCollection_ReturnsEmpty()
    {
        var store = new ChromaDbCloudStore(StatefulHttp());
        await store.EnsureCollectionAsync("empty", 3);
        var results = await store.SearchAsync("empty", new float[] { 1f, 0f, 0f });
        Assert.Empty(results);
    }

    [Fact] public async Task UpsertBatch_MultipleRecords()
    {
        var store = new ChromaDbCloudStore(StatefulHttp());
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
        var store = new ChromaDbCloudStore(StatefulHttp());
        await store.EnsureCollectionAsync("del", 2);
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
    [Fact] public async Task ProviderType_IsSingleStoreVector()
        => Assert.Equal(VectorDbProviderType.SingleStoreVector,
            new SingleStoreVectorStore(new SingleStoreVectorOptions { ConnectionString = "Server=localhost;Database=test" }).ProviderType);

    [Fact] public async Task Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new SingleStoreVectorStore(null!));

}

public sealed class SingleStoreVectorStoreLiveTests
{
    private static async Task RunAsync(Func<IVectorStore, Task> assertion)
    {
        await AsyncFixtureRunner.UseAsync<SingleStoreContainerFixture>(fixture =>
        {
            var store = new SingleStoreVectorStore(new SingleStoreVectorOptions
            {
                ConnectionString = fixture.ConnectionString!,
            });
            return assertion(store);
        });
    }

    [RequiresSingleStoreFact] public Task EnsureCollection_Upsert_Get_RoundTrip()
        => RunAsync(store => PgVectorLiveTestCases.EnsureCollectionUpsertGetRoundTripAsync(() => store));

    [RequiresSingleStoreFact] public Task Search_EmptyCollection_ReturnsEmpty()
        => RunAsync(store => PgVectorLiveTestCases.SearchEmptyCollectionReturnsEmptyAsync(() => store));

    [RequiresSingleStoreFact] public Task UpsertBatch_MultipleRecords()
        => RunAsync(store => PgVectorLiveTestCases.UpsertBatchMultipleRecordsAsync(() => store));

    [RequiresSingleStoreFact] public Task Delete_RemovesRecord()
        => RunAsync(store => PgVectorLiveTestCases.DeleteRemovesRecordAsync(() => store));

    [RequiresSingleStoreFact] public Task ListCollections_ReturnsCollections()
        => RunAsync(store => PgVectorLiveTestCases.ListCollectionsReturnsCollectionsAsync(() => store));
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

public sealed class WeaviateSelfHostedContainerTests(WeaviateContainerFixture fixture) : IClassFixture<WeaviateContainerFixture>
{
    private WeaviateSelfHostedStore CreateStore()
        => new(new HttpClient { BaseAddress = new Uri(fixture.Endpoint) });

    [Fact] public Task EnsureCollection_Upsert_Get_RoundTrip()
        => WeaviateLiveTestCases.EnsureCollectionUpsertGetRoundTripAsync(CreateStore);

    [Fact] public Task Search_EmptyCollection_ReturnsEmpty()
        => WeaviateLiveTestCases.SearchEmptyCollectionReturnsEmptyAsync(CreateStore);

    [Fact] public Task UpsertBatch_MultipleRecords()
        => WeaviateLiveTestCases.UpsertBatchMultipleRecordsAsync(CreateStore);

    [Fact] public Task Delete_RemovesRecord()
        => WeaviateLiveTestCases.DeleteRemovesRecordAsync(CreateStore);

    [Fact] public Task ListCollections_ReturnsCollections()
        => WeaviateLiveTestCases.ListCollectionsReturnsCollectionsAsync(CreateStore);
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

public sealed class MilvusSelfHostedContainerTests(MilvusContainerFixture fixture) : IClassFixture<MilvusContainerFixture>
{
    private MilvusSelfHostedStore CreateStore()
        => new(new HttpClient { BaseAddress = new Uri(fixture.Endpoint) });

    [Fact] public Task EnsureCollection_Upsert_Get_RoundTrip()
        => MilvusLiveTestCases.EnsureCollectionUpsertGetRoundTripAsync(CreateStore);

    [Fact] public Task Search_EmptyCollection_ReturnsEmpty()
        => MilvusLiveTestCases.SearchEmptyCollectionReturnsEmptyAsync(CreateStore);

    [Fact] public Task UpsertBatch_MultipleRecords()
        => MilvusLiveTestCases.UpsertBatchMultipleRecordsAsync(CreateStore);

    [Fact] public Task Delete_RemovesRecord()
        => MilvusLiveTestCases.DeleteRemovesRecordAsync(CreateStore);

    [Fact] public Task ListCollections_ReturnsCollections()
        => MilvusLiveTestCases.ListCollectionsReturnsCollectionsAsync(CreateStore);
}

public sealed class ChromaSelfHostedContainerTests(ChromaContainerFixture fixture) : IClassFixture<ChromaContainerFixture>
{
    private ChromaDbSelfHostedStore CreateStore()
        => new(new HttpClient { BaseAddress = new Uri(fixture.Endpoint) });

    [Fact] public Task EnsureCollection_Upsert_Get_RoundTrip()
        => ChromaLiveTestCases.EnsureCollectionUpsertGetRoundTripAsync(CreateStore);

    [Fact] public Task Search_EmptyCollection_ReturnsEmpty()
        => ChromaLiveTestCases.SearchEmptyCollectionReturnsEmptyAsync(CreateStore);

    [Fact] public Task UpsertBatch_MultipleRecords()
        => ChromaLiveTestCases.UpsertBatchMultipleRecordsAsync(CreateStore);

    [Fact] public Task Delete_RemovesRecord()
        => ChromaLiveTestCases.DeleteRemovesRecordAsync(CreateStore);

    [Fact] public Task ListCollections_ReturnsCollections()
        => ChromaLiveTestCases.ListCollectionsReturnsCollectionsAsync(CreateStore);
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
        var store = new ChromaDbSelfHostedStore(StatefulHttp());
        await store.EnsureCollectionAsync("empty", 3);
        var results = await store.SearchAsync("empty", [1f, 0f, 0f]);
        Assert.Empty(results);
    }

    [Fact] public async Task UpsertBatch_MultipleRecords()
    {
        var store = new ChromaDbSelfHostedStore(StatefulHttp());
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
        var store = new ChromaDbSelfHostedStore(StatefulHttp());
        await store.EnsureCollectionAsync("del", 2);
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

public class ValdStoreTests(ValdContainerFixture fixture) : IClassFixture<ValdContainerFixture>
{
    private static ValdStore BuildUnitStore()
        => new(new InMemoryValdClient());

    private ValdStore CreateStore()
        => new(fixture.Endpoint);

    [Fact] public async Task ProviderType_IsVald()
        => Assert.Equal(VectorDbProviderType.Vald, BuildUnitStore().ProviderType);

    [Fact] public async Task Constructor_NullClient_Throws()
        => Assert.Throws<ArgumentNullException>(() => new ValdStore((IValdClient)null!));

    [Fact] public async Task Get_MissingRecord_ReturnsNull()
        => Assert.Null(await BuildUnitStore().GetAsync("missing", "r1"));

    [Fact] public async Task Search_EmptyCollection_ReturnsEmpty()
    {
        var store = BuildUnitStore();
        var results = await store.SearchAsync("empty", new float[] { 1f, 0f, 0f });
        Assert.Empty(results);
    }

    [Fact] public async Task UpsertBatch_MultipleRecords()
    {
        var store = BuildUnitStore();
        await store.UpsertBatchAsync("batch", [
            new VectorRecord("a", [1f, 0f]),
            new VectorRecord("b", [0f, 1f])
        ]);

        Assert.NotNull(await store.GetAsync("batch", "a"));
        Assert.NotNull(await store.GetAsync("batch", "b"));
    }

    [Fact] public async Task Delete_RemovesRecord()
    {
        var store = BuildUnitStore();
        await store.UpsertBatchAsync("del", [new VectorRecord("x", [1f, 0f])]);
        await store.DeleteAsync("del", "x");

        Assert.Null(await store.GetAsync("del", "x"));
    }

    [Fact] public Task EnsureCollection_Upsert_Get_RoundTrip()
        => ValdLiveTestCases.EnsureCollectionUpsertGetRoundTripAsync(CreateStore);

    [Fact] public Task Search_EmptyCollection_Live_ReturnsEmpty()
        => ValdLiveTestCases.SearchEmptyCollectionReturnsEmptyAsync(CreateStore);

    [Fact] public Task UpsertBatch_Live_MultipleRecords()
        => ValdLiveTestCases.UpsertBatchMultipleRecordsAsync(CreateStore);

    [Fact] public Task Delete_Live_RemovesRecord()
        => ValdLiveTestCases.DeleteRemovesRecordAsync(CreateStore);

    [Fact] public async Task ListCollections_ReturnsCollections()
        => Assert.Empty(await CreateStore().ListCollectionsAsync());
}

// ─── VD-18 — Vespa ────────────────────────────────────────────────────────────

public class VespaStoreTests(VespaContainerFixture fixture) : IClassFixture<VespaContainerFixture>
{
    private static HttpClient MockHttp(string body = "{\"root\":{\"fields\":{\"totalCount\":0},\"children\":[]}}", HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new HttpClient(new TestHttpMessageHandler(statusCode, body))
        { BaseAddress = new Uri("http://localhost:8080/") };

    private static HttpClient StatefulHttp()
        => new HttpClient(new VespaStatefulHandler()) { BaseAddress = new Uri("http://localhost:8080/") };

    private VespaStore CreateStore()
        => new(new HttpClient { BaseAddress = new Uri(fixture.Endpoint) });

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

    [Fact] public Task EnsureCollection_Upsert_Get_RoundTrip()
        => VespaLiveTestCases.EnsureCollectionUpsertGetRoundTripAsync(CreateStore);

    [Fact] public Task Search_ReturnsNormalizedIds()
        => VespaLiveTestCases.SearchReturnsNormalizedIdsAsync(CreateStore);

    [Fact] public Task UpsertBatch_Live_MultipleRecords()
        => VespaLiveTestCases.UpsertBatchMultipleRecordsAsync(CreateStore);

    [Fact] public Task Delete_Live_RemovesRecord()
        => VespaLiveTestCases.DeleteRemovesRecordAsync(CreateStore);

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

public class MongoDbAtlasVectorStoreLiveTests
{
    [RequiresEnvironmentFact(
        LiveVectorStoreTestSupport.MongoAtlasConnectionStringEnv,
        LiveVectorStoreTestSupport.MongoAtlasDatabaseEnv,
        LiveVectorStoreTestSupport.MongoAtlasCollectionEnv)]
    public async Task Live_Upsert_Get_Search_Delete_RoundTrip()
    {
        var connectionString = LiveVectorStoreTestSupport.RequireEnvironmentVariable(
            LiveVectorStoreTestSupport.MongoAtlasConnectionStringEnv,
            "a live MongoDB Atlas connection string");
        var databaseName = LiveVectorStoreTestSupport.RequireEnvironmentVariable(
            LiveVectorStoreTestSupport.MongoAtlasDatabaseEnv,
            "a live MongoDB Atlas database name");
        var collection = LiveVectorStoreTestSupport.RequireEnvironmentVariable(
            LiveVectorStoreTestSupport.MongoAtlasCollectionEnv,
            "a MongoDB Atlas collection that already has the expected vector_index configured");

        var client = new MongoClient(connectionString);
        var store = new MongoDbAtlasVectorStore(client.GetDatabase(databaseName));
        var idA = LiveVectorStoreTestSupport.UniqueName("atlas_a");
        var idB = LiveVectorStoreTestSupport.UniqueName("atlas_b");

        try
        {
            await store.UpsertBatchAsync(collection, [
                new VectorRecord(idA, [1f, 0f, 0f], "alpha"),
                new VectorRecord(idB, [0f, 1f, 0f], "beta")
            ]);

            var got = await store.GetAsync(collection, idA);
            Assert.NotNull(got);
            Assert.Equal(idA, got!.Id);
            Assert.Equal("alpha", got.Text);

            var results = await store.SearchAsync(collection, [1f, 0f, 0f], topK: 5);
            Assert.Contains(results, item => item.Id == idA);

            await store.DeleteAsync(collection, idA);
            await store.DeleteAsync(collection, idB);

            Assert.Null(await store.GetAsync(collection, idA));
        }
        finally
        {
            try { await store.DeleteAsync(collection, idA); } catch { }
            try { await store.DeleteAsync(collection, idB); } catch { }
        }
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

    [RequiresEnvironmentFact(
        LiveVectorStoreTestSupport.CosmosVCoreConnectionStringEnv,
        LiveVectorStoreTestSupport.CosmosVCoreDatabaseEnv,
        LiveVectorStoreTestSupport.CosmosVCoreCollectionEnv)]
    public async Task EnsureCollection_Upsert_Get_RoundTrip()
    {
        var connectionString = LiveVectorStoreTestSupport.RequireEnvironmentVariable(
            LiveVectorStoreTestSupport.CosmosVCoreConnectionStringEnv,
            "a live Azure Cosmos DB vCore connection string");
        var databaseId = LiveVectorStoreTestSupport.RequireEnvironmentVariable(
            LiveVectorStoreTestSupport.CosmosVCoreDatabaseEnv,
            "a live Azure Cosmos DB vCore database name");
        var collection = LiveVectorStoreTestSupport.RequireEnvironmentVariable(
            LiveVectorStoreTestSupport.CosmosVCoreCollectionEnv,
            "a Cosmos DB vCore container with vector search enabled");

        using var client = new CosmosClient(connectionString);
        var store = new CosmosDbVCoreStore(client, databaseId);
        var id = LiveVectorStoreTestSupport.UniqueName("cosmos_roundtrip");

        try
        {
            await store.EnsureCollectionAsync(collection, 3);
            await store.UpsertAsync(collection, new VectorRecord(id, [1f, 0f, 0f], "hello"));

            var got = await store.GetAsync(collection, id);
            Assert.NotNull(got);
            Assert.Equal(id, got!.Id);
            Assert.Equal("hello", got.Text);
        }
        finally
        {
            try { await store.DeleteAsync(collection, id); } catch { }
        }
    }

    [RequiresEnvironmentFact(
        LiveVectorStoreTestSupport.CosmosVCoreConnectionStringEnv,
        LiveVectorStoreTestSupport.CosmosVCoreDatabaseEnv,
        LiveVectorStoreTestSupport.CosmosVCoreCollectionEnv)]
    public async Task Search_EmptyCollection_ReturnsEmpty()
    {
        var connectionString = LiveVectorStoreTestSupport.RequireEnvironmentVariable(
            LiveVectorStoreTestSupport.CosmosVCoreConnectionStringEnv,
            "a live Azure Cosmos DB vCore connection string");
        var databaseId = LiveVectorStoreTestSupport.RequireEnvironmentVariable(
            LiveVectorStoreTestSupport.CosmosVCoreDatabaseEnv,
            "a live Azure Cosmos DB vCore database name");
        var collection = LiveVectorStoreTestSupport.RequireEnvironmentVariable(
            LiveVectorStoreTestSupport.CosmosVCoreCollectionEnv,
            "a Cosmos DB vCore container with vector search enabled");

        using var client = new CosmosClient(connectionString);
        var store = new CosmosDbVCoreStore(client, databaseId);
        var results = await store.SearchAsync(collection, [9f, 9f, 9f], topK: 5, minScore: 0.999f);
        Assert.Empty(results);
    }

    [RequiresEnvironmentFact(
        LiveVectorStoreTestSupport.CosmosVCoreConnectionStringEnv,
        LiveVectorStoreTestSupport.CosmosVCoreDatabaseEnv,
        LiveVectorStoreTestSupport.CosmosVCoreCollectionEnv)]
    public async Task UpsertBatch_MultipleRecords()
    {
        var connectionString = LiveVectorStoreTestSupport.RequireEnvironmentVariable(
            LiveVectorStoreTestSupport.CosmosVCoreConnectionStringEnv,
            "a live Azure Cosmos DB vCore connection string");
        var databaseId = LiveVectorStoreTestSupport.RequireEnvironmentVariable(
            LiveVectorStoreTestSupport.CosmosVCoreDatabaseEnv,
            "a live Azure Cosmos DB vCore database name");
        var collection = LiveVectorStoreTestSupport.RequireEnvironmentVariable(
            LiveVectorStoreTestSupport.CosmosVCoreCollectionEnv,
            "a Cosmos DB vCore container with vector search enabled");

        using var client = new CosmosClient(connectionString);
        var store = new CosmosDbVCoreStore(client, databaseId);
        var idA = LiveVectorStoreTestSupport.UniqueName("cosmos_a");
        var idB = LiveVectorStoreTestSupport.UniqueName("cosmos_b");

        try
        {
            await store.UpsertBatchAsync(collection, [
                new VectorRecord(idA, [1f, 0f], "alpha"),
                new VectorRecord(idB, [0f, 1f], "beta")
            ]);

            Assert.NotNull(await store.GetAsync(collection, idA));
            Assert.NotNull(await store.GetAsync(collection, idB));
        }
        finally
        {
            try { await store.DeleteAsync(collection, idA); } catch { }
            try { await store.DeleteAsync(collection, idB); } catch { }
        }
    }

    [RequiresEnvironmentFact(
        LiveVectorStoreTestSupport.CosmosVCoreConnectionStringEnv,
        LiveVectorStoreTestSupport.CosmosVCoreDatabaseEnv,
        LiveVectorStoreTestSupport.CosmosVCoreCollectionEnv)]
    public async Task Delete_RemovesRecord()
    {
        var connectionString = LiveVectorStoreTestSupport.RequireEnvironmentVariable(
            LiveVectorStoreTestSupport.CosmosVCoreConnectionStringEnv,
            "a live Azure Cosmos DB vCore connection string");
        var databaseId = LiveVectorStoreTestSupport.RequireEnvironmentVariable(
            LiveVectorStoreTestSupport.CosmosVCoreDatabaseEnv,
            "a live Azure Cosmos DB vCore database name");
        var collection = LiveVectorStoreTestSupport.RequireEnvironmentVariable(
            LiveVectorStoreTestSupport.CosmosVCoreCollectionEnv,
            "a Cosmos DB vCore container with vector search enabled");

        using var client = new CosmosClient(connectionString);
        var store = new CosmosDbVCoreStore(client, databaseId);
        var id = LiveVectorStoreTestSupport.UniqueName("cosmos_delete");

        await store.UpsertAsync(collection, new VectorRecord(id, [1f, 0f], "delete-me"));
        await store.DeleteAsync(collection, id);

        Assert.Null(await store.GetAsync(collection, id));
    }
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
    public class ScaNNStoreLiveTests
    {
        [RequiresEnvironmentFact(LiveVectorStoreTestSupport.ScaNNEndpointEnv)]
        public async Task Live_EnsureCollection_Upsert_Search_Delete_RoundTrip()
        {
            using var http = LiveVectorStoreTestSupport.CreateScaNNHttpClientOrSkip();
            var store = new ScaNNStore(http);
            var collection = LiveVectorStoreTestSupport.UniqueName("scann_live");

            try
            {
                await store.EnsureCollectionAsync(collection, 3);
                await store.UpsertBatchAsync(collection, [
                    new VectorRecord("a", [1f, 0f, 0f]),
                    new VectorRecord("b", [0f, 1f, 0f])
                ]);

                Assert.NotNull(await store.GetAsync(collection, "a"));

                var results = await store.SearchAsync(collection, [1f, 0f, 0f], topK: 5);
                Assert.Contains(results, item => item.Id == "a");

                await store.DeleteAsync(collection, "a");
                Assert.Null(await store.GetAsync(collection, "a"));
            }
            finally
            {
                try { await store.DeleteCollectionAsync(collection); } catch { }
            }
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

    [RequiresRocksDbNativeFact]
    public void ProviderType_IsRocksDBHNSW()
    {
        _store = LiveVectorStoreTestSupport.CreateRocksDbStoreOrSkip(_dir);
        Assert.Equal(VectorDbProviderType.RocksDBHNSW, _store.ProviderType);
    }

    [Fact] public async Task Constructor_EmptyPath_Throws()
        => Assert.Throws<ArgumentException>(() => new RocksDbHnswStore(""));

    [RequiresRocksDbNativeFact]
    public async Task EnsureCollection_Upsert_Get_RoundTrip()
    {
        _store = LiveVectorStoreTestSupport.CreateRocksDbStoreOrSkip(_dir);
        await _store.EnsureCollectionAsync("col", 3);
        var rec = new VectorRecord("r1", new float[] { 1f, 0f, 0f }, "hello");
        await _store.UpsertAsync("col", rec);
        var got = await _store.GetAsync("col", "r1");
        Assert.NotNull(got);
        Assert.Equal("r1", got!.Id);
        Assert.Equal("hello", got.Text);
    }

    [RequiresRocksDbNativeFact]
    public async Task Search_EmptyCollection_ReturnsEmpty()
    {
        _store = LiveVectorStoreTestSupport.CreateRocksDbStoreOrSkip(_dir);
        await _store.EnsureCollectionAsync("empty", 3);
        var results = await _store.SearchAsync("empty", new float[] { 1f, 0f, 0f });
        Assert.Empty(results);
    }

    [RequiresRocksDbNativeFact]
    public async Task UpsertBatch_AllRecordsRetrievable()
    {
        _store = LiveVectorStoreTestSupport.CreateRocksDbStoreOrSkip(_dir);
        await _store.EnsureCollectionAsync("batch", 2);
        await _store.UpsertBatchAsync("batch", new[]
        {
            new VectorRecord("a", new float[] { 1f, 0f }),
            new VectorRecord("b", new float[] { 0f, 1f }),
        });
        Assert.NotNull(await _store.GetAsync("batch", "a"));
        Assert.NotNull(await _store.GetAsync("batch", "b"));
    }

    [RequiresRocksDbNativeFact]
    public async Task Delete_RemovesRecord()
    {
        _store = LiveVectorStoreTestSupport.CreateRocksDbStoreOrSkip(_dir);
        await _store.EnsureCollectionAsync("col", 2);
        await _store.UpsertAsync("col", new VectorRecord("x", new float[] { 1f, 0f }));
        await _store.DeleteAsync("col", "x");
        Assert.Null(await _store.GetAsync("col", "x"));
    }

    [RequiresRocksDbNativeFact]
    public async Task ListCollections_ReturnsCreatedCollections()
    {
        _store = LiveVectorStoreTestSupport.CreateRocksDbStoreOrSkip(_dir);
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

    [RequiresEnvironmentFact(LiveVectorStoreTestSupport.SqlServerVectorConnectionStringEnv)]
    public async Task EnsureCollection_Upsert_Get_RoundTrip()
    {
        var connectionString = LiveVectorStoreTestSupport.RequireEnvironmentVariable(
            LiveVectorStoreTestSupport.SqlServerVectorConnectionStringEnv,
            "a live SQL Server 2025+ connection string with VECTOR support");
        var store = new SqlServerVectorStore(connectionString);
        var collection = LiveVectorStoreTestSupport.UniqueName("sqlvector");

        try
        {
            await store.EnsureCollectionAsync(collection, 3);
            await store.UpsertAsync(collection, new VectorRecord("r1", [1f, 0f, 0f], "hello"));

            var got = await store.GetAsync(collection, "r1");
            Assert.NotNull(got);
            Assert.Equal("r1", got!.Id);
            Assert.Equal("hello", got.Text);
        }
        finally
        {
            try { await store.DeleteCollectionAsync(collection); } catch { }
        }
    }

    [RequiresEnvironmentFact(LiveVectorStoreTestSupport.SqlServerVectorConnectionStringEnv)]
    public async Task Search_EmptyCollection_ReturnsEmpty()
    {
        var connectionString = LiveVectorStoreTestSupport.RequireEnvironmentVariable(
            LiveVectorStoreTestSupport.SqlServerVectorConnectionStringEnv,
            "a live SQL Server 2025+ connection string with VECTOR support");
        var store = new SqlServerVectorStore(connectionString);
        var collection = LiveVectorStoreTestSupport.UniqueName("sqlvector");

        try
        {
            await store.EnsureCollectionAsync(collection, 3);
            var results = await store.SearchAsync(collection, [1f, 0f, 0f], topK: 5);
            Assert.Empty(results);
        }
        finally
        {
            try { await store.DeleteCollectionAsync(collection); } catch { }
        }
    }

    [RequiresEnvironmentFact(LiveVectorStoreTestSupport.SqlServerVectorConnectionStringEnv)]
    public async Task UpsertBatch_MultipleRecords()
    {
        var connectionString = LiveVectorStoreTestSupport.RequireEnvironmentVariable(
            LiveVectorStoreTestSupport.SqlServerVectorConnectionStringEnv,
            "a live SQL Server 2025+ connection string with VECTOR support");
        var store = new SqlServerVectorStore(connectionString);
        var collection = LiveVectorStoreTestSupport.UniqueName("sqlvector");

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
        }
    }

    [RequiresEnvironmentFact(LiveVectorStoreTestSupport.SqlServerVectorConnectionStringEnv)]
    public async Task Delete_RemovesRecord()
    {
        var connectionString = LiveVectorStoreTestSupport.RequireEnvironmentVariable(
            LiveVectorStoreTestSupport.SqlServerVectorConnectionStringEnv,
            "a live SQL Server 2025+ connection string with VECTOR support");
        var store = new SqlServerVectorStore(connectionString);
        var collection = LiveVectorStoreTestSupport.UniqueName("sqlvector");

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
        }
    }

    [RequiresEnvironmentFact(LiveVectorStoreTestSupport.SqlServerVectorConnectionStringEnv)]
    public async Task ListCollections_ReturnsCollections()
    {
        var connectionString = LiveVectorStoreTestSupport.RequireEnvironmentVariable(
            LiveVectorStoreTestSupport.SqlServerVectorConnectionStringEnv,
            "a live SQL Server 2025+ connection string with VECTOR support");
        var store = new SqlServerVectorStore(connectionString);
        var collection = LiveVectorStoreTestSupport.UniqueName("sqlvector");

        try
        {
            await store.EnsureCollectionAsync(collection, 2);
            var collections = await store.ListCollectionsAsync();
            Assert.Contains(collection, collections);
        }
        finally
        {
            try { await store.DeleteCollectionAsync(collection); } catch { }
        }
    }
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

        if (path == "/v2/vectordb/collections/load" || path == "/v2/vectordb/collections/flush")
            return JsonResponse(new { });

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

        if (path == "/v2/vectordb/entities/query")
        {
            var collection = doc.RootElement.GetProperty("collectionName").GetString() ?? string.Empty;
            var results = new List<object>();
            if (_collections.TryGetValue(collection, out var store)
                && doc.RootElement.TryGetProperty("filter", out var filterElement))
            {
                var filter = filterElement.GetString() ?? string.Empty;
                const string prefix = "id in [\"";
                const string suffix = "\"]";
                if (filter.StartsWith(prefix, StringComparison.Ordinal) && filter.EndsWith(suffix, StringComparison.Ordinal))
                {
                    var id = filter.Substring(prefix.Length, filter.Length - prefix.Length - suffix.Length)
                        .Replace("\\\"", "\"");
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
                if (doc.RootElement.TryGetProperty("id", out var ids))
                {
                    foreach (var id in ids.EnumerateArray())
                        store.Remove(id.GetString() ?? string.Empty);
                }
                else if (doc.RootElement.TryGetProperty("filter", out var filterElement))
                {
                    var filter = filterElement.GetString() ?? string.Empty;
                    const string prefix = "id in ['";
                    const string suffix = "']";
                    const string doubleQuotedPrefix = "id in [\"";
                    const string doubleQuotedSuffix = "\"]";
                    if (filter.StartsWith(prefix, StringComparison.Ordinal) && filter.EndsWith(suffix, StringComparison.Ordinal))
                    {
                        var id = filter.Substring(prefix.Length, filter.Length - prefix.Length - suffix.Length)
                            .Replace("\\'", "'");
                        store.Remove(id);
                    }
                    else if (filter.StartsWith(doubleQuotedPrefix, StringComparison.Ordinal) && filter.EndsWith(doubleQuotedSuffix, StringComparison.Ordinal))
                    {
                        var id = filter.Substring(doubleQuotedPrefix.Length, filter.Length - doubleQuotedPrefix.Length - doubleQuotedSuffix.Length)
                            .Replace("\\\"", "\"");
                        store.Remove(id);
                    }
                }
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
        if (segments.Length == 7 && request.Method == HttpMethod.Post
            && segments[0] == "api" && segments[1] == "v2"
            && segments[2] == "tenants" && segments[4] == "databases" && segments[6] == "collections")
        {
            using var createDoc = await ReadJsonAsync(request, cancellationToken);
            var collectionName = createDoc.RootElement.GetProperty("name").GetString() ?? string.Empty;
            _knownCollections.Add(collectionName);
            GetCollection(_collections, collectionName);
            return JsonResponse(new { id = collectionName, name = collectionName });
        }

        if (segments.Length == 7 && request.Method == HttpMethod.Get
            && segments[0] == "api" && segments[1] == "v2"
            && segments[2] == "tenants" && segments[4] == "databases" && segments[6] == "collections")
            return JsonResponse(_knownCollections.Select(name => new { id = name, name }).ToArray());

        if (segments.Length == 8
            && segments[0] == "api" && segments[1] == "v2"
            && segments[2] == "tenants" && segments[4] == "databases" && segments[6] == "collections")
        {
            if (request.Method == HttpMethod.Delete)
            {
                var collectionName = Uri.UnescapeDataString(segments[7]);
                _knownCollections.Remove(collectionName);
                _collections.Remove(collectionName);
                return JsonResponse(new { });
            }
        }

        if (segments.Length < 9
            || segments[0] != "api" || segments[1] != "v2"
            || segments[2] != "tenants" || segments[4] != "databases" || segments[6] != "collections")
            return JsonResponse(Array.Empty<object>());

        var collection = Uri.UnescapeDataString(segments[7]);
        var action = segments[8];
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

internal sealed class InMemoryValdClient : IValdClient
{
    private readonly Dictionary<string, VectorRecord> _records = new(StringComparer.Ordinal);

    public Task UpsertAsync(string collection, VectorRecord record, CancellationToken ct = default)
    {
        _records[ValdKey(collection, record.Id)] = new VectorRecord(record.Id, [.. record.Vector], record.Text);
        return Task.CompletedTask;
    }

    public Task UpsertBatchAsync(string collection, IReadOnlyList<VectorRecord> records, CancellationToken ct = default)
    {
        foreach (var record in records)
        {
            _records[ValdKey(collection, record.Id)] = new VectorRecord(record.Id, [.. record.Vector], record.Text);
        }

        return Task.CompletedTask;
    }

    public Task<VectorRecord?> GetAsync(string collection, string id, CancellationToken ct = default)
        => Task.FromResult(_records.TryGetValue(ValdKey(collection, id), out var record)
            ? new VectorRecord(record.Id, [.. record.Vector], record.Text)
            : null);

    public Task DeleteAsync(string collection, string id, CancellationToken ct = default)
    {
        _records.Remove(ValdKey(collection, id));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<VectorSearchResult>> SearchAsync(string collection, float[] queryVector, int topK = 10, float minScore = 0f, CancellationToken ct = default)
    {
        var prefix = collection + "/";
        var results = _records
            .Where(pair => pair.Key.StartsWith(prefix, StringComparison.Ordinal))
            .Select(pair =>
            {
                var distance = EuclideanDistance(queryVector, pair.Value.Vector);
                var score = 1f - distance;
                return new VectorSearchResult(pair.Value.Id, score);
            })
            .Where(result => result.Score >= minScore)
            .OrderByDescending(result => result.Score)
            .Take(topK)
            .ToArray();

        return Task.FromResult<IReadOnlyList<VectorSearchResult>>(results);
    }

    private static float EuclideanDistance(float[] left, float[] right)
    {
        var limit = Math.Min(left.Length, right.Length);
        var sum = 0f;
        for (var index = 0; index < limit; index++)
        {
            var delta = left[index] - right[index];
            sum += delta * delta;
        }

        return sum;
    }

    private static string ValdKey(string collection, string id) => $"{collection}/{id}";
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
            if (request.Method == HttpMethod.Put || request.Method == HttpMethod.Post)
            {
                using var doc = await ReadJsonAsync(request, cancellationToken);
                var fields = doc.RootElement.GetProperty("fields");
                var text = fields.TryGetProperty("text_content", out var textEl) ? textEl.GetString() : null;
                var embedding = fields.GetProperty("embedding");
                var vector = embedding.ValueKind == JsonValueKind.Object && embedding.TryGetProperty("values", out var values)
                    ? ReadVector(values)
                    : ReadVector(embedding);
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

public sealed class WeaviateContainerFixture : IAsyncLifetime
{
    private IContainer? _container;

    public string Endpoint { get; private set; } = "http://localhost:8080/";

    public async Task InitializeAsync()
    {
        _container = new ContainerBuilder("cr.weaviate.io/semitechnologies/weaviate:1.30.7")
            .WithEnvironment("AUTHENTICATION_ANONYMOUS_ACCESS_ENABLED", "true")
            .WithEnvironment("PERSISTENCE_DATA_PATH", "/var/lib/weaviate")
            .WithEnvironment("DEFAULT_VECTORIZER_MODULE", "none")
            .WithEnvironment("ENABLE_MODULES", "")
            .WithEnvironment("QUERY_DEFAULTS_LIMIT", "25")
            .WithEnvironment("CLUSTER_HOSTNAME", "node1")
            .WithPortBinding(8080, true)
            .Build();

        await _container.StartAsync();
        var port = _container.GetMappedPublicPort(8080);
        Endpoint = $"http://{_container.Hostname}:{port}/";
        await ContainerReadiness.WaitForTcpPortAsync(_container.Hostname, port, attempts: 120);
        await ContainerReadiness.WaitForWeaviateAsync(Endpoint, attempts: 120);
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }
}

public sealed class MilvusContainerFixture : IAsyncLifetime
{
    private IContainer? _container;

    public string Endpoint { get; private set; } = "http://localhost:19530/";

    public async Task InitializeAsync()
    {
        _container = new ContainerBuilder("milvusdb/milvus:v2.5.10")
            .WithCommand("milvus", "run", "standalone")
            .WithEnvironment("ETCD_USE_EMBED", "true")
            .WithEnvironment("COMMON_STORAGETYPE", "local")
            .WithPortBinding(19530, true)
            .Build();

        await _container.StartAsync();
        var port = _container.GetMappedPublicPort(19530);
        Endpoint = $"http://{_container.Hostname}:{port}/";
        await ContainerReadiness.WaitForTcpPortAsync(_container.Hostname, port, attempts: 180);
        await ContainerReadiness.WaitForMilvusAsync(Endpoint, attempts: 180);
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }
}

public sealed class ChromaContainerFixture : IAsyncLifetime
{
    private IContainer? _container;

    public string Endpoint { get; private set; } = "http://localhost:8000/";

    public async Task InitializeAsync()
    {
        _container = new ContainerBuilder("ghcr.io/chroma-core/chroma:latest")
            .WithPortBinding(8000, true)
            .Build();

        await _container.StartAsync();
        var port = _container.GetMappedPublicPort(8000);
        Endpoint = $"http://{_container.Hostname}:{port}/";
        await ContainerReadiness.WaitForTcpPortAsync(_container.Hostname, port, attempts: 120);
        await ContainerReadiness.WaitForChromaAsync(Endpoint, attempts: 120);
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

public sealed class ValdContainerFixture : IAsyncLifetime
{
    private IContainer? _container;

    public string Endpoint { get; private set; } = "http://localhost:8081";

    public async Task InitializeAsync()
    {
        _container = new ContainerBuilder("vdaas/vald-agent-ngt:latest")
            .WithPortBinding(8081, true)
            .Build();

        await _container.StartAsync();
        var port = _container.GetMappedPublicPort(8081);
        Endpoint = $"http://{_container.Hostname}:{port}";
        await ContainerReadiness.WaitForTcpPortAsync(_container.Hostname, port, attempts: 120);
        await ContainerReadiness.WaitForValdAsync(Endpoint, attempts: 120);
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }
}

public sealed class VespaContainerFixture : IAsyncLifetime
{
    private const string VespaConfigAlias = "vespa-config";
    private const string VespaServicesAlias = "vespa-services";
    private const string VespaSchema = "vespatest";

    private readonly string _suffix = Guid.NewGuid().ToString("N");
    private string? _networkName;
    private string? _configContainerName;
    private string? _servicesContainerName;
    private string? _appRoot;
    private string[] _dockerTargetArgs = [];
    private DockerCliResult? _lastDeployResult;
    private int _lastDeployAttempts;

    public string Endpoint { get; private set; } = "http://127.0.0.1:8080/";

    public async Task InitializeAsync()
    {
        _networkName = $"agxioma-vespa-net-{_suffix}";
        _configContainerName = $"agxioma-vespa-config-{_suffix}";
        _servicesContainerName = $"agxioma-vespa-services-{_suffix}";

        await EnsureDockerCliAvailableAsync();

        var networkCreate = await RunDockerCommandAsync(TimeSpan.FromSeconds(30), "network", "create", _networkName);
        EnsureDockerSuccess(networkCreate, "create Vespa network");

        var configRun = await RunDockerCommandAsync(
            TimeSpan.FromMinutes(2),
            "run", "-d",
            "--name", _configContainerName,
            "--hostname", VespaConfigAlias,
            "--network", _networkName,
            "--network-alias", VespaConfigAlias,
            "-e", $"VESPA_HOSTNAME={VespaConfigAlias}",
            "-e", $"VESPA_CONFIGSERVERS={VespaConfigAlias}",
            "-p", "127.0.0.1::19071",
            "vespaengine/vespa:latest",
            "configserver");
        EnsureDockerSuccess(configRun, "start Vespa configserver");

        var configPort = await GetMappedPortAsync(_configContainerName, 19071);
        await ContainerReadiness.WaitForTcpPortAsync("127.0.0.1", configPort, attempts: 120);

        var servicesRun = await RunDockerCommandAsync(
            TimeSpan.FromMinutes(2),
            "run", "-d",
            "--name", _servicesContainerName,
            "--hostname", VespaServicesAlias,
            "--network", _networkName,
            "--network-alias", VespaServicesAlias,
            "-e", $"VESPA_HOSTNAME={VespaServicesAlias}",
            "-e", $"VESPA_CONFIGSERVERS={VespaConfigAlias}",
            "-p", "127.0.0.1::8080",
            "-p", "127.0.0.1::19050",
            "vespaengine/vespa:latest",
            "services");
        EnsureDockerSuccess(servicesRun, "start Vespa services");

        var endpointPort = await GetMappedPortAsync(_servicesContainerName, 8080);
        var adminPort = await GetMappedPortAsync(_servicesContainerName, 19050);
        Endpoint = $"http://127.0.0.1:{endpointPort}/";
        await ContainerReadiness.WaitForTcpPortAsync("127.0.0.1", endpointPort, attempts: 120);
        await ContainerReadiness.WaitForTcpPortAsync("127.0.0.1", adminPort, attempts: 120);

        _appRoot = CreateVespaApplicationPackage(VespaServicesAlias, VespaConfigAlias);

        try
        {
            await DeployApplicationAsync();
            await ContainerReadiness.WaitForVespaAsync(Endpoint, attempts: 120);
        }
        catch (Exception ex) when (ex is TimeoutException or InvalidOperationException)
        {
            var diagnostics = await GetVespaDiagnosticsAsync();
            throw new InvalidOperationException(
                $"Vespa fixture initialization failed. {ex.Message}{Environment.NewLine}{diagnostics}",
                ex);
        }
    }

    public async Task DisposeAsync()
    {
        if (_servicesContainerName is not null)
            await TryRunDockerCommandAsync(TimeSpan.FromSeconds(10), "rm", "-f", _servicesContainerName);

        if (_configContainerName is not null)
            await TryRunDockerCommandAsync(TimeSpan.FromSeconds(10), "rm", "-f", _configContainerName);

        if (_networkName is not null)
            await TryRunDockerCommandAsync(TimeSpan.FromSeconds(10), "network", "rm", _networkName);

        if (_appRoot is not null)
        {
            try
            {
                Directory.Delete(_appRoot, recursive: true);
            }
            catch
            {
            }
        }
    }

    private async Task DeployApplicationAsync()
    {
        if (_appRoot is null || _configContainerName is null)
            throw new InvalidOperationException("Vespa fixture not initialized correctly.");

        var copySource = Path.Combine(_appRoot, ".");
        var copyResult = await RunDockerCommandAsync(
            TimeSpan.FromMinutes(2),
            "cp",
            copySource,
            $"{_configContainerName}:/tmp/agxioma-vespa-app");
        EnsureDockerSuccess(copyResult, "copy Vespa application package");

        DockerCliResult? lastResult = null;
        for (var attempt = 0; attempt < 60; attempt++)
        {
            lastResult = await RunDockerCommandAsync(
                TimeSpan.FromMinutes(2),
                "exec",
                _configContainerName,
                "bash",
                "-lc",
                "set -e; /opt/vespa/bin/vespa-deploy prepare /tmp/agxioma-vespa-app; /opt/vespa/bin/vespa-deploy activate");

            _lastDeployResult = lastResult;
            _lastDeployAttempts = attempt + 1;

            if (lastResult.Value.ExitCode == 0)
                return;

            if (attempt < 59)
                await Task.Delay(1000);
        }

        throw new InvalidOperationException(
            $"Vespa application deploy failed. ExitCode={lastResult?.ExitCode}, Stdout='{lastResult?.Stdout}', Stderr='{lastResult?.Stderr}'");
    }

    private async Task<string> GetVespaDiagnosticsAsync()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Vespa diagnostics:");
        builder.AppendLine($"  Network: {_networkName ?? "<null>"}");
        builder.AppendLine($"  Config container: {_configContainerName ?? "<null>"}");
        builder.AppendLine($"  Services container: {_servicesContainerName ?? "<null>"}");
        builder.AppendLine($"  App root: {_appRoot ?? "<null>"}");
        builder.AppendLine($"  Endpoint: {Endpoint}");
        builder.AppendLine($"  Last deploy attempts: {_lastDeployAttempts}");

        if (_lastDeployResult is { } lastDeployResult)
        {
            builder.AppendLine($"  Last deploy exit code: {lastDeployResult.ExitCode}");
            builder.AppendLine($"  Last deploy stdout: {Truncate(lastDeployResult.Stdout)}");
            builder.AppendLine($"  Last deploy stderr: {Truncate(lastDeployResult.Stderr)}");
        }

        if (_configContainerName is not null)
        {
            builder.AppendLine(await RunDiagnosticCommandAsync(
                _configContainerName,
                "config tail vespa.log",
                "tail -n 120 /opt/vespa/logs/vespa/vespa.log 2>&1 || true"));
        }

        if (_servicesContainerName is not null)
        {
            builder.AppendLine(await RunDiagnosticCommandAsync(
                _servicesContainerName,
                "services probe 8080 health",
                "curl --max-time 2 -i http://localhost:8080/state/v1/health 2>&1 || true"));
            builder.AppendLine(await RunDiagnosticCommandAsync(
                _servicesContainerName,
                "services probe document readiness",
                "curl --max-time 5 -i http://localhost:8080/document/v1/vespatest/vespatest/docid/readiness-probe 2>&1 || true"));
            builder.AppendLine(await RunDiagnosticCommandAsync(
                _servicesContainerName,
                "services probe vector search",
                "curl --max-time 5 -i \"http://localhost:8080/search/?yql=select%20*%20from%20vespatest%20where%20%7BtargetHits%3A1%7DnearestNeighbor(embedding%2Cquery_embedding)&ranking=vector&hits=1&input.query(query_embedding)=tensor%3Cfloat%3E(x%5B3%5D)%3A%5B0.1%2C0.2%2C0.3%5D\" 2>&1 || true"));
            builder.AppendLine(await RunDiagnosticCommandAsync(
                _servicesContainerName,
                "services probe 19050 health",
                "curl --max-time 2 -i http://localhost:19050/state/v1/health 2>&1 || true"));
            builder.AppendLine(await RunDiagnosticCommandAsync(
                _servicesContainerName,
                "services probe ApplicationStatus",
                "curl --max-time 2 -i http://localhost:8080/ApplicationStatus 2>&1 || true"));
            builder.AppendLine(await RunDiagnosticCommandAsync(
                _servicesContainerName,
                "services process list",
                "ps -ef 2>&1 || true"));
            builder.AppendLine(await RunDiagnosticCommandAsync(
                _servicesContainerName,
                "services tail vespa.log",
                "tail -n 120 /opt/vespa/logs/vespa/vespa.log 2>&1 || true"));

            try
            {
                var logs = await RunDockerCommandAsync(TimeSpan.FromSeconds(10), "logs", "--tail", "120", _servicesContainerName);
                builder.AppendLine($"[services docker logs] ExitCode={logs.ExitCode}{Environment.NewLine}STDOUT:{Environment.NewLine}{Truncate(logs.Stdout)}{Environment.NewLine}STDERR:{Environment.NewLine}{Truncate(logs.Stderr)}");
            }
            catch (Exception ex)
            {
                builder.AppendLine($"[services docker logs] diagnostic command failed: {ex.Message}");
            }
        }

        return builder.ToString();
    }

    private async Task<string> RunDiagnosticCommandAsync(string containerName, string label, string command)
    {
        try
        {
            var result = await RunDockerCommandAsync(
                TimeSpan.FromSeconds(30),
                "exec",
                containerName,
                "bash",
                "-lc",
                command);

            return $"[{label}] ExitCode={result.ExitCode}{Environment.NewLine}STDOUT:{Environment.NewLine}{Truncate(result.Stdout)}{Environment.NewLine}STDERR:{Environment.NewLine}{Truncate(result.Stderr)}";
        }
        catch (Exception ex)
        {
            return $"[{label}] diagnostic command failed: {ex.Message}";
        }
    }

    private async Task EnsureDockerCliAvailableAsync()
    {
        _dockerTargetArgs = await ResolveDockerTargetArgsAsync();
    }

    private async Task<int> GetMappedPortAsync(string containerName, int containerPort)
    {
        var portResult = await RunDockerCommandAsync(TimeSpan.FromSeconds(15), "port", containerName, $"{containerPort}/tcp");
        EnsureDockerSuccess(portResult, $"query mapped port for {containerName}:{containerPort}");

        var line = portResult.Stdout
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault();
        if (line is null)
            throw new InvalidOperationException($"Docker did not return a mapped port for {containerName}:{containerPort}.");

        var separatorIndex = line.LastIndexOf(':');
        if (separatorIndex < 0 || !int.TryParse(line[(separatorIndex + 1)..], out var mappedPort))
            throw new InvalidOperationException($"Unable to parse mapped port from '{line}'.");

        return mappedPort;
    }

    private static void EnsureDockerSuccess(DockerCliResult result, string operation)
    {
        if (result.ExitCode == 0)
            return;

        throw new InvalidOperationException(
            $"Docker CLI failed to {operation}. ExitCode={result.ExitCode}, Stdout='{Truncate(result.Stdout)}', Stderr='{Truncate(result.Stderr)}'");
    }

    private async Task<DockerCliResult> RunDockerCommandAsync(TimeSpan timeout, params string[] arguments)
    {
        return await RunDockerCommandCoreAsync(timeout, _dockerTargetArgs, arguments);
    }

    private async Task TryRunDockerCommandAsync(TimeSpan timeout, params string[] arguments)
    {
        try
        {
            await RunDockerCommandAsync(timeout, arguments);
        }
        catch
        {
        }
    }

    private static async Task<DockerCliResult> RunDockerCommandRawAsync(TimeSpan timeout, params string[] arguments)
    {
        return await RunDockerCommandCoreAsync(timeout, [], arguments);
    }

    private static async Task<string[]> ResolveDockerTargetArgsAsync()
    {
        var candidateTargets = new[]
        {
            new[] { "-H", "npipe:////./pipe/dockerDesktopLinuxEngine" },
            new[] { "-H", "npipe:////./pipe/dockerDesktopEngine" },
            new[] { "-H", "npipe:////./pipe/docker_engine" },
            new[] { "--context", "desktop-linux" },
            new[] { "--context", "default" },
            Array.Empty<string>(),
        };

        DockerCliResult? lastFailure = null;
        foreach (var candidateTarget in candidateTargets)
        {
            var probe = await RunDockerCommandCoreAsync(
                TimeSpan.FromSeconds(15),
                candidateTarget,
                ["version", "--format", "{{.Server.Version}}"]);

            if (probe.ExitCode == 0 && !string.IsNullOrWhiteSpace(probe.Stdout))
                return candidateTarget;

            lastFailure = probe;
        }

        EnsureDockerSuccess(lastFailure ?? new DockerCliResult(1, string.Empty, "Docker probe did not run."), "query Docker server version");
        return [];
    }

    private static async Task<DockerCliResult> RunDockerCommandCoreAsync(TimeSpan timeout, IReadOnlyList<string> dockerTargetArgs, IReadOnlyList<string> arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("docker")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        process.StartInfo.Environment.Remove("DOCKER_HOST");
        process.StartInfo.Environment.Remove("DOCKER_CONTEXT");

        foreach (var argument in dockerTargetArgs)
            process.StartInfo.ArgumentList.Add(argument);

        foreach (var argument in arguments)
            process.StartInfo.ArgumentList.Add(argument);

        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        using var timeoutCts = new CancellationTokenSource(timeout);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
            }

            var timedOutStdout = await stdoutTask;
            var timedOutStderr = await stderrTask;
            throw new TimeoutException(
                $"Docker command timed out after {timeout.TotalSeconds:N0}s. Args: {string.Join(' ', dockerTargetArgs.Concat(arguments))}. Stdout='{Truncate(timedOutStdout)}', Stderr='{Truncate(timedOutStderr)}'");
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        return new DockerCliResult(process.ExitCode, stdout, stderr);
    }

    private static string Truncate(string? value, int maxLength = 4000)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "<empty>";

        var normalized = value.Replace("\r", string.Empty).Trim();
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength] + "...<truncated>";
    }

    private readonly record struct DockerCliResult(int ExitCode, string Stdout, string Stderr);

        private static string CreateVespaApplicationPackage(string servicesHostName, string configHostName)
        {
                var root = Path.Combine(Path.GetTempPath(), $"agxioma_vespa_{Guid.NewGuid():N}");
                Directory.CreateDirectory(root);
                Directory.CreateDirectory(Path.Combine(root, "schemas"));

                File.WriteAllText(Path.Combine(root, "services.xml"),
                        """
                        <services version="1.0">
                            <admin version="2.0">
                                <adminserver hostalias="config1" />
                                <cluster-controllers>
                                    <cluster-controller hostalias="node1" />
                                </cluster-controllers>
                            </admin>
                            <container id="default" version="1.0">
                                <search />
                                <document-api />
                                <nodes>
                                    <node hostalias="node1" />
                                </nodes>
                            </container>
                            <content id="content" version="1.0">
                                <redundancy>1</redundancy>
                                <documents>
                                    <document type="vespatest" mode="index" />
                                </documents>
                                <nodes>
                                    <node distribution-key="0" hostalias="node1" />
                                </nodes>
                            </content>
                        </services>
                        """);

                File.WriteAllText(Path.Combine(root, "hosts.xml"),
                        $"""
                        <hosts>
                            <host name="{configHostName}">
                                <alias>config1</alias>
                            </host>
                            <host name="{servicesHostName}">
                                <alias>node1</alias>
                            </host>
                        </hosts>
                        """);

                File.WriteAllText(Path.Combine(root, "schemas", $"{VespaSchema}.sd"),
                        """
                        schema vespatest {
                            document vespatest {
                                field id type string {
                                    indexing: attribute | summary
                                }
                                field text_content type string {
                                    indexing: index | summary
                                }
                                field embedding type tensor<float>(x[3]) {
                                    indexing: attribute | index
                                    attribute {
                                        distance-metric: euclidean
                                    }
                                }
                            }

                            rank-profile vector inherits default {
                                inputs {
                                    query(query_embedding) tensor<float>(x[3])
                                }
                                first-phase {
                                    expression: closeness(field, embedding)
                                }
                            }
                        }
                        """);

                return root;
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

    public static async Task WaitForValdAsync(string endpoint, int attempts = 40)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                var store = new ValdStore(endpoint);
                _ = await store.GetAsync("readiness", Guid.NewGuid().ToString("N"));
                return;
            }
            catch when (attempt < attempts - 1)
            {
            }

            if (attempt < attempts - 1)
                await Task.Delay(500);
        }

        throw new TimeoutException($"Vald endpoint {endpoint} did not become ready in time.");
    }

    public static async Task WaitForVespaAsync(string endpoint, int attempts = 40)
    {
        using var client = new HttpClient
        {
            BaseAddress = new Uri(endpoint),
            Timeout = TimeSpan.FromSeconds(5)
        };
        string? lastPayload = null;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                using var response = await client.GetAsync("state/v1/health");
                lastPayload = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    if (attempt < attempts - 1)
                        await Task.Delay(1000);

                    continue;
                }

                using var documentResponse = await client.GetAsync("document/v1/vespatest/vespatest/docid/readiness-probe");
                lastPayload = $"document probe status={(int)documentResponse.StatusCode} body='{await documentResponse.Content.ReadAsStringAsync()}'";
                if (documentResponse.StatusCode != HttpStatusCode.NotFound)
                {
                    if (attempt < attempts - 1)
                        await Task.Delay(1000);

                    continue;
                }

                using var searchResponse = await client.GetAsync(
                    "search/?yql=select%20*%20from%20vespatest%20where%20%7BtargetHits%3A1%7DnearestNeighbor(embedding%2Cquery_embedding)&ranking=vector&hits=1&input.query(query_embedding)=tensor%3Cfloat%3E(x%5B3%5D)%3A%5B0.1%2C0.2%2C0.3%5D");
                lastPayload = $"search probe status={(int)searchResponse.StatusCode} body='{await searchResponse.Content.ReadAsStringAsync()}'";
                if (searchResponse.IsSuccessStatusCode)
                    return;
            }
            catch (TaskCanceledException ex)
            {
                lastPayload = $"probe timeout: {ex.Message}";
            }
            catch (Exception ex)
            {
                lastPayload = $"probe exception ({ex.GetType().Name}): {ex.Message}";
            }

            if (attempt < attempts - 1)
                await Task.Delay(1000);
        }

        throw new TimeoutException(
            $"Vespa endpoint {endpoint} did not become ready in time. Last payload='{lastPayload ?? "<empty>"}'");
    }

    public static async Task WaitForWeaviateAsync(string endpoint, int attempts = 40)
    {
        using var client = new HttpClient { BaseAddress = new Uri(endpoint) };
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                using var response = await client.GetAsync("/v1/meta");
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch when (attempt < attempts - 1)
            {
            }

            if (attempt < attempts - 1)
                await Task.Delay(500);
        }

        throw new TimeoutException($"Weaviate endpoint {endpoint} did not become ready in time.");
    }

    public static async Task WaitForMilvusAsync(string endpoint, int attempts = 40)
    {
        using var client = new HttpClient { BaseAddress = new Uri(endpoint) };
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                using var response = await client.PostAsJsonAsync("/v2/vectordb/collections/list", new { });
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch when (attempt < attempts - 1)
            {
            }

            if (attempt < attempts - 1)
                await Task.Delay(1000);
        }

        throw new TimeoutException($"Milvus endpoint {endpoint} did not become ready in time.");
    }

    public static async Task WaitForChromaAsync(string endpoint, int attempts = 40)
    {
        using var client = new HttpClient { BaseAddress = new Uri(endpoint) };
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                using var response = await client.GetAsync("api/v2/heartbeat");
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch when (attempt < attempts - 1)
            {
            }

            if (attempt < attempts - 1)
                await Task.Delay(1000);
        }

        throw new TimeoutException($"Chroma endpoint {endpoint} did not become ready in time.");
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

internal static class WeaviateLiveTestCases
{
    public static async Task EnsureCollectionUpsertGetRoundTripAsync(Func<IVectorStore> createStore)
    {
        var collection = $"F5WeaviateRoundtrip{Guid.NewGuid():N}";
        var recordId = Guid.NewGuid().ToString();
        var store = createStore();
        try
        {
            await store.EnsureCollectionAsync(collection, 3);
            await store.UpsertAsync(collection, new VectorRecord(recordId, [1f, 0f, 0f], "hello"));

            await LiveTestRetry.EventuallyAsync(async () => await store.GetAsync(collection, recordId) is not null, attempts: 60, delayMs: 500);
            Assert.NotNull(await store.GetAsync(collection, recordId));
        }
        finally
        {
            await VectorStoreLiveCleanup.TryDeleteCollectionAsync(store, collection);
        }
    }

    public static async Task SearchEmptyCollectionReturnsEmptyAsync(Func<IVectorStore> createStore)
    {
        var collection = $"F5WeaviateEmpty{Guid.NewGuid():N}";
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
        var collection = $"F5WeaviateBatch{Guid.NewGuid():N}";
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

            await LiveTestRetry.EventuallyAsync(async () =>
                await store.GetAsync(collection, recordA) is not null
                && await store.GetAsync(collection, recordB) is not null,
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
        var collection = $"F5WeaviateDelete{Guid.NewGuid():N}";
        var recordId = Guid.NewGuid().ToString();
        var store = createStore();
        try
        {
            await store.EnsureCollectionAsync(collection, 2);
            await store.UpsertAsync(collection, new VectorRecord(recordId, [1f, 0f], "delete-me"));
            await LiveTestRetry.EventuallyAsync(async () => await store.GetAsync(collection, recordId) is not null, attempts: 60, delayMs: 500);

            await store.DeleteAsync(collection, recordId);
            await LiveTestRetry.EventuallyAsync(async () => await store.GetAsync(collection, recordId) is null, attempts: 60, delayMs: 500);
        }
        finally
        {
            await VectorStoreLiveCleanup.TryDeleteCollectionAsync(store, collection);
        }
    }

    public static async Task ListCollectionsReturnsCollectionsAsync(Func<IVectorStore> createStore)
    {
        var collectionA = $"F5WeaviateListA{Guid.NewGuid():N}";
        var collectionB = $"F5WeaviateListB{Guid.NewGuid():N}";
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

internal static class MilvusLiveTestCases
{
    public static async Task EnsureCollectionUpsertGetRoundTripAsync(Func<IVectorStore> createStore)
    {
        var collection = $"f5_milvus_roundtrip_{Guid.NewGuid():N}";
        var recordId = Guid.NewGuid().ToString();
        var store = createStore();
        try
        {
            await store.EnsureCollectionAsync(collection, 3);
            await store.UpsertAsync(collection, new VectorRecord(recordId, [1f, 0f, 0f], "hello"));

            await LiveTestRetry.EventuallyAsync(async () =>
            {
                var got = await store.GetAsync(collection, recordId);
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
        var collection = $"f5_milvus_empty_{Guid.NewGuid():N}";
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
        var collection = $"f5_milvus_batch_{Guid.NewGuid():N}";
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

            await LiveTestRetry.EventuallyAsync(async () =>
                await store.GetAsync(collection, recordA) is not null
                && await store.GetAsync(collection, recordB) is not null,
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
        var collection = $"f5_milvus_delete_{Guid.NewGuid():N}";
        var recordId = Guid.NewGuid().ToString();
        var store = createStore();
        try
        {
            await store.EnsureCollectionAsync(collection, 2);
            await store.UpsertAsync(collection, new VectorRecord(recordId, [1f, 0f], "delete-me"));
            await LiveTestRetry.EventuallyAsync(async () => await store.GetAsync(collection, recordId) is not null, attempts: 60, delayMs: 500);

            await store.DeleteAsync(collection, recordId);
            await LiveTestRetry.EventuallyAsync(async () => await store.GetAsync(collection, recordId) is null, attempts: 60, delayMs: 500);
        }
        finally
        {
            await VectorStoreLiveCleanup.TryDeleteCollectionAsync(store, collection);
        }
    }

    public static async Task ListCollectionsReturnsCollectionsAsync(Func<IVectorStore> createStore)
    {
        var collectionA = $"f5_milvus_list_a_{Guid.NewGuid():N}";
        var collectionB = $"f5_milvus_list_b_{Guid.NewGuid():N}";
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

internal static class ChromaLiveTestCases
{
    public static async Task EnsureCollectionUpsertGetRoundTripAsync(Func<IVectorStore> createStore)
    {
        var collection = $"f5_chroma_roundtrip_{Guid.NewGuid():N}";
        var recordId = Guid.NewGuid().ToString();
        var store = createStore();
        try
        {
            await store.EnsureCollectionAsync(collection, 3);
            await store.UpsertAsync(collection, new VectorRecord(recordId, [1f, 0f, 0f], "hello"));

            await LiveTestRetry.EventuallyAsync(async () =>
            {
                var got = await store.GetAsync(collection, recordId);
                return got is not null;
            }, attempts: 60, delayMs: 500);

            Assert.NotNull(await store.GetAsync(collection, recordId));
        }
        finally
        {
            await VectorStoreLiveCleanup.TryDeleteCollectionAsync(store, collection);
        }
    }

    public static async Task SearchEmptyCollectionReturnsEmptyAsync(Func<IVectorStore> createStore)
    {
        var collection = $"f5_chroma_empty_{Guid.NewGuid():N}";
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
        var collection = $"f5_chroma_batch_{Guid.NewGuid():N}";
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

            await LiveTestRetry.EventuallyAsync(async () =>
                await store.GetAsync(collection, recordA) is not null
                && await store.GetAsync(collection, recordB) is not null,
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
        var collection = $"f5_chroma_delete_{Guid.NewGuid():N}";
        var recordId = Guid.NewGuid().ToString();
        var store = createStore();
        try
        {
            await store.EnsureCollectionAsync(collection, 2);
            await store.UpsertAsync(collection, new VectorRecord(recordId, [1f, 0f], "delete-me"));
            await LiveTestRetry.EventuallyAsync(async () => await store.GetAsync(collection, recordId) is not null, attempts: 60, delayMs: 500);

            await store.DeleteAsync(collection, recordId);
            await LiveTestRetry.EventuallyAsync(async () => await store.GetAsync(collection, recordId) is null, attempts: 60, delayMs: 500);
        }
        finally
        {
            await VectorStoreLiveCleanup.TryDeleteCollectionAsync(store, collection);
        }
    }

    public static async Task ListCollectionsReturnsCollectionsAsync(Func<IVectorStore> createStore)
    {
        var collectionA = $"f5_chroma_list_a_{Guid.NewGuid():N}";
        var collectionB = $"f5_chroma_list_b_{Guid.NewGuid():N}";
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

internal static class ValdLiveTestCases
{
    private const int ValdDimensions = 100;

    public static async Task EnsureCollectionUpsertGetRoundTripAsync(Func<IVectorStore> createStore)
    {
        var collection = $"f5_vald_roundtrip_{Guid.NewGuid():N}";
        var recordId = Guid.NewGuid().ToString();
        var store = createStore();
        try
        {
            await store.EnsureCollectionAsync(collection, ValdDimensions);
            await store.UpsertAsync(collection, new VectorRecord(recordId, BasisVector(0), "hello"));

            await LiveTestRetry.EventuallyAsync(async () =>
            {
                var got = await store.GetAsync(collection, recordId);
                return got is not null && got.Vector.Length == ValdDimensions && got.Vector[0] == 1f;
            }, attempts: 60, delayMs: 500);
        }
        finally
        {
            await TryDeleteAsync(store, collection, recordId);
        }
    }

    public static async Task SearchEmptyCollectionReturnsEmptyAsync(Func<IVectorStore> createStore)
    {
        var collection = $"f5_vald_empty_{Guid.NewGuid():N}";
        var store = createStore();
        await store.EnsureCollectionAsync(collection, ValdDimensions);
        Assert.Empty(await store.SearchAsync(collection, BasisVector(0), topK: 5));
    }

    public static async Task UpsertBatchMultipleRecordsAsync(Func<IVectorStore> createStore)
    {
        var collection = $"f5_vald_batch_{Guid.NewGuid():N}";
        var recordA = Guid.NewGuid().ToString();
        var recordB = Guid.NewGuid().ToString();
        var store = createStore();
        try
        {
            await store.EnsureCollectionAsync(collection, ValdDimensions);
            await store.UpsertBatchAsync(collection, [
                new VectorRecord(recordA, BasisVector(0), "alpha"),
                new VectorRecord(recordB, BasisVector(1), "beta")
            ]);

            await LiveTestRetry.EventuallyAsync(async () =>
                await store.GetAsync(collection, recordA) is not null
                && await store.GetAsync(collection, recordB) is not null,
                attempts: 60,
                delayMs: 500);
        }
        finally
        {
            await TryDeleteAsync(store, collection, recordA, recordB);
        }
    }

    public static async Task DeleteRemovesRecordAsync(Func<IVectorStore> createStore)
    {
        var collection = $"f5_vald_delete_{Guid.NewGuid():N}";
        var recordId = Guid.NewGuid().ToString();
        var store = createStore();
        await store.EnsureCollectionAsync(collection, ValdDimensions);
        await store.UpsertAsync(collection, new VectorRecord(recordId, BasisVector(2), "delete-me"));
        await LiveTestRetry.EventuallyAsync(async () => await store.GetAsync(collection, recordId) is not null, attempts: 60, delayMs: 500);

        await store.DeleteAsync(collection, recordId);
        await LiveTestRetry.EventuallyAsync(async () => await store.GetAsync(collection, recordId) is null, attempts: 60, delayMs: 500);
    }

    private static float[] BasisVector(int hotIndex)
    {
        var vector = new float[ValdDimensions];
        vector[hotIndex] = 1f;
        return vector;
    }

    private static async Task TryDeleteAsync(IVectorStore store, string collection, params string[] ids)
    {
        foreach (var id in ids)
        {
            try
            {
                await store.DeleteAsync(collection, id);
            }
            catch
            {
            }
        }
    }
}

internal static class VespaLiveTestCases
{
    private const string Collection = "vespatest";

    public static async Task EnsureCollectionUpsertGetRoundTripAsync(Func<IVectorStore> createStore)
    {
        var recordId = Guid.NewGuid().ToString();
        var store = createStore();

        await store.UpsertAsync(Collection, new VectorRecord(recordId, [1f, 0f, 0f], "hello"));
        await LiveTestRetry.EventuallyAsync(async () =>
        {
            var got = await store.GetAsync(Collection, recordId);
            return got is not null && got.Text == "hello";
        }, attempts: 60, delayMs: 500);
        await store.DeleteAsync(Collection, recordId);
    }

    public static async Task SearchReturnsNormalizedIdsAsync(Func<IVectorStore> createStore)
    {
        var recordId = Guid.NewGuid().ToString();
        var store = createStore();
        try
        {
            await store.UpsertAsync(Collection, new VectorRecord(recordId, [1f, 0f, 0f], "search-me"));
            await LiveTestRetry.EventuallyAsync(async () => await store.GetAsync(Collection, recordId) is not null, attempts: 60, delayMs: 500);

            await LiveTestRetry.EventuallyAsync(async () =>
            {
                var results = await store.SearchAsync(Collection, [1f, 0f, 0f], topK: 3);
                return results.Any(r => r.Id == recordId);
            }, attempts: 60, delayMs: 500);
        }
        finally
        {
            await store.DeleteAsync(Collection, recordId);
        }
    }

    public static async Task UpsertBatchMultipleRecordsAsync(Func<IVectorStore> createStore)
    {
        var recordA = Guid.NewGuid().ToString();
        var recordB = Guid.NewGuid().ToString();
        var store = createStore();
        try
        {
            await store.UpsertBatchAsync(Collection, [
                new VectorRecord(recordA, [1f, 0f, 0f], "alpha"),
                new VectorRecord(recordB, [0f, 1f, 0f], "beta")
            ]);

            await LiveTestRetry.EventuallyAsync(async () =>
                await store.GetAsync(Collection, recordA) is not null
                && await store.GetAsync(Collection, recordB) is not null,
                attempts: 60,
                delayMs: 500);
        }
        finally
        {
            await store.DeleteAsync(Collection, recordA);
            await store.DeleteAsync(Collection, recordB);
        }
    }

    public static async Task DeleteRemovesRecordAsync(Func<IVectorStore> createStore)
    {
        var recordId = Guid.NewGuid().ToString();
        var store = createStore();
        await store.UpsertAsync(Collection, new VectorRecord(recordId, [0f, 0f, 1f], "delete-me"));
        await LiveTestRetry.EventuallyAsync(async () => await store.GetAsync(Collection, recordId) is not null, attempts: 60, delayMs: 500);

        await store.DeleteAsync(Collection, recordId);
        await LiveTestRetry.EventuallyAsync(async () => await store.GetAsync(Collection, recordId) is null, attempts: 60, delayMs: 500);
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

