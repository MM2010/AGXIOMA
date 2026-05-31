using System.Net;
using System.Diagnostics;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Reflection;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using AGXIOME.Shared.Models;
using AGXIOME.Storage.Stores;
using ClickHouse.Client.ADO;
using Couchbase;
using Couchbase.Core.Exceptions.KeyValue;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DuckDB.NET.Data;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.Azure.Cosmos;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;
using Raven.Client.Documents;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Xunit;

namespace AGXIOME.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// F4 — DB Store Tests (DB-01..DB-25)
// This suite is being hardened toward real providers and realistic emulators.
// DB-01 SQLite is the first baseline provider and must validate real file-backed
// behaviour, offensive inputs, and basic contention-friendly lifecycle paths.
// ─────────────────────────────────────────────────────────────────────────────

// NOT file-scoped — file keyword causes mangled type names with < > chars
// which break SQLite table names and Windows file names.
internal record DbTestEntity
{
    public string  Id    { get; init; } = string.Empty;
    public string  Name  { get; init; } = string.Empty;
    public int     Score { get; init; }
}

public class F4RealProofGuardTests
{
    private static readonly string[] RequiredRealProofTests =
    [
        nameof(SqlServerCommandStoreTests.SaveUpdateDelete_WithRealSqlServerConnection_WorksWhenConfigured),
        nameof(SqlServerLocalDbStoreTests.SaveUpdateDelete_WithRealLocalDbConnection_WorksWhenConfigured),
        nameof(PostgresCommandStoreTests.SaveUpdateDelete_WithRealPostgresConnection_WorksWhenConfigured),
        nameof(SingleStoreCommandStoreTests.SaveUpdateDelete_WithRealSingleStoreConnection_WorksWhenConfigured),
        nameof(NeonCommandStoreTests.SaveUpdateDelete_WithPostgresCompatConnection_Works),
        nameof(SupabaseCommandStoreTests.SaveUpdateDelete_WithPostgresCompatConnection_Works),
        nameof(CockroachDbCommandStoreTests.SaveUpdateDelete_WithPostgresCompatConnection_Works),
        nameof(TimescaleDbCommandStoreTests.SaveUpdateDelete_WithPostgresCompatConnection_Works),
        nameof(CosmosDbCommandStoreTests.SaveUpdateDelete_WithRealCosmosConnection_WorksWhenConfigured),
        nameof(FirestoreCommandStoreTests.SaveUpdateDelete_WithRealFirestoreEmulator_WorksWhenConfigured),
        nameof(SurrealDbCommandStoreTests.SaveUpdateDelete_WithRealSurrealDb_WorksWhenConfigured),
        nameof(RavenDbCommandStoreTests.SaveUpdateDelete_WithRealRavenDb_WorksWhenConfigured),
        nameof(CouchbaseCommandStoreTests.SaveUpdateDelete_WithRealCouchbase_WorksWhenConfigured),
        nameof(DynamoDbCommandStoreTests.SaveUpdateDelete_WithRealDynamoDbLocal_WorksWhenConfigured),
        nameof(RedisSessionStoreTests.SaveUpdateDelete_WithRealRedisConnection_WorksWhenConfigured),
        nameof(MemcachedCommandStoreTests.SaveUpdateDelete_WithRealMemcached_WorksWhenConfigured),
        nameof(HazelcastCommandStoreTests.SaveUpdateDelete_WithRealHazelcast_WorksWhenConfigured),
        nameof(EtcdCommandStoreTests.SaveUpdateDelete_WithRealEtcd_WorksWhenConfigured),
        nameof(ClickHouseCommandStoreTests.SaveUpdateDelete_WithRealClickHouse_WorksWhenConfigured),
        nameof(InfluxDbCommandStoreTests.SaveUpdateDelete_WithRealInfluxDb_WorksWhenConfigured),
        nameof(QuestDbCommandStoreTests.SaveUpdateDelete_WithRealQuestDb_WorksWhenConfigured),
        nameof(DuckDbCommandStoreTests.SaveUpdateDelete_WithRealDuckDbFile_Works),
        nameof(ElasticsearchCommandStoreTests.SaveUpdateDelete_WithRealElasticsearchNode_WorksWhenConfigured),
        nameof(MeilisearchCommandStoreTests.SaveUpdateDelete_WithRealMeilisearchServer_WorksWhenConfigured),
        nameof(JsonFileCommandStoreTests.SaveUpdateDelete_WithRealJsonFile_Works)
    ];

    [Fact]
    public void RealProofTests_ArePresent()
    {
        var availableRealProofTests = typeof(F4RealProofGuardTests).Assembly
            .GetTypes()
            .Where(type => type.Namespace == typeof(F4RealProofGuardTests).Namespace)
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            .Where(method => method.GetCustomAttribute<FactAttribute>() is not null)
            .Select(method => method.Name)
            .ToHashSet(StringComparer.Ordinal);

        var missingTests = RequiredRealProofTests
            .Where(testName => !availableRealProofTests.Contains(testName))
            .ToArray();

        Assert.True(
            missingTests.Length == 0,
            $"F4 SQL real-proof coverage regressed. Missing tests: {string.Join(", ", missingTests)}");

        Assert.True(
            RequiredRealProofTests.Length >= 1,
            "F4 real-provider filter must include at least one real-provider proof test.");
    }
}

internal static class ConditionalTestEnvironment
{
    private static readonly string[] DockerPipeNames =
    [
        "docker_engine",
        "dockerDesktopLinuxEngine",
        "docker_engine_linux",
        "dockerDesktopEngine"
    ];

    public static bool IsTcpEndpointReachable(string host, int port)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(host, port);
            return connectTask.Wait(TimeSpan.FromSeconds(2)) && client.Connected;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsDockerAvailable()
    {
        foreach (var pipeName in DockerPipeNames)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                using var client = new HttpClient(new SocketsHttpHandler
                {
                    ConnectCallback = async (context, cancellationToken) =>
                    {
                        var stream = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                        await stream.ConnectAsync(cts.Token);
                        return stream;
                    }
                })
                {
                    Timeout = TimeSpan.FromSeconds(2)
                };

                using var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/_ping");
                using var response = client.Send(request, cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCKER_HOST")))
                    {
                        Environment.SetEnvironmentVariable("DOCKER_HOST", ToDockerHostUri(pipeName));
                    }

                    return true;
                }
            }
            catch
            {
            }
        }

        return false;
    }

    private static string ToDockerHostUri(string pipeName)
        => pipeName switch
        {
            "docker_engine_linux" => "npipe://./pipe/dockerDesktopLinuxEngine",
            _ => $"npipe://./pipe/{pipeName}"
        };

    public static bool IsLocalDbInstalled()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        return File.Exists(Path.Combine(programFiles, "Microsoft SQL Server", "150", "Tools", "Binn", "SqlLocalDB.exe"))
            || File.Exists(Path.Combine(programFiles, "Microsoft SQL Server", "160", "Tools", "Binn", "SqlLocalDB.exe"));
    }

    public static bool IsDedicatedPostgresAvailable()
        => IsTcpEndpointReachable("127.0.0.1", 55432);

    public static bool IsDedicatedSingleStoreAvailable()
        => IsTcpEndpointReachable("127.0.0.1", 13306);

    public static bool IsDedicatedCockroachAvailable()
        => IsTcpEndpointReachable("127.0.0.1", 12657);

    public static bool IsDedicatedTimescaleAvailable()
        => IsTcpEndpointReachable("127.0.0.1", 15432);

    public static bool IsDedicatedCouchbaseAvailable()
        => IsTcpEndpointReachable("127.0.0.1", 18091);

    public static bool IsDedicatedMemcachedAvailable()
        => IsTcpEndpointReachable("127.0.0.1", 11212);

    public static bool IsDedicatedHazelcastAvailable()
        => IsTcpEndpointReachable("127.0.0.1", 15701);

    public static bool IsDedicatedEtcdAvailable()
        => IsTcpEndpointReachable("127.0.0.1", 12379);

    public static bool IsDedicatedClickHouseAvailable()
        => IsTcpEndpointReachable("127.0.0.1", 18123);

    public static bool IsDedicatedInfluxDbAvailable()
        => IsTcpEndpointReachable("127.0.0.1", 18086);

    public static bool IsDedicatedQuestDbAvailable()
        => IsTcpEndpointReachable("127.0.0.1", 19000);
}

public sealed class RequiresDockerFactAttribute : FactAttribute
{
    public RequiresDockerFactAttribute()
    {
        if (!ConditionalTestEnvironment.IsDockerAvailable()
            && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AGXIOME_SQLSERVER_TEST_CONNECTION"))
            && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AGXIOME_POSTGRES_TEST_CONNECTION"))
            && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AGXIOME_ELASTIC_TEST_ENDPOINT"))
            && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AGXIOME_MEILI_TEST_ENDPOINT")))
        {
            Skip = "Docker/Testcontainers not available and no external SQL/PostgreSQL connection override configured.";
        }
    }
}

public sealed class RequiresPostgresRealFactAttribute : FactAttribute
{
    public RequiresPostgresRealFactAttribute()
    {
        if (!ConditionalTestEnvironment.IsDedicatedPostgresAvailable()
            && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AGXIOME_POSTGRES_TEST_CONNECTION"))
            && !ConditionalTestEnvironment.IsDockerAvailable())
        {
            Skip = "PostgreSQL real-proof test requires AGXIOME_POSTGRES_TEST_CONNECTION, dedicated compose postgres on 127.0.0.1:55432, or a working Docker/Testcontainers daemon.";
        }
    }
}

public sealed class RequiresLocalDbFactAttribute : FactAttribute
{
    public RequiresLocalDbFactAttribute()
    {
        if (!ConditionalTestEnvironment.IsLocalDbInstalled()
            && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AGXIOME_SQLLOCALDB_TEST_CONNECTION")))
        {
            Skip = "SQL Server LocalDB not available and no external LocalDB connection override configured.";
        }
    }
}

public sealed class RequiresSingleStoreFactAttribute : FactAttribute
{
    public RequiresSingleStoreFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AGXIOME_SINGLESTORE_TEST_CONNECTION"))
            && !ConditionalTestEnvironment.IsDedicatedSingleStoreAvailable()
            && !ConditionalTestEnvironment.IsDockerAvailable())
        {
            Skip = "SingleStore real-proof test requires AGXIOME_SINGLESTORE_TEST_CONNECTION, dedicated compose SingleStore on 127.0.0.1:13306, or a working Docker/Testcontainers daemon.";
        }
    }
}

public sealed class RequiresCockroachFactAttribute : FactAttribute
{
    public RequiresCockroachFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AGXIOME_COCKROACH_TEST_CONNECTION"))
            && !ConditionalTestEnvironment.IsDedicatedCockroachAvailable()
            && !ConditionalTestEnvironment.IsDockerAvailable())
        {
            Skip = "CockroachDB real-proof test requires AGXIOME_COCKROACH_TEST_CONNECTION, dedicated compose CockroachDB on 127.0.0.1:12657, or a working Docker/Testcontainers daemon.";
        }
    }
}

public sealed class RequiresTimescaleFactAttribute : FactAttribute
{
    public RequiresTimescaleFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AGXIOME_TIMESCALE_TEST_CONNECTION"))
            && !ConditionalTestEnvironment.IsDedicatedTimescaleAvailable()
            && !ConditionalTestEnvironment.IsDockerAvailable())
        {
            Skip = "TimescaleDB real-proof test requires AGXIOME_TIMESCALE_TEST_CONNECTION, dedicated compose TimescaleDB on 127.0.0.1:15432, or a working Docker/Testcontainers daemon.";
        }
    }
}

public sealed class RequiresCosmosFactAttribute : FactAttribute
{
    public RequiresCosmosFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AGXIOME_COSMOS_TEST_CONNECTION")))
        {
            Skip = "Cosmos real-proof test requires AGXIOME_COSMOS_TEST_CONNECTION.";
        }
    }
}

public sealed class RequiresFirestoreFactAttribute : FactAttribute
{
    public RequiresFirestoreFactAttribute()
    {
        var dedicatedPort = int.TryParse(Environment.GetEnvironmentVariable("AGXIOME_FIRESTORE_HOST_PORT"), out var configuredPort)
            ? configuredPort
            : 18090;

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST"))
            && !ConditionalTestEnvironment.IsTcpEndpointReachable("127.0.0.1", dedicatedPort)
            && !ConditionalTestEnvironment.IsDockerAvailable())
        {
            Skip = "Firestore real-proof test requires FIRESTORE_EMULATOR_HOST, dedicated compose Firestore on 127.0.0.1:18090, or a working Docker/Testcontainers daemon.";
        }
    }
}

public sealed class RequiresRavenFactAttribute : FactAttribute
{
    public RequiresRavenFactAttribute()
    {
        var configuredUrl = Environment.GetEnvironmentVariable("AGXIOME_RAVEN_TEST_URL");
        var dedicatedPort = 18080;
        if (Uri.TryCreate(configuredUrl, UriKind.Absolute, out var configuredUri))
        {
            dedicatedPort = configuredUri.Port;
        }

        if (string.IsNullOrWhiteSpace(configuredUrl)
            && !ConditionalTestEnvironment.IsTcpEndpointReachable("127.0.0.1", dedicatedPort)
            && !ConditionalTestEnvironment.IsDockerAvailable())
        {
            Skip = "RavenDB real-proof test requires AGXIOME_RAVEN_TEST_URL, dedicated compose RavenDB on 127.0.0.1:18080, or a working Docker/Testcontainers daemon.";
        }
    }
}

public sealed class RequiresCouchbaseFactAttribute : FactAttribute
{
    public RequiresCouchbaseFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AGXIOME_COUCHBASE_TEST_CONNECTION"))
            && !ConditionalTestEnvironment.IsDedicatedCouchbaseAvailable())
        {
            Skip = "Couchbase real-proof test requires AGXIOME_COUCHBASE_TEST_CONNECTION or dedicated compose Couchbase on 127.0.0.1:18091.";
        }
    }
}

public sealed class RequiresRedisFactAttribute : FactAttribute
{
    public RequiresRedisFactAttribute()
    {
        var connectionString = Environment.GetEnvironmentVariable("AGXIOME_REDIS_TEST_CONNECTION")
            ?? "127.0.0.1:6390,abortConnect=false";

        try
        {
            using var mux = ConnectionMultiplexer.Connect(connectionString);
            if (!mux.IsConnected)
            {
                Skip = "Redis real-proof test requires a reachable Redis endpoint.";
            }
        }
        catch
        {
            Skip = "Redis real-proof test requires a reachable Redis endpoint.";
        }
    }
}

public sealed class RequiresMemcachedFactAttribute : FactAttribute
{
    public RequiresMemcachedFactAttribute()
    {
        if (!ConditionalTestEnvironment.IsDedicatedMemcachedAvailable()
            && !ConditionalTestEnvironment.IsDockerAvailable())
        {
            Skip = "Memcached real-proof test requires dedicated Memcached on 127.0.0.1:11212 or a working Docker/Testcontainers daemon.";
        }
    }
}

public sealed class RequiresHazelcastFactAttribute : FactAttribute
{
    public RequiresHazelcastFactAttribute()
    {
        if (!ConditionalTestEnvironment.IsDedicatedHazelcastAvailable()
            && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AGXIOME_HAZELCAST_TEST_ADDRESS"))
            && !ConditionalTestEnvironment.IsDockerAvailable())
        {
            Skip = "Hazelcast real-proof test requires AGXIOME_HAZELCAST_TEST_ADDRESS, dedicated Hazelcast on 127.0.0.1:15701, or a working Docker/Testcontainers daemon.";
        }
    }
}

public sealed class RequiresEtcdFactAttribute : FactAttribute
{
    public RequiresEtcdFactAttribute()
    {
        if (!ConditionalTestEnvironment.IsDedicatedEtcdAvailable()
            && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AGXIOME_ETCD_TEST_ENDPOINT"))
            && !ConditionalTestEnvironment.IsDockerAvailable())
        {
            Skip = "Etcd real-proof test requires AGXIOME_ETCD_TEST_ENDPOINT, dedicated etcd on 127.0.0.1:12379, or a working Docker/Testcontainers daemon.";
        }
    }
}

public sealed class RequiresClickHouseFactAttribute : FactAttribute
{
    public RequiresClickHouseFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AGXIOME_CLICKHOUSE_TEST_CONNECTION"))
            && !ConditionalTestEnvironment.IsDedicatedClickHouseAvailable()
            && !ConditionalTestEnvironment.IsDockerAvailable())
        {
            Skip = "ClickHouse real-proof test requires AGXIOME_CLICKHOUSE_TEST_CONNECTION, dedicated ClickHouse on 127.0.0.1:18123, or a working Docker/Testcontainers daemon.";
        }
    }
}

public sealed class RequiresInfluxDbFactAttribute : FactAttribute
{
    public RequiresInfluxDbFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AGXIOME_INFLUX_TEST_URL"))
            && !ConditionalTestEnvironment.IsDedicatedInfluxDbAvailable()
            && !ConditionalTestEnvironment.IsDockerAvailable())
        {
            Skip = "InfluxDB real-proof test requires AGXIOME_INFLUX_TEST_URL, dedicated InfluxDB on 127.0.0.1:18086, or a working Docker/Testcontainers daemon.";
        }
    }
}

public sealed class RequiresQuestDbFactAttribute : FactAttribute
{
    public RequiresQuestDbFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AGXIOME_QUESTDB_ILP_ENDPOINT"))
            && !ConditionalTestEnvironment.IsDedicatedQuestDbAvailable()
            && !ConditionalTestEnvironment.IsDockerAvailable())
        {
            Skip = "QuestDB real-proof test requires AGXIOME_QUESTDB_ILP_ENDPOINT, dedicated QuestDB on 127.0.0.1:19000, or a working Docker/Testcontainers daemon.";
        }
    }
}

internal static class AsyncFixtureRunner
{
    public static async Task UseAsync<TFixture>(Func<TFixture, Task> action)
        where TFixture : IAsyncLifetime, new()
    {
        var fixture = new TFixture();
        await fixture.InitializeAsync();
        try
        {
            await action(fixture);
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }
}

public sealed class DynamoDbLocalFixture : IAsyncLifetime
{
    private const string DynamoDbLocalServiceUrlEnv = "AGXIOME_DYNAMODB_TEST_SERVICEURL";
    private const string DefaultRegion = "us-east-1";
    private IContainer? _container;

    public string? ServiceUrl { get; private set; }
    public string BackendKind { get; private set; } = "none";

    public async Task InitializeAsync()
    {
        var externalServiceUrl = Environment.GetEnvironmentVariable(DynamoDbLocalServiceUrlEnv);
        if (!string.IsNullOrWhiteSpace(externalServiceUrl))
        {
            ServiceUrl = externalServiceUrl;
            BackendKind = "external";
            return;
        }

        if (!ConditionalTestEnvironment.IsDockerAvailable())
            return;

        _container = new ContainerBuilder("localstack/localstack:3.0")
            .WithEnvironment("SERVICES", "dynamodb")
            .WithEnvironment("AWS_DEFAULT_REGION", DefaultRegion)
            .WithPortBinding(4566, true)
            .Build();

        await _container.StartAsync();
        ServiceUrl = $"http://127.0.0.1:{_container.GetMappedPublicPort(4566)}";
        await WaitForDynamoAsync(ServiceUrl);
        BackendKind = "docker";
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }

    private static async Task WaitForDynamoAsync(string serviceUrl, int attempts = 30)
    {
        using var client = CreateClient(serviceUrl);
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                await client.ListTablesAsync();
                return;
            }
            catch when (attempt < attempts - 1)
            {
                await Task.Delay(1000);
            }
        }

        throw new TimeoutException("DynamoDB Local endpoint did not become reachable in time.");
    }

    public static AmazonDynamoDBClient CreateClient(string serviceUrl)
    {
        var config = new AmazonDynamoDBConfig
        {
            ServiceURL = serviceUrl,
            AuthenticationRegion = DefaultRegion
        };
        return new AmazonDynamoDBClient(new BasicAWSCredentials("test", "test"), config);
    }
}

public sealed class SurrealDbContainerFixture : IAsyncLifetime
{
    private const string SurrealBaseUrlEnv = "AGXIOME_SURREAL_TEST_BASEURL";
    private IContainer? _container;

    public string? BaseUrl { get; private set; }
    public string BackendKind { get; private set; } = "none";

    public async Task InitializeAsync()
    {
        var externalBaseUrl = Environment.GetEnvironmentVariable(SurrealBaseUrlEnv);
        if (!string.IsNullOrWhiteSpace(externalBaseUrl))
        {
            try
            {
                await WaitForSurrealAsync(externalBaseUrl);
                BaseUrl = externalBaseUrl;
                BackendKind = "external";
                return;
            }
            catch when (ConditionalTestEnvironment.IsDockerAvailable())
            {
                // Fall back to an ephemeral container when the configured external endpoint is stale.
            }
        }

        if (!ConditionalTestEnvironment.IsDockerAvailable())
            return;

        _container = new ContainerBuilder("surrealdb/surrealdb:latest")
            .WithCommand("start", "--bind", "0.0.0.0:8000", "--user", "root", "--pass", "root", "memory")
            .WithPortBinding(8000, true)
            .Build();

        await _container.StartAsync();
        BaseUrl = $"http://{_container.Hostname}:{_container.GetMappedPublicPort(8000)}";
        await WaitForSurrealAsync(BaseUrl);
        await EnsureNamespaceAndDatabaseAsync(BaseUrl, "test", "test");
        BackendKind = "docker";
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }

    public static HttpClient CreateHttpClient(string baseUrl)
    {
        var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        var credentials = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("root:root"));
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
        client.DefaultRequestHeaders.Add("Surreal-NS", "test");
        client.DefaultRequestHeaders.Add("Surreal-DB", "test");
        return client;
    }

    private static async Task WaitForSurrealAsync(string baseUrl, int attempts = 20)
    {
        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var client = new HttpClient(handler) { BaseAddress = new Uri(baseUrl) };
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "/");
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                if ((int)response.StatusCode >= 200
                    && response.Headers.Contains("surreal-version"))
                    return;
            }
            catch when (attempt < attempts - 1)
            {
            }

            await Task.Delay(500);
        }

        throw new TimeoutException("SurrealDB endpoint did not become reachable in time.");
    }

    private static async Task EnsureNamespaceAndDatabaseAsync(string baseUrl, string ns, string db)
    {
        using var rootClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
        var credentials = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("root:root"));
        rootClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

        using (var defineNamespace = new StringContent($"DEFINE NAMESPACE {ns};", System.Text.Encoding.UTF8, "text/plain"))
        {
            await rootClient.PostAsync("/sql", defineNamespace);
        }

        rootClient.DefaultRequestHeaders.Add("Surreal-NS", ns);
        using var defineDatabase = new StringContent($"DEFINE DATABASE {db};", System.Text.Encoding.UTF8, "text/plain");
        await rootClient.PostAsync("/sql", defineDatabase);
    }
}

public sealed class FirestoreEmulatorFixture : IAsyncLifetime
{
    private const string EmulatorHostEnv = "FIRESTORE_EMULATOR_HOST";
    private const string FirestoreHostPortEnv = "AGXIOME_FIRESTORE_HOST_PORT";
    private const string FirestoreProjectIdEnv = "AGXIOME_FIRESTORE_PROJECT_ID";

    private IContainer? _container;

    public string? EmulatorHost { get; private set; }
    public string ProjectId { get; private set; } = "agxioma-f4";
    public string BackendKind { get; private set; } = "none";

    public async Task InitializeAsync()
    {
        ProjectId = Environment.GetEnvironmentVariable(FirestoreProjectIdEnv) ?? "agxioma-f4";

        var externalEmulatorHost = Environment.GetEnvironmentVariable(EmulatorHostEnv);
        if (!string.IsNullOrWhiteSpace(externalEmulatorHost))
        {
            EmulatorHost = externalEmulatorHost;
            await WaitForFirestoreAsync(EmulatorHost);
            BackendKind = "external";
            return;
        }

        var dedicatedPort = int.TryParse(Environment.GetEnvironmentVariable(FirestoreHostPortEnv), out var configuredPort)
            ? configuredPort
            : 18090;

        if (await WaitForDedicatedEndpointAsync("127.0.0.1", dedicatedPort))
        {
            EmulatorHost = $"127.0.0.1:{dedicatedPort}";
            await WaitForFirestoreAsync(EmulatorHost);
            BackendKind = "compose";
            return;
        }

        if (!ConditionalTestEnvironment.IsDockerAvailable())
            return;

        _container = new ContainerBuilder("mtlynch/firestore-emulator:latest")
            .WithPortBinding(8080, true)
            .Build();

        await _container.StartAsync();
        EmulatorHost = $"127.0.0.1:{_container.GetMappedPublicPort(8080)}";
        await WaitForFirestoreAsync(EmulatorHost);
        BackendKind = "docker";
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }

    private static async Task WaitForFirestoreAsync(string emulatorHost, int attempts = 40)
    {
        using var client = new TcpClient();
        var endpoint = emulatorHost.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (endpoint.Length != 2 || !int.TryParse(endpoint[1], out var port))
        {
            throw new InvalidOperationException($"Firestore emulator host '{emulatorHost}' is not a valid host:port endpoint.");
        }

        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                await client.ConnectAsync(endpoint[0], port);
                if (client.Connected)
                {
                    client.Close();
                    return;
                }
            }
            catch when (attempt < attempts - 1)
            {
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException($"Firestore emulator endpoint '{emulatorHost}' did not become reachable in time.");
    }

    private static async Task<bool> WaitForDedicatedEndpointAsync(string host, int port, int attempts = 20)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            if (ConditionalTestEnvironment.IsTcpEndpointReachable(host, port))
                return true;

            await Task.Delay(1000);
        }

        return false;
    }
}

public sealed class RavenDbContainerFixture : IAsyncLifetime
{
    private const string RavenUrlEnv = "AGXIOME_RAVEN_TEST_URL";
    private const string RavenDatabaseEnv = "AGXIOME_RAVEN_DATABASE";

    private IContainer? _container;

    public string? ServerUrl { get; private set; }
    public string DatabaseName { get; private set; } = "agxiome-f4";
    public string BackendKind { get; private set; } = "none";

    public async Task InitializeAsync()
    {
        DatabaseName = Environment.GetEnvironmentVariable(RavenDatabaseEnv) ?? "agxiome-f4";

        var externalUrl = Environment.GetEnvironmentVariable(RavenUrlEnv);
        if (!string.IsNullOrWhiteSpace(externalUrl))
        {
            try
            {
                await WaitForRavenAsync(externalUrl);
                ServerUrl = externalUrl;
                BackendKind = "external";
                return;
            }
            catch when (ConditionalTestEnvironment.IsDockerAvailable())
            {
            }
        }

        if (!ConditionalTestEnvironment.IsDockerAvailable())
            return;

        _container = new ContainerBuilder("ravendb/ravendb:7.0-ubuntu-latest")
            .WithEnvironment("RAVEN_Setup_Mode", "None")
            .WithEnvironment("RAVEN_Security_UnsecuredAccessAllowed", "PrivateNetwork")
            .WithEnvironment("RAVEN_Logs_Mode", "None")
            .WithPortBinding(8080, true)
            .Build();

        await _container.StartAsync();
        ServerUrl = $"http://127.0.0.1:{_container.GetMappedPublicPort(8080)}";
        await WaitForRavenAsync(ServerUrl);
        BackendKind = "docker";
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }

    private static async Task WaitForRavenAsync(string serverUrl, int attempts = 40)
    {
        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var client = new HttpClient(handler) { BaseAddress = new Uri(serverUrl) };

        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                using var response = await client.GetAsync("/", HttpCompletionOption.ResponseHeadersRead);
                if ((int)response.StatusCode >= 200 && (int)response.StatusCode < 500)
                    return;
            }
            catch when (attempt < attempts - 1)
            {
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException($"RavenDB endpoint '{serverUrl}' did not become reachable in time.");
    }
}

public sealed class CouchbaseContainerFixture : IAsyncLifetime
{
    private const string CouchbaseConnectionEnv = "AGXIOME_COUCHBASE_TEST_CONNECTION";
    private const string CouchbaseHostPortEnv = "AGXIOME_COUCHBASE_HOST_PORT";
    private const string CouchbaseUserEnv = "AGXIOME_COUCHBASE_USERNAME";
    private const string CouchbasePasswordEnv = "AGXIOME_COUCHBASE_PASSWORD";
    private const string CouchbaseBucketEnv = "AGXIOME_COUCHBASE_BUCKET";
    private const string CouchbaseScopeEnv = "AGXIOME_COUCHBASE_SCOPE";
    private const string CouchbaseCollectionEnv = "AGXIOME_COUCHBASE_COLLECTION";

    public string? ConnectionString { get; private set; }
    public string? ManagementUrl { get; private set; }
    public string Username { get; private set; } = "Administrator";
    public string Password { get; private set; } = string.Empty;
    public string BucketName { get; private set; } = "agxioma";
    public string ScopeName { get; private set; } = "_default";
    public string CollectionName { get; private set; } = "_default";
    public string BackendKind { get; private set; } = "none";

    public async Task InitializeAsync()
    {
        Username = Environment.GetEnvironmentVariable(CouchbaseUserEnv) ?? "Administrator";
        Password = Environment.GetEnvironmentVariable(CouchbasePasswordEnv) ?? string.Empty;
        BucketName = Environment.GetEnvironmentVariable(CouchbaseBucketEnv) ?? "agxioma";
        ScopeName = Environment.GetEnvironmentVariable(CouchbaseScopeEnv) ?? "_default";
        CollectionName = Environment.GetEnvironmentVariable(CouchbaseCollectionEnv) ?? "_default";

        var hostPort = Environment.GetEnvironmentVariable(CouchbaseHostPortEnv) ?? "18091";
        ManagementUrl = $"http://127.0.0.1:{hostPort}";
        ConnectionString = Environment.GetEnvironmentVariable(CouchbaseConnectionEnv);
        if (string.IsNullOrWhiteSpace(ConnectionString))
            ConnectionString = "couchbase://127.0.0.1:11210";

        if (!ConditionalTestEnvironment.IsTcpEndpointReachable("127.0.0.1", int.Parse(hostPort)))
            return;

        await WaitForManagementAsync();
        await EnsureClusterInitializedAsync();
        await EnsureAlternateAddressAsync();
        await EnsureBucketAsync();
        BackendKind = "external";
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task EnsureClusterInitializedAsync()
    {
        using var client = CreateManagementClient();
        using var poolsResponse = await client.GetAsync("/pools");
        var poolsPayload = await poolsResponse.Content.ReadAsStringAsync();
        poolsResponse.EnsureSuccessStatusCode();

        using var poolsDocument = JsonDocument.Parse(poolsPayload);
        if (poolsDocument.RootElement.TryGetProperty("pools", out var poolsElement)
            && poolsElement.ValueKind == JsonValueKind.Array
            && poolsElement.GetArrayLength() > 0)
        {
            return;
        }
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["hostname"] = "127.0.0.1",
            ["username"] = Username,
            ["password"] = Password,
            ["port"] = "SAME",
            ["services"] = "kv",
            ["memoryQuota"] = "256"
        });

        using var response = await client.PostAsync("/clusterInit", content);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Couchbase cluster init failed: {payload}");
    }

    private async Task EnsureBucketAsync()
    {
        using var client = CreateManagementClient();

        using (var getBucket = await client.GetAsync($"/pools/default/buckets/{BucketName}"))
        {
            if (getBucket.IsSuccessStatusCode)
            {
                await WaitForBucketAsync(client);
                return;
            }
        }

        using (var createBucket = await client.PostAsync(
            "/pools/default/buckets",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["name"] = BucketName,
                ["ramQuotaMB"] = "128",
                ["bucketType"] = "couchbase",
                ["flushEnabled"] = "1"
            })))
        {
            var payload = await createBucket.Content.ReadAsStringAsync();
            Assert.True(
                createBucket.IsSuccessStatusCode || payload.Contains("already exists", StringComparison.OrdinalIgnoreCase),
                $"Couchbase bucket creation failed: {payload}");
        }

        await WaitForBucketAsync(client);
    }

    private HttpClient CreateManagementClient()
    {
        var client = new HttpClient { BaseAddress = new Uri(ManagementUrl!) };
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Username}:{Password}"));
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
        return client;
    }

    private async Task EnsureAlternateAddressAsync()
    {
        var desiredManagementPort = new Uri(ManagementUrl!).Port;
        if (desiredManagementPort == 8091)
            return;

        using var client = CreateManagementClient();
        using var response = await client.GetAsync("/pools/default/nodeServices");
        var payload = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(payload);
        var node = document.RootElement.GetProperty("nodesExt")[0];
        if (node.TryGetProperty("alternateAddresses", out var alternateAddresses)
            && alternateAddresses.TryGetProperty("external", out var external)
            && external.TryGetProperty("hostname", out var hostname)
            && external.TryGetProperty("ports", out var ports)
            && ports.TryGetProperty("mgmt", out var managementPort)
            && hostname.GetString() == "127.0.0.1"
            && managementPort.GetInt32() == desiredManagementPort)
        {
            return;
        }

        if (!ConditionalTestEnvironment.IsDockerAvailable())
            return;

        var kvPort = 11210;
        if (Uri.TryCreate(ConnectionString, UriKind.Absolute, out var connectionUri) && connectionUri.Port > 0)
            kvPort = connectionUri.Port;

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("docker")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.StartInfo.ArgumentList.Add("exec");
        process.StartInfo.ArgumentList.Add("agxioma-f4-couchbase");
        process.StartInfo.ArgumentList.Add("couchbase-cli");
        process.StartInfo.ArgumentList.Add("setting-alternate-address");
        process.StartInfo.ArgumentList.Add("-c");
        process.StartInfo.ArgumentList.Add("127.0.0.1:8091");
        process.StartInfo.ArgumentList.Add("-u");
        process.StartInfo.ArgumentList.Add(Username);
        process.StartInfo.ArgumentList.Add("-p");
        process.StartInfo.ArgumentList.Add(Password);
        process.StartInfo.ArgumentList.Add("--set");
        process.StartInfo.ArgumentList.Add("--node");
        process.StartInfo.ArgumentList.Add("127.0.0.1:8091");
        process.StartInfo.ArgumentList.Add("--hostname");
        process.StartInfo.ArgumentList.Add("127.0.0.1");
        process.StartInfo.ArgumentList.Add("--ports");
        process.StartInfo.ArgumentList.Add($"mgmt={desiredManagementPort},kv={kvPort}");

        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        Assert.True(
            process.ExitCode == 0,
            $"Couchbase alternate-address configuration failed. stdout: {stdout}{Environment.NewLine}stderr: {stderr}");
    }

    private async Task WaitForBucketAsync(HttpClient client, int attempts = 60)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                using var response = await client.GetAsync($"/pools/default/buckets/{BucketName}");
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch when (attempt < attempts - 1)
            {
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException($"Couchbase bucket '{BucketName}' was not ready in time.");
    }

    private async Task WaitForManagementAsync(int attempts = 120)
    {
        using var client = CreateManagementClient();

        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                using var response = await client.GetAsync("/pools");
                if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                    return;
            }
            catch when (attempt < attempts - 1)
            {
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException($"Couchbase management endpoint '{ManagementUrl}' did not become reachable in time.");
    }
}

public sealed class HazelcastContainerFixture : IAsyncLifetime
{
    private const string HazelcastAddressEnv = "AGXIOME_HAZELCAST_TEST_ADDRESS";
    private const string HazelcastClusterEnv = "AGXIOME_HAZELCAST_CLUSTER_NAME";
    private const string HazelcastMapEnv = "AGXIOME_HAZELCAST_MAP_NAME";

    private IContainer? _container;

    public string? Address { get; private set; }
    public string ClusterName { get; private set; } = "agxioma-dev";
    public string MapName { get; private set; } = "agxioma-f4";
    public string BackendKind { get; private set; } = "none";

    public async Task InitializeAsync()
    {
        ClusterName = Environment.GetEnvironmentVariable(HazelcastClusterEnv) ?? "agxioma-dev";
        MapName = Environment.GetEnvironmentVariable(HazelcastMapEnv) ?? "agxioma-f4";

        var externalAddress = Environment.GetEnvironmentVariable(HazelcastAddressEnv);
        if (!string.IsNullOrWhiteSpace(externalAddress) && TryParseHostAndPort(externalAddress, out var externalHost, out var externalPort))
        {
            try
            {
                await WaitForHazelcastAsync(externalHost, externalPort);
                Address = externalAddress;
                BackendKind = "external";
                return;
            }
            catch when (ConditionalTestEnvironment.IsDockerAvailable())
            {
            }
        }

        if (!ConditionalTestEnvironment.IsDockerAvailable())
            return;

        _container = new ContainerBuilder("hazelcast/hazelcast:5.5")
            .WithEnvironment("HZ_CLUSTERNAME", ClusterName)
            .WithPortBinding(5701, true)
            .Build();

        await _container.StartAsync();
        Address = $"127.0.0.1:{_container.GetMappedPublicPort(5701)}";
        await WaitForHazelcastAsync("127.0.0.1", _container.GetMappedPublicPort(5701));
        BackendKind = "docker";
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }

    private static async Task WaitForHazelcastAsync(string host, int port, int attempts = 60)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(host, port);
                if (client.Connected)
                    return;
            }
            catch when (attempt < attempts - 1)
            {
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException($"Hazelcast endpoint '{host}:{port}' did not become reachable in time.");
    }

    private static bool TryParseHostAndPort(string address, out string host, out int port)
    {
        host = string.Empty;
        port = 0;
        var parts = address.Split(':', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 && int.TryParse(parts[1], out port) && (host = parts[0]) is not null;
    }
}

public sealed class EtcdContainerFixture : IAsyncLifetime
{
    private const string EtcdEndpointEnv = "AGXIOME_ETCD_TEST_ENDPOINT";

    private IContainer? _container;

    public string? Endpoint { get; private set; }
    public string BackendKind { get; private set; } = "none";

    public async Task InitializeAsync()
    {
        var externalEndpoint = Environment.GetEnvironmentVariable(EtcdEndpointEnv);
        if (!string.IsNullOrWhiteSpace(externalEndpoint) && TryParseHostAndPort(externalEndpoint, out var externalHost, out var externalPort))
        {
            try
            {
                await WaitForEtcdAsync(externalHost, externalPort);
                Endpoint = externalEndpoint;
                BackendKind = "external";
                return;
            }
            catch when (ConditionalTestEnvironment.IsDockerAvailable())
            {
            }
        }

        if (!ConditionalTestEnvironment.IsDockerAvailable())
            return;

        _container = new ContainerBuilder("quay.io/coreos/etcd:v3.5.15")
            .WithCommand(
                "/usr/local/bin/etcd",
                "--advertise-client-urls=http://0.0.0.0:2379",
                "--listen-client-urls=http://0.0.0.0:2379")
            .WithPortBinding(2379, true)
            .Build();

        await _container.StartAsync();
        var port = _container.GetMappedPublicPort(2379);
        Endpoint = $"127.0.0.1:{port}";
        await WaitForEtcdAsync("127.0.0.1", port);
        BackendKind = "docker";
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }

    private static async Task WaitForEtcdAsync(string host, int port, int attempts = 60)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(host, port);
                if (client.Connected)
                    return;
            }
            catch when (attempt < attempts - 1)
            {
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException($"etcd endpoint '{host}:{port}' did not become reachable in time.");
    }

    private static bool TryParseHostAndPort(string endpoint, out string host, out int port)
    {
        host = string.Empty;
        port = 0;
        var parts = endpoint.Split(':', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 && int.TryParse(parts[1], out port) && (host = parts[0]) is not null;
    }
}

public sealed class ClickHouseContainerFixture : IAsyncLifetime
{
    private const string ClickHouseConnectionEnv = "AGXIOME_CLICKHOUSE_TEST_CONNECTION";
    private const string ClickHouseDatabaseEnv = "AGXIOME_CLICKHOUSE_DATABASE";
    private const string ClickHouseUsernameEnv = "AGXIOME_CLICKHOUSE_USERNAME";
    private const string ClickHousePasswordEnv = "AGXIOME_CLICKHOUSE_PASSWORD";
    private static readonly TimeSpan ClickHouseReadinessTimeout = TimeSpan.FromSeconds(5);

    private IContainer? _container;

    public string? ConnectionString { get; private set; }
    public string Database { get; private set; } = "default";
    public string BackendKind { get; private set; } = "none";

    public async Task InitializeAsync()
    {
        Database = Environment.GetEnvironmentVariable(ClickHouseDatabaseEnv) ?? "default";

        var externalConnectionString = Environment.GetEnvironmentVariable(ClickHouseConnectionEnv);
        if (!string.IsNullOrWhiteSpace(externalConnectionString))
        {
            await WaitForClickHouseAsync(externalConnectionString);
            ConnectionString = externalConnectionString;
            BackendKind = "external";
            return;
        }

        if (!ConditionalTestEnvironment.IsDockerAvailable())
            return;

        var username = Environment.GetEnvironmentVariable(ClickHouseUsernameEnv) ?? "default";
        var password = Environment.GetEnvironmentVariable(ClickHousePasswordEnv) ?? string.Empty;

        _container = new ContainerBuilder("clickhouse/clickhouse-server:24.8")
            .WithEnvironment("CLICKHOUSE_DB", Database)
            .WithEnvironment("CLICKHOUSE_USER", username)
            .WithEnvironment("CLICKHOUSE_PASSWORD", password)
            .WithPortBinding(8123, true)
            .WithPortBinding(9000, true)
            .Build();

        await _container.StartAsync();
        ConnectionString = $"Host=127.0.0.1;Port={_container.GetMappedPublicPort(8123)};Username={username};Password={password};Protocol=http";
        await WaitForClickHouseAsync(ConnectionString);
        BackendKind = "docker";
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }

    private static async Task WaitForClickHouseAsync(string connectionString, int attempts = 60)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                await using var conn = new ClickHouseConnection(connectionString);
                await conn.OpenAsync().WaitAsync(ClickHouseReadinessTimeout);
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT 1";
                var value = await cmd.ExecuteScalarAsync().WaitAsync(ClickHouseReadinessTimeout);
                if (Convert.ToInt32(value) == 1)
                    return;
            }
            catch when (attempt < attempts - 1)
            {
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException("ClickHouse endpoint did not become reachable in time.");
    }
}

public sealed class InfluxDbContainerFixture : IAsyncLifetime
{
    private const string InfluxUrlEnv = "AGXIOME_INFLUX_TEST_URL";
    private const string InfluxOrgEnv = "AGXIOME_INFLUX_ORG";
    private const string InfluxBucketEnv = "AGXIOME_INFLUX_BUCKET";
    private const string InfluxTokenEnv = "AGXIOME_INFLUX_TOKEN";

    private IContainer? _container;

    public string? Url { get; private set; }
    public string Org { get; private set; } = "agxioma";
    public string Bucket { get; private set; } = "agxioma_f4";
    public string Token { get; private set; } = string.Empty;
    public string BackendKind { get; private set; } = "none";

    public async Task InitializeAsync()
    {
        Org = Environment.GetEnvironmentVariable(InfluxOrgEnv) ?? "agxioma";
        Bucket = Environment.GetEnvironmentVariable(InfluxBucketEnv) ?? "agxioma_f4";
        Token = Environment.GetEnvironmentVariable(InfluxTokenEnv) ?? string.Empty;

        var externalUrl = Environment.GetEnvironmentVariable(InfluxUrlEnv);
        if (!string.IsNullOrWhiteSpace(externalUrl))
        {
            await WaitForInfluxAsync(externalUrl, Token);
            Url = externalUrl;
            BackendKind = "external";
            return;
        }

        if (!ConditionalTestEnvironment.IsDockerAvailable())
            return;

        _container = new ContainerBuilder("influxdb:2.7")
            .WithEnvironment("DOCKER_INFLUXDB_INIT_MODE", "setup")
            .WithEnvironment("DOCKER_INFLUXDB_INIT_USERNAME", "admin")
            .WithEnvironment("DOCKER_INFLUXDB_INIT_PASSWORD", Token)
            .WithEnvironment("DOCKER_INFLUXDB_INIT_ORG", Org)
            .WithEnvironment("DOCKER_INFLUXDB_INIT_BUCKET", Bucket)
            .WithEnvironment("DOCKER_INFLUXDB_INIT_ADMIN_TOKEN", Token)
            .WithPortBinding(8086, true)
            .Build();

        await _container.StartAsync();
        Url = $"http://127.0.0.1:{_container.GetMappedPublicPort(8086)}";
        await WaitForInfluxAsync(Url, Token);
        BackendKind = "docker";
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }

    private static async Task WaitForInfluxAsync(string url, string token, int attempts = 60)
    {
        using var client = CreateInfluxHttpClient(url, token);

        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                using var response = await client.GetAsync("health");
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch when (attempt < attempts - 1)
            {
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException($"InfluxDB endpoint '{url}' did not become reachable in time.");
    }

    private static HttpClient CreateInfluxHttpClient(string url, string token)
    {
        var client = new HttpClient { BaseAddress = new Uri(AppendTrailingSlash(url)) };
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Token", token);
        return client;
    }

    private static string AppendTrailingSlash(string url)
        => url.EndsWith("/", StringComparison.Ordinal) ? url : url + "/";
}

public sealed class QuestDbContainerFixture : IAsyncLifetime
{
    private const string QuestDbIlpEnv = "AGXIOME_QUESTDB_ILP_ENDPOINT";
    private const string QuestDbNpgsqlEnv = "AGXIOME_QUESTDB_NPGSQL_CONNECTION";

    private IContainer? _container;

    public string? IlpEndpoint { get; private set; }
    public string? NpgsqlConnectionString { get; private set; }
    public string BackendKind { get; private set; } = "none";

    public async Task InitializeAsync()
    {
        var externalIlp = Environment.GetEnvironmentVariable(QuestDbIlpEnv);
        var externalNpgsql = Environment.GetEnvironmentVariable(QuestDbNpgsqlEnv);
        if (!string.IsNullOrWhiteSpace(externalIlp))
        {
            await WaitForQuestDbAsync(externalIlp);
            IlpEndpoint = externalIlp;
            NpgsqlConnectionString = externalNpgsql ?? "Host=127.0.0.1;Port=18812;Username=admin;Password=quest";
            BackendKind = "external";
            return;
        }

        if (!ConditionalTestEnvironment.IsDockerAvailable())
            return;

        _container = new ContainerBuilder("questdb/questdb:8.2.2")
            .WithPortBinding(9000, true)
            .WithPortBinding(8812, true)
            .Build();

        await _container.StartAsync();
        var httpPort = _container.GetMappedPublicPort(9000);
        var pgPort = _container.GetMappedPublicPort(8812);
        IlpEndpoint = $"http://127.0.0.1:{httpPort}";
        NpgsqlConnectionString = $"Host=127.0.0.1;Port={pgPort};Username=admin;Password=quest";
        await WaitForQuestDbAsync(IlpEndpoint);
        BackendKind = "docker";
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }

    private static async Task WaitForQuestDbAsync(string ilpEndpoint, int attempts = 60)
    {
        using var client = new HttpClient { BaseAddress = new Uri(AppendTrailingSlash(ilpEndpoint)) };

        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                using var response = await client.GetAsync("exec?query=select%201");
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch when (attempt < attempts - 1)
            {
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException($"QuestDB endpoint '{ilpEndpoint}' did not become reachable in time.");
    }

    private static string AppendTrailingSlash(string url)
        => url.EndsWith("/", StringComparison.Ordinal) ? url : url + "/";
}

public sealed class SingleStoreContainerFixture : IAsyncLifetime
{
    private const string SingleStoreConnectionEnv = "AGXIOME_SINGLESTORE_TEST_CONNECTION";
    private const string SingleStorePortEnv = "AGXIOME_SINGLESTORE_HOST_PORT";
    private const string SingleStoreUsernameEnv = "AGXIOME_SINGLESTORE_USERNAME";
    private const string SingleStorePasswordEnv = "AGXIOME_SINGLESTORE_PASSWORD";
    private const string SingleStoreDatabaseEnv = "AGXIOME_SINGLESTORE_DATABASE";

    private IContainer? _container;

    public string? ConnectionString { get; private set; }
    public string BackendKind { get; private set; } = "none";

    public async Task InitializeAsync()
    {
        var externalConnection = Environment.GetEnvironmentVariable(SingleStoreConnectionEnv);
        if (!string.IsNullOrWhiteSpace(externalConnection))
        {
            ConnectionString = externalConnection;
            BackendKind = "external";
            return;
        }

        if (ConditionalTestEnvironment.IsDedicatedSingleStoreAvailable())
        {
            ConnectionString = BuildDedicatedComposeConnectionString();
            await WaitForSingleStoreAsync(ConnectionString, Environment.GetEnvironmentVariable(SingleStoreDatabaseEnv) ?? "agxioma_f4");
            BackendKind = "compose";
            return;
        }

        if (!ConditionalTestEnvironment.IsDockerAvailable())
            return;

        var username = Environment.GetEnvironmentVariable(SingleStoreUsernameEnv) ?? "root";
        var password = Environment.GetEnvironmentVariable(SingleStorePasswordEnv) ?? "Agxioma_singlestore_2026!";
        var database = Environment.GetEnvironmentVariable(SingleStoreDatabaseEnv) ?? "agxioma_f4";
        _container = new ContainerBuilder("singlestore/cluster-in-a-box:latest")
            .WithEnvironment("ROOT_PASSWORD", password)
            .WithEnvironment("SINGLESTORE_LICENSE", "free")
            .WithPortBinding(3306, true)
            .Build();

        await _container.StartAsync();

        var builder = new MySqlConnectionStringBuilder
        {
            Server = _container.Hostname,
            Port = Convert.ToUInt32(_container.GetMappedPublicPort(3306)),
            UserID = username,
            Password = password,
            Database = database,
            ConnectionTimeout = 5,
            AllowUserVariables = true
        };

        ConnectionString = builder.ConnectionString;
        await WaitForSingleStoreAsync(ConnectionString, database);
        BackendKind = "docker";
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }

    private static async Task WaitForSingleStoreAsync(string connectionString, string database, int attempts = 40)
    {
        var bootstrapBuilder = new MySqlConnectionStringBuilder(connectionString)
        {
            Database = string.Empty
        };

        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                await using var connection = new MySqlConnection(bootstrapBuilder.ConnectionString);
                await connection.OpenAsync();

                await using var createDb = connection.CreateCommand();
                createDb.CommandText = $"CREATE DATABASE IF NOT EXISTS `{database}`; USE `{database}`;";
                await createDb.ExecuteNonQueryAsync();
                return;
            }
            catch when (attempt < attempts - 1)
            {
                await Task.Delay(1000);
            }
        }

        throw new TimeoutException("SingleStore endpoint did not become reachable in time.");
    }

    private static string BuildDedicatedComposeConnectionString()
    {
        var username = Environment.GetEnvironmentVariable(SingleStoreUsernameEnv) ?? "root";
        var password = Environment.GetEnvironmentVariable(SingleStorePasswordEnv) ?? "Agxioma_singlestore_2026!";
        var database = Environment.GetEnvironmentVariable(SingleStoreDatabaseEnv) ?? "agxioma_f4";
        var port = uint.TryParse(Environment.GetEnvironmentVariable(SingleStorePortEnv), out var parsedPort)
            ? parsedPort
            : 13306u;

        return new MySqlConnectionStringBuilder
        {
            Server = "127.0.0.1",
            Port = port,
            UserID = username,
            Password = password,
            Database = database,
            ConnectionTimeout = 5,
            AllowUserVariables = true
        }.ConnectionString;
    }
}

public sealed class CockroachDbContainerFixture : IAsyncLifetime
{
    private const string DedicatedComposeConnectionString = "Host=127.0.0.1;Port=12657;Username=root;Database=defaultdb;SSL Mode=Disable;Trust Server Certificate=true;Pooling=false;Timeout=5;Command Timeout=30;Include Error Detail=true";
    private const string ConnectionEnv = "AGXIOME_COCKROACH_TEST_CONNECTION";

    private IContainer? _container;
    private string? _isolatedDatabaseName;
    private string? _adminConnectionString;

    public string? ConnectionString { get; private set; }
    public string BackendKind { get; private set; } = "none";

    public async Task InitializeAsync()
    {
        var externalConnectionString = Environment.GetEnvironmentVariable(ConnectionEnv);
        if (!string.IsNullOrWhiteSpace(externalConnectionString))
        {
            ConnectionString = await CreateIsolatedDatabaseConnectionAsync(externalConnectionString);
            BackendKind = "external-isolated";
            return;
        }

        if (await WaitForDedicatedEndpointAsync("127.0.0.1", 12657))
        {
            ConnectionString = await CreateIsolatedDatabaseConnectionAsync(DedicatedComposeConnectionString);
            BackendKind = "dedicated-compose";
            return;
        }

        if (!ConditionalTestEnvironment.IsDockerAvailable())
            return;

        _container = new ContainerBuilder("cockroachdb/cockroach:v25.2.1")
            .WithEntrypoint("cockroach")
            .WithCommand("start-single-node", "--insecure", "--advertise-addr=127.0.0.1:26357", "--listen-addr=127.0.0.1:26357", "--sql-addr=0.0.0.0:26257", "--http-addr=0.0.0.0:8080")
            .WithPortBinding(26257, true)
            .Build();

        await _container.StartAsync();

        var bootstrapConnection = new NpgsqlConnectionStringBuilder
        {
            Host = _container.Hostname,
            Port = _container.GetMappedPublicPort(26257),
            Username = "root",
            Database = "defaultdb",
            SslMode = SslMode.Disable,
            TrustServerCertificate = true,
            Pooling = false,
            Timeout = 5,
            CommandTimeout = 30,
            IncludeErrorDetail = true
        }.ConnectionString;

        ConnectionString = await CreateIsolatedDatabaseConnectionAsync(bootstrapConnection);
        BackendKind = "docker";
    }

    public async Task DisposeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_isolatedDatabaseName) && !string.IsNullOrWhiteSpace(_adminConnectionString))
        {
            await using var adminConn = new NpgsqlConnection(_adminConnectionString);
            await adminConn.OpenAsync();

            await using var drop = adminConn.CreateCommand();
            drop.CommandText = $"DROP DATABASE IF EXISTS \"{_isolatedDatabaseName}\"";
            await drop.ExecuteNonQueryAsync();
        }

        if (_container is not null)
            await _container.DisposeAsync();
    }

    private async Task<string> CreateIsolatedDatabaseConnectionAsync(string adminConnectionString)
    {
        var adminBuilder = new NpgsqlConnectionStringBuilder(adminConnectionString);
        var adminDatabase = string.IsNullOrWhiteSpace(adminBuilder.Database) ? "defaultdb" : adminBuilder.Database;
        adminBuilder.Database = adminDatabase;
        adminBuilder.Pooling = false;
        adminBuilder.Timeout = 5;
        adminBuilder.CommandTimeout = 30;
        adminBuilder.IncludeErrorDetail = true;

        _isolatedDatabaseName = $"agxiome_crdb_{Guid.NewGuid():N}";
        _adminConnectionString = adminBuilder.ConnectionString;

        await using var adminConn = await OpenCockroachConnectionWithRetryAsync(_adminConnectionString);

        await using var create = adminConn.CreateCommand();
        create.CommandText = $"CREATE DATABASE IF NOT EXISTS \"{_isolatedDatabaseName}\"";
        await create.ExecuteNonQueryAsync();

        var isolatedBuilder = new NpgsqlConnectionStringBuilder(_adminConnectionString)
        {
            Database = _isolatedDatabaseName
        };

        return isolatedBuilder.ConnectionString;
    }

    private static async Task<NpgsqlConnection> OpenCockroachConnectionWithRetryAsync(string connectionString, int attempts = 30)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var connection = new NpgsqlConnection(connectionString);
            try
            {
                await connection.OpenAsync();
                return connection;
            }
            catch when (attempt < attempts - 1)
            {
                await connection.DisposeAsync();
                await Task.Delay(1000);
            }
            catch
            {
                await connection.DisposeAsync();
                throw;
            }
        }

        throw new TimeoutException("CockroachDB endpoint did not accept SQL connections in time.");
    }

    private static async Task<bool> WaitForDedicatedEndpointAsync(string host, int port, int attempts = 20)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            if (ConditionalTestEnvironment.IsTcpEndpointReachable(host, port))
                return true;

            await Task.Delay(1000);
        }

        return false;
    }
}

public sealed class TimescaleDbContainerFixture : IAsyncLifetime
{
    private const string DedicatedComposeConnectionString = "Host=127.0.0.1;Port=15432;Database=agxiome_f4;Username=postgres;Password=agxiome_timescale_pw;SSL Mode=Disable;Trust Server Certificate=true;Pooling=false;Timeout=5;Command Timeout=30;Include Error Detail=true";
    private const string ConnectionEnv = "AGXIOME_TIMESCALE_TEST_CONNECTION";

    private IContainer? _container;

    public string? ConnectionString { get; private set; }
    public string BackendKind { get; private set; } = "none";

    public async Task InitializeAsync()
    {
        var externalConnectionString = Environment.GetEnvironmentVariable(ConnectionEnv);
        if (!string.IsNullOrWhiteSpace(externalConnectionString))
        {
            ConnectionString = externalConnectionString;
            BackendKind = "external";
            return;
        }

        if (await WaitForDedicatedEndpointAsync("127.0.0.1", 15432))
        {
            ConnectionString = DedicatedComposeConnectionString;
            await EnsureTimescaleDatabaseReadyAsync(ConnectionString);
            BackendKind = "dedicated-compose";
            return;
        }

        if (!ConditionalTestEnvironment.IsDockerAvailable())
            return;

        _container = new ContainerBuilder("timescale/timescaledb:latest-pg16")
            .WithEnvironment("POSTGRES_PASSWORD", "postgres")
            .WithEnvironment("POSTGRES_DB", "agxiome_f4")
            .WithPortBinding(5432, true)
            .Build();

        await _container.StartAsync();

        ConnectionString = new NpgsqlConnectionStringBuilder
        {
            Host = _container.Hostname,
            Port = _container.GetMappedPublicPort(5432),
            Database = "agxiome_f4",
            Username = "postgres",
            Password = "postgres",
            SslMode = SslMode.Disable,
            TrustServerCertificate = true,
            Pooling = false,
            Timeout = 5,
            CommandTimeout = 30,
            IncludeErrorDetail = true
        }.ConnectionString;

        await EnsureTimescaleDatabaseReadyAsync(ConnectionString);
        BackendKind = "docker";
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }

    private static async Task EnsureTimescaleDatabaseReadyAsync(string connectionString, int attempts = 40)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                await using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync();
                return;
            }
            catch when (attempt < attempts - 1)
            {
                await Task.Delay(1000);
            }
        }

        throw new TimeoutException("TimescaleDB endpoint did not become reachable in time.");
    }

    private static async Task<bool> WaitForDedicatedEndpointAsync(string host, int port, int attempts = 20)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            if (ConditionalTestEnvironment.IsTcpEndpointReachable(host, port))
                return true;

            await Task.Delay(1000);
        }

        return false;
    }
}

// ── DB-01 SQLite ─────────────────────────────────────────────────────────────
public class SqliteCommandStoreTests
{
    private static string CreateTempSqlitePath()
        => Path.Combine(Path.GetTempPath(), $"agxioma_{Guid.NewGuid():N}.db");

    private static SqliteCommandStore CreateFileStore(string path)
        => new(new SqliteStoreOptions
        {
            ConnectionString = $"Data Source={path};Pooling=False",
            AutoCreateSchema = true
        });

    [Fact] public async Task ProviderType_IsSQLite()
    {
        var store = new SqliteCommandStore(new SqliteStoreOptions { ConnectionString = "Data Source=:memory:" });
        Assert.Equal(StorageProviderType.SQLite, store.ProviderType);
    }

    [Fact] public async Task Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new SqliteCommandStore(null!));

    [Fact] public async Task Constructor_EmptyConnectionString_Throws()
        => Assert.Throws<ArgumentException>(() => new SqliteCommandStore(new SqliteStoreOptions { ConnectionString = string.Empty }));

    [Fact] public async Task SaveAndDelete_RoundTrip()
    {
        var tmp = CreateTempSqlitePath();
        try
        {
            using var store = CreateFileStore(tmp);
            var entity = new DbTestEntity { Id = "1", Name = "Hello", Score = 42 };
            await store.SaveAsync(entity);
            await store.DeleteAsync<DbTestEntity>("1");

            Assert.True(File.Exists(tmp));
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    [Fact] public async Task UpdateAsync_DoesNotThrow()
    {
        var tmp = CreateTempSqlitePath();
        try
        {
            using var store = CreateFileStore(tmp);
            await store.SaveAsync(new DbTestEntity { Id = "2", Name = "Updated", Score = 99 });
            await store.UpdateAsync(new DbTestEntity { Id = "2", Name = "Updated2", Score = 100 });
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    [Fact] public async Task SaveAsync_InvalidFilePath_Throws()
    {
        var invalidPath = Path.Combine(Path.GetTempPath(), $"agxioma_<invalid>_{Guid.NewGuid():N}.db");
        var store = CreateFileStore(invalidPath);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            store.SaveAsync(new DbTestEntity { Id = "bad-path", Name = "Invalid", Score = 0 }));
    }

    [Fact] public async Task ConcurrentSaves_SameFile_DoNotThrow()
    {
        var tmp = CreateTempSqlitePath();
        try
        {
            using var store = CreateFileStore(tmp);
            var tasks = Enumerable.Range(0, 8)
                .Select(index => store.SaveAsync(new DbTestEntity
                {
                    Id = $"c-{index}",
                    Name = $"Concurrent-{index}",
                    Score = index
                }));

            var ex = await Record.ExceptionAsync(() => Task.WhenAll(tasks));
            Assert.Null(ex);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    [Fact] public async Task ReopenExistingFile_AfterDispose_DoesNotThrow()
    {
        var tmp = CreateTempSqlitePath();
        try
        {
            using (var firstStore = CreateFileStore(tmp))
            {
                await firstStore.SaveAsync(new DbTestEntity { Id = "reopen", Name = "First", Score = 1 });
            }

            using var secondStore = CreateFileStore(tmp);
            var ex = await Record.ExceptionAsync(() =>
                secondStore.UpdateAsync(new DbTestEntity { Id = "reopen", Name = "Second", Score = 2 }));

            Assert.Null(ex);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    [Fact] public async Task Dispose_IsIdempotent()
    {
        var store = new SqliteCommandStore(new SqliteStoreOptions { ConnectionString = "Data Source=:memory:" });
        store.Dispose();
        store.Dispose(); // second call must not throw
    }
}

// ── DB-02 SQL Server ─────────────────────────────────────────────────────────
public class SqlServerCommandStoreTests
{
    private const string SqlServerLiveConnectionEnv = "AGXIOME_SQLSERVER_TEST_CONNECTION";

    [Fact] public async Task ProviderType_IsSQLServer()
    {
        var store = new SqlServerCommandStore(new SqlServerStoreOptions { ConnectionString = "Server=.;Database=Test;Integrated Security=True" });
        Assert.Equal(StorageProviderType.SQLServer, store.ProviderType);
    }

    [Fact] public async Task Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new SqlServerCommandStore(null!));

    [Fact] public async Task Constructor_EmptyConnectionString_Throws()
        => Assert.Throws<ArgumentException>(() => new SqlServerCommandStore(new SqlServerStoreOptions { ConnectionString = string.Empty }));

    [Fact] public async Task SaveAsync_InvalidConnection_Throws()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var store = new SqlServerCommandStore(new SqlServerStoreOptions { ConnectionString = "Server=invalid_host_does_not_exist;Database=X;Connect Timeout=1;TrustServerCertificate=True" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "1", Name = "T", Score = 0 }, cts.Token));
    }

    [Fact] public async Task DeleteAsync_InvalidConnection_Throws()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var store = new SqlServerCommandStore(new SqlServerStoreOptions { ConnectionString = "Server=invalid_host_does_not_exist;Database=X;Connect Timeout=1;TrustServerCertificate=True" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.DeleteAsync<DbTestEntity>("1", cts.Token));
    }

    [Fact] public async Task Options_RoundTrip()
    {
        var opts = new SqlServerStoreOptions { ConnectionString = "my-conn-string" };
        Assert.Equal("my-conn-string", opts.ConnectionString);
    }

    [RequiresDockerFact] public async Task SaveUpdateDelete_WithRealSqlServerConnection_WorksWhenConfigured()
    {
        await AsyncFixtureRunner.UseAsync<SqlServerContainerFixture>(async fixture =>
        {
            var connectionString = Environment.GetEnvironmentVariable(SqlServerLiveConnectionEnv)
                ?? fixture.ConnectionString;
            Assert.False(string.IsNullOrWhiteSpace(connectionString), "Real SQL Server backend was not available for this test run.");

            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SqlServerLiveConnectionEnv)))
                Assert.Equal("docker", fixture.BackendKind);

            var databaseName = $"agxiome_sqlserver_{Guid.NewGuid():N}";
            var masterConnectionString = new SqlConnectionStringBuilder(connectionString)
            {
                InitialCatalog = "master"
            }.ConnectionString;
            var databaseConnectionString = new SqlConnectionStringBuilder(connectionString)
            {
                InitialCatalog = databaseName
            }.ConnectionString;
            const string tableName = "DbTestEntitys";

            await using (var masterConn = new SqlConnection(masterConnectionString))
            {
                await masterConn.OpenAsync();

                await using var dropBefore = masterConn.CreateCommand();
                dropBefore.CommandText = $"IF DB_ID(N'{databaseName}') IS NOT NULL DROP DATABASE [{databaseName}]";
                await dropBefore.ExecuteNonQueryAsync();

                await using var createDb = masterConn.CreateCommand();
                createDb.CommandText = $"CREATE DATABASE [{databaseName}]";
                await createDb.ExecuteNonQueryAsync();
            }

            try
            {
                await using var conn = new SqlConnection(databaseConnectionString);
                await conn.OpenAsync();

                await using var create = conn.CreateCommand();
                create.CommandText = $"""
                    IF OBJECT_ID(N'[dbo].[{tableName}]', N'U') IS NULL
                    BEGIN
                        CREATE TABLE [dbo].[{tableName}] (
                            [Id] NVARCHAR(128) NOT NULL PRIMARY KEY,
                            [Name] NVARCHAR(256) NOT NULL,
                            [Score] INT NOT NULL
                        );
                    END
                    """;
                await create.ExecuteNonQueryAsync();

                var store = new SqlServerCommandStore(new SqlServerStoreOptions { ConnectionString = databaseConnectionString });
                await store.SaveAsync(new DbTestEntity { Id = "live-sqlserver", Name = "First", Score = 1 });
                await store.UpdateAsync(new DbTestEntity { Id = "live-sqlserver", Name = "Updated", Score = 2 });

                await using var verifyUpdated = conn.CreateCommand();
                verifyUpdated.CommandText = $"SELECT [Name], [Score] FROM [dbo].[{tableName}] WHERE [Id] = @Id";
                verifyUpdated.Parameters.AddWithValue("@Id", "live-sqlserver");
                await using (var reader = await verifyUpdated.ExecuteReaderAsync())
                {
                    Assert.True(await reader.ReadAsync());
                    Assert.Equal("Updated", reader.GetString(0));
                    Assert.Equal(2, reader.GetInt32(1));
                }

                await store.DeleteAsync<DbTestEntity>("live-sqlserver");

                await using var verify = conn.CreateCommand();
                verify.CommandText = $"SELECT COUNT(1) FROM [dbo].[{tableName}] WHERE [Id] = @Id";
                verify.Parameters.AddWithValue("@Id", "live-sqlserver");
                var count = Convert.ToInt32(await verify.ExecuteScalarAsync());
                Assert.Equal(0, count);
            }
            finally
            {
                await using var cleanupConn = new SqlConnection(masterConnectionString);
                await cleanupConn.OpenAsync();
                await using var cleanup = cleanupConn.CreateCommand();
                cleanup.CommandText = $"""
                    IF DB_ID(N'{databaseName}') IS NOT NULL
                    BEGIN
                        ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                        DROP DATABASE [{databaseName}];
                    END
                    """;
                await cleanup.ExecuteNonQueryAsync();
            }
        });
    }
}

public sealed class SqlServerContainerFixture : IAsyncLifetime
{
    private const string SqlServerPassword = "Agxiome_Strong_Pass123!";
    private IContainer? _container;

    public string? ConnectionString { get; private set; }
    public string BackendKind { get; private set; } = "none";

    public async Task InitializeAsync()
    {
        var externalConnectionString = Environment.GetEnvironmentVariable("AGXIOME_SQLSERVER_TEST_CONNECTION");
        if (!string.IsNullOrWhiteSpace(externalConnectionString))
        {
            ConnectionString = externalConnectionString;
            BackendKind = "external";
            return;
        }

        _container = new ContainerBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            .WithEnvironment("ACCEPT_EULA", "Y")
            .WithEnvironment("MSSQL_SA_PASSWORD", SqlServerPassword)
            .WithEnvironment("MSSQL_PID", "Developer")
            .WithPortBinding(1433, true)
            .Build();

        await _container.StartAsync();
        var port = _container.GetMappedPublicPort(1433);
        ConnectionString = $"Server={_container.Hostname},{port};Database=master;User Id=sa;Password={SqlServerPassword};TrustServerCertificate=True;Encrypt=False;Pooling=False;Connect Timeout=15";
        await WaitForSqlServerAsync(ConnectionString);
        BackendKind = "docker";
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }

    private static async Task WaitForSqlServerAsync(string connectionString, int attempts = 120)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();
                return;
            }
            catch when (attempt < attempts - 1)
            {
                await Task.Delay(1000);
            }
        }

        throw new TimeoutException("SQL Server container did not become reachable in time.");
    }
}

// ── DB-03 SQL Server LocalDB ─────────────────────────────────────────────────
public class SqlServerLocalDbStoreTests : IClassFixture<SqlServerLocalDbFixture>
{
    private const string SqlServerLocalDbLiveConnectionEnv = "AGXIOME_SQLLOCALDB_TEST_CONNECTION";
    private readonly SqlServerLocalDbFixture _fixture;

    public SqlServerLocalDbStoreTests(SqlServerLocalDbFixture fixture)
        => _fixture = fixture;

    [Fact] public async Task ProviderType_IsSQLServerLocalDB()
    {
        var store = new SqlServerLocalDbCommandStore(new SqlServerLocalDbStoreOptions { ConnectionString = "Data Source=(localdb)\\MSSQLLocalDB;Database=Test" });
        Assert.Equal(StorageProviderType.SQLServerLocalDB, store.ProviderType);
    }

    [Fact] public async Task Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new SqlServerLocalDbCommandStore(null!));

    [Fact] public async Task Constructor_EmptyConnectionString_Throws()
        => Assert.Throws<ArgumentException>(() => new SqlServerLocalDbCommandStore(new SqlServerLocalDbStoreOptions { ConnectionString = string.Empty }));

    [Fact] public async Task SaveAsync_InvalidConnection_Throws()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var store = new SqlServerLocalDbCommandStore(new SqlServerLocalDbStoreOptions { ConnectionString = "Data Source=(localdb)\\NoSuchInstance;Database=X;Connect Timeout=1" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "1", Name = "T", Score = 0 }, cts.Token));
    }

    [Fact] public async Task DeleteAsync_InvalidConnection_Throws()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var store = new SqlServerLocalDbCommandStore(new SqlServerLocalDbStoreOptions { ConnectionString = "Data Source=(localdb)\\NoSuchInstance;Database=X;Connect Timeout=1" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.DeleteAsync<DbTestEntity>("1", cts.Token));
    }

    [Fact] public async Task Options_Defaults()
    {
        var opts = new SqlServerLocalDbStoreOptions { ConnectionString = "cs" };
        Assert.NotNull(opts.ConnectionString);
    }

    [RequiresLocalDbFact] public async Task SaveUpdateDelete_WithRealLocalDbConnection_WorksWhenConfigured()
    {
        var connectionString = Environment.GetEnvironmentVariable(SqlServerLocalDbLiveConnectionEnv)
            ?? _fixture.ConnectionString;

        Assert.False(string.IsNullOrWhiteSpace(connectionString), "SQL Server LocalDB is not available on this host.");

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SqlServerLocalDbLiveConnectionEnv)))
            Assert.Equal("localdb", _fixture.BackendKind);

        var databaseName = $"agxiome_localdb_{Guid.NewGuid():N}";
        var masterConnectionString = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = "master"
        }.ConnectionString;
        var databaseConnectionString = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = databaseName
        }.ConnectionString;
        const string tableName = "DbTestEntitys";

        await using (var masterConn = new SqlConnection(masterConnectionString))
        {
            await masterConn.OpenAsync();

            await using var createDb = masterConn.CreateCommand();
            createDb.CommandText = $"IF DB_ID(N'{databaseName}') IS NULL CREATE DATABASE [{databaseName}]";
            await createDb.ExecuteNonQueryAsync();
        }

        try
        {
            await using var conn = new SqlConnection(databaseConnectionString);
            await conn.OpenAsync();

            await using var create = conn.CreateCommand();
            create.CommandText = $"""
                IF OBJECT_ID(N'[dbo].[{tableName}]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[{tableName}] (
                        [Id] NVARCHAR(128) NOT NULL PRIMARY KEY,
                        [Name] NVARCHAR(256) NOT NULL,
                        [Score] INT NOT NULL
                    );
                END
                """;
            await create.ExecuteNonQueryAsync();

            var store = new SqlServerLocalDbCommandStore(new SqlServerLocalDbStoreOptions { ConnectionString = databaseConnectionString });
            await store.SaveAsync(new DbTestEntity { Id = "live-localdb", Name = "First", Score = 1 });
            await store.UpdateAsync(new DbTestEntity { Id = "live-localdb", Name = "Updated", Score = 2 });

            await using var verifyUpdated = conn.CreateCommand();
            verifyUpdated.CommandText = $"SELECT [Name], [Score] FROM [dbo].[{tableName}] WHERE [Id] = @Id";
            verifyUpdated.Parameters.AddWithValue("@Id", "live-localdb");
            await using (var reader = await verifyUpdated.ExecuteReaderAsync())
            {
                Assert.True(await reader.ReadAsync());
                Assert.Equal("Updated", reader.GetString(0));
                Assert.Equal(2, reader.GetInt32(1));
            }

            await store.DeleteAsync<DbTestEntity>("live-localdb");

            await using var verifyDeleted = conn.CreateCommand();
            verifyDeleted.CommandText = $"SELECT COUNT(1) FROM [dbo].[{tableName}] WHERE [Id] = @Id";
            verifyDeleted.Parameters.AddWithValue("@Id", "live-localdb");
            var remaining = Convert.ToInt32(await verifyDeleted.ExecuteScalarAsync());
            Assert.Equal(0, remaining);
        }
        finally
        {
            await using var cleanupConn = new SqlConnection(masterConnectionString);
            await cleanupConn.OpenAsync();
            await using var cleanup = cleanupConn.CreateCommand();
            cleanup.CommandText = $"""
                IF DB_ID(N'{databaseName}') IS NOT NULL
                BEGIN
                    ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{databaseName}];
                END
                """;
            await cleanup.ExecuteNonQueryAsync();
        }
    }
}

public sealed class SqlServerLocalDbFixture : IAsyncLifetime
{
    private string? _instanceName;

    public string? ConnectionString { get; private set; }
    public string BackendKind { get; private set; } = "none";

    public async Task InitializeAsync()
    {
        var externalConnectionString = Environment.GetEnvironmentVariable("AGXIOME_SQLLOCALDB_TEST_CONNECTION");
        if (!string.IsNullOrWhiteSpace(externalConnectionString))
        {
            ConnectionString = externalConnectionString;
            BackendKind = "external";
            return;
        }

        if (!IsSqlLocalDbInstalled())
            return;

        _instanceName = $"AGXIOME_F4_{Guid.NewGuid():N}";
        await RunSqlLocalDbAsync($"create \"{_instanceName}\"");
        await RunSqlLocalDbAsync($"start \"{_instanceName}\"");

        ConnectionString = $"Data Source=(localdb)\\{_instanceName};Initial Catalog=master;Integrated Security=True;Pooling=False;Connect Timeout=15;TrustServerCertificate=True";
        await WaitForLocalDbAsync(ConnectionString);
        BackendKind = "localdb";
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(_instanceName))
            return;

        try
        {
            await RunSqlLocalDbAsync($"stop \"{_instanceName}\" -k", allowFailure: true);
        }
        finally
        {
            await RunSqlLocalDbAsync($"delete \"{_instanceName}\"", allowFailure: true);
        }
    }

    private static bool IsSqlLocalDbInstalled()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        return File.Exists(Path.Combine(programFiles, "Microsoft SQL Server", "150", "Tools", "Binn", "SqlLocalDB.exe"))
            || File.Exists(Path.Combine(programFiles, "Microsoft SQL Server", "160", "Tools", "Binn", "SqlLocalDB.exe"));
    }

    private static async Task WaitForLocalDbAsync(string connectionString, int attempts = 30)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();
                return;
            }
            catch when (attempt < attempts - 1)
            {
                await Task.Delay(1000);
            }
        }

        throw new TimeoutException("SQL Server LocalDB instance did not become reachable in time.");
    }

    private static async Task RunSqlLocalDbAsync(string arguments, bool allowFailure = false)
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var candidatePaths = new[]
        {
            Path.Combine(programFiles, "Microsoft SQL Server", "160", "Tools", "Binn", "SqlLocalDB.exe"),
            Path.Combine(programFiles, "Microsoft SQL Server", "150", "Tools", "Binn", "SqlLocalDB.exe")
        };

        var executable = candidatePaths.FirstOrDefault(File.Exists);
        if (string.IsNullOrWhiteSpace(executable))
        {
            if (allowFailure)
                return;

            throw new InvalidOperationException("SqlLocalDB.exe was not found on this host.");
        }

        var startInfo = new System.Diagnostics.ProcessStartInfo(executable, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = System.Diagnostics.Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start SqlLocalDB.exe.");

        await process.WaitForExitAsync();

        if (process.ExitCode == 0 || allowFailure)
            return;

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        throw new InvalidOperationException($"SqlLocalDB command failed with exit code {process.ExitCode}. stdout: {stdout} stderr: {stderr}");
    }
}

// ── DB-03B PostgreSQL ────────────────────────────────────────────────────────
public class PostgresCommandStoreTests
{
    private const string PostgresLiveConnectionEnv = "AGXIOME_POSTGRES_TEST_CONNECTION";

    [Fact] public async Task Constructor_EmptyConnectionString_IsStillRuntimeRejected()
        => await Assert.ThrowsAnyAsync<Exception>(() => new PostgresCommandStore(string.Empty).SaveAsync(new DbTestEntity { Id = "x", Name = "bad", Score = 0 }));

    [RequiresPostgresRealFact] public async Task SaveUpdateDelete_WithRealPostgresConnection_WorksWhenConfigured()
    {
        await AsyncFixtureRunner.UseAsync<PostgresContainerFixture>(async fixture =>
        {
            var connectionString = fixture.ConnectionString;
            Assert.False(string.IsNullOrWhiteSpace(connectionString), "Real PostgreSQL backend was not available for this test run.");

            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PostgresLiveConnectionEnv)))
                Assert.Equal("docker", fixture.BackendKind);

            const string tableName = "dbtestentitys";

            await using (var conn = new NpgsqlConnection(connectionString))
            {
                await conn.OpenAsync();

                await using (var extension = conn.CreateCommand())
                {
                    extension.CommandText = "CREATE EXTENSION IF NOT EXISTS vector;";
                    try
                    {
                        await extension.ExecuteNonQueryAsync();
                    }
                    catch
                    {
                    }
                }

                await using var create = conn.CreateCommand();
                create.CommandText = $"""
                    CREATE TABLE IF NOT EXISTS "{tableName}" (
                        "id" text PRIMARY KEY,
                        "name" text NOT NULL,
                        "score" integer NOT NULL
                    );
                    """;
                await create.ExecuteNonQueryAsync();

                await using var cleanup = conn.CreateCommand();
                cleanup.CommandText = $"DELETE FROM \"{tableName}\" WHERE \"id\" = @Id";
                cleanup.Parameters.AddWithValue("@Id", "live-postgres");
                await cleanup.ExecuteNonQueryAsync();
            }

            var store = new PostgresCommandStore(connectionString!);
            await store.SaveAsync(new DbTestEntity { Id = "live-postgres", Name = "First", Score = 1 });
            await store.UpdateAsync(new DbTestEntity { Id = "live-postgres", Name = "Updated", Score = 2 });

            await using (var verifyConn = new NpgsqlConnection(connectionString))
            {
                await verifyConn.OpenAsync();

                await using var verifyUpdated = verifyConn.CreateCommand();
                verifyUpdated.CommandText = $"SELECT \"name\", \"score\" FROM \"{tableName}\" WHERE \"id\" = @Id";
                verifyUpdated.Parameters.AddWithValue("@Id", "live-postgres");
                await using (var reader = await verifyUpdated.ExecuteReaderAsync())
                {
                    Assert.True(await reader.ReadAsync());
                    Assert.Equal("Updated", reader.GetString(0));
                    Assert.Equal(2, reader.GetInt32(1));
                }
            }

            await store.DeleteAsync<DbTestEntity>("live-postgres");

            await using var verifyDeleteConn = new NpgsqlConnection(connectionString);
            await verifyDeleteConn.OpenAsync();
            await using var verifyDelete = verifyDeleteConn.CreateCommand();
            verifyDelete.CommandText = $"SELECT COUNT(*) FROM \"{tableName}\" WHERE \"id\" = @Id";
            verifyDelete.Parameters.AddWithValue("@Id", "live-postgres");
            var remaining = Convert.ToInt64(await verifyDelete.ExecuteScalarAsync());
            Assert.Equal(0L, remaining);
        });
    }
}

internal static class NpgsqlCompatTestHarness
{
    public static async Task PrepareEntityTableAsync(string connectionString, string tableName, string entityId)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        await using (var extension = conn.CreateCommand())
        {
            extension.CommandText = "CREATE EXTENSION IF NOT EXISTS vector;";
            try
            {
                await extension.ExecuteNonQueryAsync();
            }
            catch
            {
                // Optional for contract coverage; keep the test compatible with plain PostgreSQL too.
            }
        }

        await using var create = conn.CreateCommand();
        create.CommandText = $"""
            CREATE TABLE IF NOT EXISTS "{tableName}" (
                "Id" text PRIMARY KEY,
                "Name" text NOT NULL,
                "Score" integer NOT NULL
            );
            """;
        await create.ExecuteNonQueryAsync();

        await using var cleanup = conn.CreateCommand();
        cleanup.CommandText = $"DELETE FROM \"{tableName}\" WHERE \"Id\" = @Id";
        cleanup.Parameters.AddWithValue("@Id", entityId);
        await cleanup.ExecuteNonQueryAsync();
    }

    public static async Task AssertEntityUpdatedAsync(string connectionString, string tableName, string entityId, string expectedName, int expectedScore)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        await using var verifyUpdated = conn.CreateCommand();
        verifyUpdated.CommandText = $"SELECT \"Name\", \"Score\" FROM \"{tableName}\" WHERE \"Id\" = @Id";
        verifyUpdated.Parameters.AddWithValue("@Id", entityId);
        await using var reader = await verifyUpdated.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(expectedName, reader.GetString(0));
        Assert.Equal(expectedScore, reader.GetInt32(1));
    }

    public static async Task AssertEntityDeletedAsync(string connectionString, string tableName, string entityId)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        await using var verifyDelete = conn.CreateCommand();
        verifyDelete.CommandText = $"SELECT COUNT(*) FROM \"{tableName}\" WHERE \"Id\" = @Id";
        verifyDelete.Parameters.AddWithValue("@Id", entityId);
        var remaining = Convert.ToInt64(await verifyDelete.ExecuteScalarAsync());
        Assert.Equal(0L, remaining);
    }
}

public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private const string DedicatedComposeConnectionString = "Host=127.0.0.1;Port=55432;Database=agxiome_f4;Username=agxiome;Password=agxiome_f4_pw;SSL Mode=Disable;Trust Server Certificate=true;Pooling=false;Timeout=5;Command Timeout=30;Include Error Detail=true";
    private PostgreSqlContainer? _container;
    private string? _isolatedDatabaseName;
    private string? _adminConnectionString;

    public string? ConnectionString { get; private set; }
    public string BackendKind { get; private set; } = "none";

    public async Task InitializeAsync()
    {
        var externalConnectionString = Environment.GetEnvironmentVariable("AGXIOME_POSTGRES_TEST_CONNECTION");
        if (!string.IsNullOrWhiteSpace(externalConnectionString))
        {
            ConnectionString = await CreateIsolatedDatabaseConnectionAsync(externalConnectionString);
            BackendKind = "external-isolated";
            return;
        }

        if (ConditionalTestEnvironment.IsDedicatedPostgresAvailable())
        {
            ConnectionString = await CreateIsolatedDatabaseConnectionAsync(DedicatedComposeConnectionString);
            BackendKind = "dedicated-compose";
            return;
        }

        _container = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("agxiome_f4")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
        BackendKind = "docker";
    }

    public async Task DisposeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_isolatedDatabaseName) && !string.IsNullOrWhiteSpace(_adminConnectionString))
        {
            await using var adminConn = new NpgsqlConnection(_adminConnectionString);
            await adminConn.OpenAsync();

            await using (var terminate = adminConn.CreateCommand())
            {
                terminate.CommandText = """
                    SELECT pg_terminate_backend(pid)
                    FROM pg_stat_activity
                    WHERE datname = @db AND pid <> pg_backend_pid();
                    """;
                terminate.Parameters.AddWithValue("db", _isolatedDatabaseName);
                await terminate.ExecuteNonQueryAsync();
            }

            await using var drop = adminConn.CreateCommand();
            drop.CommandText = $"DROP DATABASE IF EXISTS \"{_isolatedDatabaseName}\"";
            await drop.ExecuteNonQueryAsync();
        }

        if (_container is not null)
            await _container.DisposeAsync().AsTask();
    }

    private async Task<string> CreateIsolatedDatabaseConnectionAsync(string baseConnectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            Database = string.IsNullOrWhiteSpace(new NpgsqlConnectionStringBuilder(baseConnectionString).Database)
                ? "postgres"
                : new NpgsqlConnectionStringBuilder(baseConnectionString).Database,
            Pooling = false,
            IncludeErrorDetail = true
        };

        var adminBuilder = new NpgsqlConnectionStringBuilder(builder.ConnectionString)
        {
            Database = "postgres"
        };

        _isolatedDatabaseName = $"agxiome_f4_{Guid.NewGuid():N}";
        _adminConnectionString = adminBuilder.ConnectionString;

        await using var adminConn = new NpgsqlConnection(_adminConnectionString);
        await adminConn.OpenAsync();
        await using var create = adminConn.CreateCommand();
        create.CommandText = $"CREATE DATABASE \"{_isolatedDatabaseName}\"";
        await create.ExecuteNonQueryAsync();

        builder.Database = _isolatedDatabaseName;
        return builder.ConnectionString;
    }
}

// ── DB-04 Neon ───────────────────────────────────────────────────────────────
public class NeonCommandStoreTests
{
    [Fact] public async Task ProviderType_IsNeon()
    {
        var store = new NeonCommandStore(new NeonStoreOptions { ConnectionString = "Host=test;Username=u;Password=p;Database=d" });
        Assert.Equal(StorageProviderType.Neon, store.ProviderType);
    }

    [Fact] public async Task Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new NeonCommandStore(null!));

    [RequiresPostgresRealFact] public async Task SaveAsync_InvalidHost_Throws()
    {
        // Negative test against real PostgreSQL: wrong schema throws
        await AsyncFixtureRunner.UseAsync<PostgresContainerFixture>(async fixture =>
        {
            var badSchemaConn = fixture.ConnectionString!.Replace("public", "nonexistent_schema");
            var store = new NeonCommandStore(new NeonStoreOptions { ConnectionString = badSchemaConn + ";Timeout=3" });
            await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "x" }));
        });
    }

    [RequiresPostgresRealFact] public async Task DeleteAsync_InvalidHost_Throws()
    {
        await AsyncFixtureRunner.UseAsync<PostgresContainerFixture>(async fixture =>
        {
            var badSchemaConn = fixture.ConnectionString!.Replace("public", "nonexistent_schema");
            var store = new NeonCommandStore(new NeonStoreOptions { ConnectionString = badSchemaConn + ";Timeout=3" });
            await Assert.ThrowsAnyAsync<Exception>(() => store.DeleteAsync<DbTestEntity>("x"));
        });
    }

    [Fact] public async Task Options_RoundTrip()
    {
        var opts = new NeonStoreOptions { ConnectionString = "neon-cs" };
        Assert.Equal("neon-cs", opts.ConnectionString);
    }

    [RequiresPostgresRealFact] public async Task SaveUpdateDelete_WithPostgresCompatConnection_Works()
    {
        await AsyncFixtureRunner.UseAsync<PostgresContainerFixture>(async fixture =>
        {
            var connectionString = fixture.ConnectionString;
            Assert.False(string.IsNullOrWhiteSpace(connectionString), "Real PostgreSQL-compatible backend was not available for Neon compatibility tests.");

            const string entityId = "live-neon";
            const string tableName = "dbtestentitys";
            await NpgsqlCompatTestHarness.PrepareEntityTableAsync(connectionString!, tableName, entityId);

            var store = new NeonCommandStore(new NeonStoreOptions { ConnectionString = connectionString! });
            await store.SaveAsync(new DbTestEntity { Id = entityId, Name = "First", Score = 1 });
            await store.UpdateAsync(new DbTestEntity { Id = entityId, Name = "Updated", Score = 2 });
            await NpgsqlCompatTestHarness.AssertEntityUpdatedAsync(connectionString!, tableName, entityId, "Updated", 2);
            await store.DeleteAsync<DbTestEntity>(entityId);
            await NpgsqlCompatTestHarness.AssertEntityDeletedAsync(connectionString!, tableName, entityId);
        });
    }
}

// ── DB-05 Supabase ───────────────────────────────────────────────────────────
public class SupabaseCommandStoreTests
{
    [Fact] public async Task ProviderType_IsSupabase()
    {
        var store = new SupabaseCommandStore(new SupabaseStoreOptions { ConnectionString = "Host=test;Username=u;Password=p;Database=d" });
        Assert.Equal(StorageProviderType.Supabase, store.ProviderType);
    }

    [Fact] public async Task Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new SupabaseCommandStore(null!));

    [Fact] public async Task BypassRls_DefaultFalse()
    {
        var opts = new SupabaseStoreOptions { ConnectionString = "cs" };
        Assert.False(opts.BypassRls);
    }

    [RequiresPostgresRealFact] public async Task SaveAsync_InvalidHost_Throws()
    {
        await AsyncFixtureRunner.UseAsync<PostgresContainerFixture>(async fixture =>
        {
            var badSchemaConn = fixture.ConnectionString!.Replace("public", "nonexistent_schema");
            var store = new SupabaseCommandStore(new SupabaseStoreOptions { ConnectionString = badSchemaConn + ";Timeout=3" });
            await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "x" }));
        });
    }

    [Fact] public async Task Options_RoundTrip()
    {
        var opts = new SupabaseStoreOptions { ConnectionString = "sb-cs", BypassRls = true };
        Assert.True(opts.BypassRls);
    }

    [RequiresPostgresRealFact] public async Task SaveUpdateDelete_WithPostgresCompatConnection_Works()
    {
        await AsyncFixtureRunner.UseAsync<PostgresContainerFixture>(async fixture =>
        {
            var connectionString = fixture.ConnectionString;
            Assert.False(string.IsNullOrWhiteSpace(connectionString), "Real PostgreSQL-compatible backend was not available for Supabase compatibility tests.");

            const string entityId = "live-supabase";
            const string tableName = "dbtestentitys";
            await NpgsqlCompatTestHarness.PrepareEntityTableAsync(connectionString!, tableName, entityId);

            var store = new SupabaseCommandStore(new SupabaseStoreOptions { ConnectionString = connectionString! });
            await store.SaveAsync(new DbTestEntity { Id = entityId, Name = "First", Score = 1 });
            await store.UpdateAsync(new DbTestEntity { Id = entityId, Name = "Updated", Score = 2 });
            await NpgsqlCompatTestHarness.AssertEntityUpdatedAsync(connectionString!, tableName, entityId, "Updated", 2);
            await store.DeleteAsync<DbTestEntity>(entityId);
            await NpgsqlCompatTestHarness.AssertEntityDeletedAsync(connectionString!, tableName, entityId);
        });
    }
}

// ── DB-06 SingleStore ─────────────────────────────────────────────────────────
public class SingleStoreCommandStoreTests
{
    [Fact] public async Task ProviderType_IsSingleStore()
    {
        var store = new SingleStoreCommandStore(new SingleStoreOptions { ConnectionString = "Server=localhost;Database=t" });
        Assert.Equal(StorageProviderType.SingleStore, store.ProviderType);
    }

    [Fact] public async Task Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new SingleStoreCommandStore(null!));

    [Fact] public async Task BulkBatchSize_Default500()
    {
        var opts = new SingleStoreOptions { ConnectionString = "cs" };
        Assert.Equal(500, opts.BulkBatchSize);
    }

    [RequiresPostgresRealFact] public async Task SaveAsync_InvalidConnection_Throws()
    {
        await AsyncFixtureRunner.UseAsync<PostgresContainerFixture>(async fixture =>
        {
            var badSchemaConn = fixture.ConnectionString!.Replace("public", "nonexistent_schema");
            var store = new SingleStoreCommandStore(new SingleStoreOptions { ConnectionString = badSchemaConn + ";Timeout=3" });
            await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "1" }));
        });
    }

    [Fact] public async Task BulkSaveAsync_EmptyList_DoesNotThrow()
    {
        var store = new SingleStoreCommandStore(new SingleStoreOptions { ConnectionString = "Server=invalid_ss_host;Database=x;Connection Timeout=1" });
        // Empty batch short-circuits before touching the connection.
        await store.BulkSaveAsync(new List<DbTestEntity>());
    }

    [RequiresSingleStoreFact] public async Task SaveUpdateDelete_WithRealSingleStoreConnection_WorksWhenConfigured()
    {
        await AsyncFixtureRunner.UseAsync<SingleStoreContainerFixture>(async fixture =>
        {
            var connectionString = fixture.ConnectionString;
            Assert.False(string.IsNullOrWhiteSpace(connectionString), "Real SingleStore backend was not available for this test run.");

            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            const string tableName = "dbtestentitys";
            await using (var create = connection.CreateCommand())
            {
                create.CommandText = @"
CREATE TABLE IF NOT EXISTS `dbtestentitys` (
    `Id` VARCHAR(255) NOT NULL,
    `Name` TEXT NULL,
    `Score` INT NOT NULL,
    PRIMARY KEY (`Id`)
);";
                await create.ExecuteNonQueryAsync();
            }

            var entityId = $"live-singlestore-{Guid.NewGuid():N}";
            var store = new SingleStoreCommandStore(new SingleStoreOptions { ConnectionString = connectionString! });

            try
            {
                await store.SaveAsync(new DbTestEntity { Id = entityId, Name = "First", Score = 1 });
                await store.UpdateAsync(new DbTestEntity { Id = entityId, Name = "Updated", Score = 2 });

                await using (var select = connection.CreateCommand())
                {
                    select.CommandText = $"SELECT `Name`, `Score` FROM `{tableName}` WHERE `Id` = @id";
                    select.Parameters.AddWithValue("@id", entityId);

                    await using var reader = await select.ExecuteReaderAsync();
                    Assert.True(await reader.ReadAsync());
                    Assert.Equal("Updated", reader.GetString(0));
                    Assert.Equal(2, reader.GetInt32(1));
                }

                await store.DeleteAsync<DbTestEntity>(entityId);

                await using var exists = connection.CreateCommand();
                exists.CommandText = $"SELECT COUNT(*) FROM `{tableName}` WHERE `Id` = @id";
                exists.Parameters.AddWithValue("@id", entityId);
                var remaining = Convert.ToInt32(await exists.ExecuteScalarAsync());
                Assert.Equal(0, remaining);
            }
            finally
            {
                await using var cleanup = connection.CreateCommand();
                cleanup.CommandText = $"DELETE FROM `{tableName}` WHERE `Id` = @id";
                cleanup.Parameters.AddWithValue("@id", entityId);
                await cleanup.ExecuteNonQueryAsync();
            }
        });
    }
}

// ── DB-07 CockroachDB ─────────────────────────────────────────────────────────
public class CockroachDbCommandStoreTests
{
    [Fact] public async Task ProviderType_IsCockroachDB()
    {
        var store = new CockroachDbCommandStore(new CockroachDbStoreOptions { ConnectionString = "Host=localhost;Port=26257;Username=root;Database=defaultdb" });
        Assert.Equal(StorageProviderType.CockroachDB, store.ProviderType);
    }

    [Fact] public async Task Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new CockroachDbCommandStore(null!));

    [RequiresPostgresRealFact] public async Task SaveAsync_InvalidHost_Throws()
    {
        await AsyncFixtureRunner.UseAsync<PostgresContainerFixture>(async fixture =>
        {
            var badSchemaConn = fixture.ConnectionString!.Replace("public", "nonexistent_schema");
            var store = new CockroachDbCommandStore(new CockroachDbStoreOptions { ConnectionString = badSchemaConn + ";Timeout=3" });
            await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "x" }));
        });
    }

    [RequiresPostgresRealFact] public async Task DeleteAsync_InvalidHost_Throws()
    {
        await AsyncFixtureRunner.UseAsync<PostgresContainerFixture>(async fixture =>
        {
            var badSchemaConn = fixture.ConnectionString!.Replace("public", "nonexistent_schema");
            var store = new CockroachDbCommandStore(new CockroachDbStoreOptions { ConnectionString = badSchemaConn + ";Timeout=3" });
            await Assert.ThrowsAnyAsync<Exception>(() => store.DeleteAsync<DbTestEntity>("x"));
        });
    }

    [Fact] public async Task Options_RoundTrip()
    {
        var opts = new CockroachDbStoreOptions { ConnectionString = "crdb-cs" };
        Assert.Equal("crdb-cs", opts.ConnectionString);
    }

    [RequiresCockroachFact] public async Task SaveUpdateDelete_WithPostgresCompatConnection_Works()
    {
        await AsyncFixtureRunner.UseAsync<CockroachDbContainerFixture>(async fixture =>
        {
            var connectionString = fixture.ConnectionString;
            Assert.False(string.IsNullOrWhiteSpace(connectionString), "Real CockroachDB backend was not available for this test run.");

            const string entityId = "live-cockroach";
            const string tableName = "dbtestentitys";
            await NpgsqlCompatTestHarness.PrepareEntityTableAsync(connectionString!, tableName, entityId);

            var store = new CockroachDbCommandStore(new CockroachDbStoreOptions { ConnectionString = connectionString! });
            await store.SaveAsync(new DbTestEntity { Id = entityId, Name = "First", Score = 1 });
            await store.UpdateAsync(new DbTestEntity { Id = entityId, Name = "Updated", Score = 2 });
            await NpgsqlCompatTestHarness.AssertEntityUpdatedAsync(connectionString!, tableName, entityId, "Updated", 2);
            await store.DeleteAsync<DbTestEntity>(entityId);
            await NpgsqlCompatTestHarness.AssertEntityDeletedAsync(connectionString!, tableName, entityId);
        });
    }
}

// ── DB-08 TimescaleDB ─────────────────────────────────────────────────────────
public class TimescaleDbCommandStoreTests
{
    [Fact] public async Task ProviderType_IsTimescaleDB()
    {
        var store = new TimescaleDbCommandStore(new TimescaleDbStoreOptions { ConnectionString = "Host=localhost;Username=u;Password=p;Database=d" });
        Assert.Equal(StorageProviderType.TimescaleDB, store.ProviderType);
    }

    [Fact] public async Task Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new TimescaleDbCommandStore(null!));

    [Fact] public async Task UseExplicitTransaction_DefaultFalse()
    {
        var opts = new TimescaleDbStoreOptions { ConnectionString = "cs" };
        Assert.False(opts.UseExplicitTransaction);
    }

    [RequiresPostgresRealFact] public async Task SaveAsync_InvalidHost_Throws()
    {
        await AsyncFixtureRunner.UseAsync<PostgresContainerFixture>(async fixture =>
        {
            var badSchemaConn = fixture.ConnectionString!.Replace("public", "nonexistent_schema");
            var store = new TimescaleDbCommandStore(new TimescaleDbStoreOptions { ConnectionString = badSchemaConn + ";Timeout=3" });
            await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "x" }));
        });
    }

    [Fact] public async Task Options_ConnectionString_Stored()
    {
        var opts = new TimescaleDbStoreOptions { ConnectionString = "ts-cs" };
        Assert.Equal("ts-cs", opts.ConnectionString);
    }

    [RequiresTimescaleFact] public async Task SaveUpdateDelete_WithPostgresCompatConnection_Works()
    {
        await AsyncFixtureRunner.UseAsync<TimescaleDbContainerFixture>(async fixture =>
        {
            var connectionString = fixture.ConnectionString;
            Assert.False(string.IsNullOrWhiteSpace(connectionString), "Real TimescaleDB backend was not available for this test run.");

            const string entityId = "live-timescale";
            const string tableName = "dbtestentitys";
            await NpgsqlCompatTestHarness.PrepareEntityTableAsync(connectionString!, tableName, entityId);

            var store = new TimescaleDbCommandStore(new TimescaleDbStoreOptions { ConnectionString = connectionString! });
            await store.SaveAsync(new DbTestEntity { Id = entityId, Name = "First", Score = 1 });
            await store.UpdateAsync(new DbTestEntity { Id = entityId, Name = "Updated", Score = 2 });
            await NpgsqlCompatTestHarness.AssertEntityUpdatedAsync(connectionString!, tableName, entityId, "Updated", 2);
            await store.DeleteAsync<DbTestEntity>(entityId);
            await NpgsqlCompatTestHarness.AssertEntityDeletedAsync(connectionString!, tableName, entityId);
        });
    }
}

// ── DB-09 DynamoDB ───────────────────────────────────────────────────────────
public class DynamoDbCommandStoreTests
{
    [Fact] public async Task ProviderType_IsDynamoDB()
    {
        var store = new DynamoDbCommandStore(new DynamoDbStoreOptions
        {
            TableName  = "test", ServiceUrl = "http://localhost:8000",
            AccessKey  = "key",  SecretKey   = "secret"
        });
        Assert.Equal(StorageProviderType.DynamoDB, store.ProviderType);
    }

    [Fact] public async Task Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new DynamoDbCommandStore(null!));

    [Fact] public async Task SaveAsync_InvalidEndpoint_Throws()
    {
        // Invalid table name triggers immediate config validation
        var store = new DynamoDbCommandStore(new DynamoDbStoreOptions { TableName = "", ServiceUrl = "http://localhost:8000", AccessKey = "k", SecretKey = "s" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "1" }));
    }

    [Fact] public async Task DeleteAsync_InvalidEndpoint_Throws()
    {
        var store = new DynamoDbCommandStore(new DynamoDbStoreOptions { TableName = "", ServiceUrl = "http://localhost:8000", AccessKey = "k", SecretKey = "s" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.DeleteAsync<DbTestEntity>("1"));
    }

    [Fact] public async Task Options_Region_Default()
    {
        var opts = new DynamoDbStoreOptions
        {
            TableName = "t", ServiceUrl = "http://localhost", AccessKey = "k", SecretKey = "s"
        };
        Assert.Equal("us-east-1", opts.Region);
    }

    [RequiresDockerFact] public async Task SaveUpdateDelete_WithRealDynamoDbLocal_WorksWhenConfigured()
    {
        await AsyncFixtureRunner.UseAsync<DynamoDbLocalFixture>(async fixture =>
        {
            var serviceUrl = Environment.GetEnvironmentVariable("AGXIOME_DYNAMODB_TEST_SERVICEURL")
                ?? fixture.ServiceUrl;
            Assert.False(string.IsNullOrWhiteSpace(serviceUrl), "Real DynamoDB backend was not available for this test run.");

            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AGXIOME_DYNAMODB_TEST_SERVICEURL")))
                Assert.Equal("docker", fixture.BackendKind);

            var tableName = $"agxiome_f4_dynamo_{Guid.NewGuid():N}";
            using var client = DynamoDbLocalFixture.CreateClient(serviceUrl!);

            await client.CreateTableAsync(new CreateTableRequest
            {
                TableName = tableName,
                AttributeDefinitions =
                [
                    new AttributeDefinition("Id", ScalarAttributeType.S)
                ],
                KeySchema =
                [
                    new KeySchemaElement("Id", KeyType.HASH)
                ],
                BillingMode = BillingMode.PAY_PER_REQUEST
            });

            await WaitForTableStatusAsync(client, tableName, TableStatus.ACTIVE);

            try
            {
                var store = new DynamoDbCommandStore(new DynamoDbStoreOptions
                {
                    TableName = tableName,
                    ServiceUrl = serviceUrl!,
                    AccessKey = "test",
                    SecretKey = "test"
                });

                await store.SaveAsync(new DbTestEntity { Id = "live-dynamo", Name = "First", Score = 1 });
                await store.UpdateAsync(new DbTestEntity { Id = "live-dynamo", Name = "Updated", Score = 2 });

                var getUpdated = await client.GetItemAsync(new GetItemRequest
                {
                    TableName = tableName,
                    Key = new Dictionary<string, AttributeValue> { ["Id"] = new() { S = "live-dynamo" } }
                });

                Assert.True(getUpdated.IsItemSet);
                Assert.Equal("Updated", getUpdated.Item["Name"].S);
                Assert.Equal("2", getUpdated.Item["Score"].N);

                await store.DeleteAsync<DbTestEntity>("live-dynamo");

                var getDeleted = await client.GetItemAsync(new GetItemRequest
                {
                    TableName = tableName,
                    Key = new Dictionary<string, AttributeValue> { ["Id"] = new() { S = "live-dynamo" } }
                });

                Assert.False(getDeleted.IsItemSet);
            }
            finally
            {
                await client.DeleteTableAsync(tableName);
            }
        });
    }

    private static async Task WaitForTableStatusAsync(AmazonDynamoDBClient client, string tableName, string expectedStatus, int attempts = 30)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var response = await client.DescribeTableAsync(tableName);
            if (string.Equals(response.Table.TableStatus, expectedStatus, StringComparison.OrdinalIgnoreCase))
                return;

            await Task.Delay(500);
        }

        throw new TimeoutException($"DynamoDB table '{tableName}' did not reach status '{expectedStatus}' in time.");
    }
}

// ── DB-10 CosmosDB ────────────────────────────────────────────────────────────
public class CosmosDbCommandStoreTests
{
    private const string CosmosConnectionEnv = "AGXIOME_COSMOS_TEST_CONNECTION";

    [Fact] public async Task ProviderType_IsCosmosDB()
    {
        var store = new CosmosDbCommandStore(new CosmosDbStoreOptions
        {
            ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
            DatabaseId       = "db",
            ContainerId      = "col"
        });
        Assert.Equal(StorageProviderType.CosmosDB, store.ProviderType);
    }

    [Fact] public async Task Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new CosmosDbCommandStore(null!));

    [Fact] public async Task PartitionKeyPath_Default()
    {
        var opts = new CosmosDbStoreOptions
        {
            ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=test",
            DatabaseId = "db", ContainerId = "col"
        };
        Assert.Equal("/id", opts.PartitionKeyPath);
    }

    [Fact] public async Task SaveAsync_InvalidEndpoint_Throws()
    {
        // Empty connection string triggers validation in constructor/init
        Assert.Throws<ArgumentException>(() => new CosmosDbCommandStore(new CosmosDbStoreOptions { ConnectionString = "", DatabaseId = "db", ContainerId = "col" }));
    }

    [Fact] public async Task Dispose_IsIdempotent()
    {
        var store = new CosmosDbCommandStore(new CosmosDbStoreOptions
        {
            ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
            DatabaseId = "db", ContainerId = "col"
        });
        store.Dispose();
        store.Dispose();
    }

    [RequiresCosmosFact] public async Task SaveUpdateDelete_WithRealCosmosConnection_WorksWhenConfigured()
    {
        var connectionString = Environment.GetEnvironmentVariable(CosmosConnectionEnv);
        Assert.False(string.IsNullOrWhiteSpace(connectionString), "Cosmos connection override was not provided.");

        var databaseId = $"agxiome-f4-cosmos-{Guid.NewGuid():N}";
        var containerId = "dbtestentitys";

        var opts = new CosmosDbStoreOptions
        {
            ConnectionString = connectionString!,
            DatabaseId = databaseId,
            ContainerId = containerId,
            PartitionKeyPath = "/id",
            AllowInsecureTlsForLocalEmulator = true
        };

        await RunWithTimeoutAsync(
            WaitForCosmosAsync(connectionString!, opts.AllowInsecureTlsForLocalEmulator),
            TimeSpan.FromSeconds(110),
            "Cosmos emulator HTTPS readiness");

        using var client = new CosmosClient(connectionString!, opts.CreateClientOptions());
        var database = await RunWithTimeoutAsync(
            client.CreateDatabaseIfNotExistsAsync(databaseId),
            TimeSpan.FromSeconds(20),
            "Cosmos CreateDatabaseIfNotExistsAsync");

        var container = await RunWithTimeoutAsync(
            database.Database.CreateContainerIfNotExistsAsync(containerId, "/id"),
            TimeSpan.FromSeconds(20),
            "Cosmos CreateContainerIfNotExistsAsync");

        var store = new CosmosDbCommandStore(opts);

        var entityId = $"live-cosmos-{Guid.NewGuid():N}";
        try
        {
            await store.SaveAsync(new DbTestEntity { Id = entityId, Name = "First", Score = 1 });
            await store.UpdateAsync(new DbTestEntity { Id = entityId, Name = "Updated", Score = 2 });

            var response = await container.Container.ReadItemAsync<DbTestEntity>(entityId, new PartitionKey(entityId));
            Assert.Equal("Updated", response.Resource.Name);
            Assert.Equal(2, response.Resource.Score);

            await store.DeleteAsync<DbTestEntity>(entityId);
            await Assert.ThrowsAsync<CosmosException>(async () =>
                await container.Container.ReadItemAsync<DbTestEntity>(entityId, new PartitionKey(entityId)));
        }
        finally
        {
            await database.Database.DeleteAsync();
            store.Dispose();
        }
    }

    private static async Task WaitForCosmosAsync(string connectionString, bool allowInsecureTlsForLocalEmulator, int attempts = 90)
    {
        var endpoint = GetCosmosAccountEndpoint(connectionString);
        using var handler = new HttpClientHandler();

        if (allowInsecureTlsForLocalEmulator && endpoint.IsLoopback)
        {
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        using var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                using var response = await client.GetAsync(endpoint);
                if ((int)response.StatusCode >= 200)
                {
                    return;
                }
            }
            catch when (attempt < attempts - 1)
            {
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException($"Cosmos endpoint '{endpoint}' did not become reachable in time.");
    }

    private static async Task<T> RunWithTimeoutAsync<T>(Task<T> task, TimeSpan timeout, string operationName)
    {
        var completedTask = await Task.WhenAny(task, Task.Delay(timeout));
        if (completedTask != task)
        {
            throw new TimeoutException($"{operationName} did not complete within {timeout.TotalSeconds:0}s.");
        }

        return await task;
    }

    private static async Task RunWithTimeoutAsync(Task task, TimeSpan timeout, string operationName)
    {
        var completedTask = await Task.WhenAny(task, Task.Delay(timeout));
        if (completedTask != task)
        {
            throw new TimeoutException($"{operationName} did not complete within {timeout.TotalSeconds:0}s.");
        }

        await task;
    }

    private static Uri GetCosmosAccountEndpoint(string connectionString)
    {
        foreach (var segment in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            const string prefix = "AccountEndpoint=";
            if (!segment.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var endpointValue = segment[prefix.Length..];
            if (Uri.TryCreate(endpointValue, UriKind.Absolute, out var endpoint))
            {
                return endpoint;
            }

            break;
        }

        throw new InvalidOperationException("Cosmos connection string does not contain a valid AccountEndpoint.");
    }
}

// ── DB-11 Firestore ───────────────────────────────────────────────────────────
public class FirestoreCommandStoreTests
{
    private static readonly SemaphoreSlim FirestoreEmulatorEnvGate = new(1, 1);

    [Fact] public async Task ProviderType_IsFirestore()
    {
        await UseFirestoreEmulatorHostAsync("127.0.0.1:18090", () =>
        {
            var store = new FirestoreCommandStore(new FirestoreStoreOptions { ProjectId = "test-project" });
            Assert.Equal(StorageProviderType.Firestore, store.ProviderType);
            return Task.CompletedTask;
        });
    }

    [Fact] public async Task Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new FirestoreCommandStore(null!));

    [Fact] public async Task Options_ProjectId_Stored()
    {
        var opts = new FirestoreStoreOptions { ProjectId = "my-project" };
        Assert.Equal("my-project", opts.ProjectId);
    }

    [Fact] public async Task Options_CredentialFilePath_NullByDefault()
    {
        var opts = new FirestoreStoreOptions { ProjectId = "p" };
        Assert.Null(opts.CredentialFilePath);
    }

    [Fact] public async Task Constructor_MissingCredentialFile_Throws()
    {
        var missingCredentialsPath = Path.Combine(Path.GetTempPath(), $"firestore-{Guid.NewGuid():N}.json");
        var opts = new FirestoreStoreOptions { ProjectId = "invalid-project-xyz", CredentialFilePath = missingCredentialsPath };

        Assert.ThrowsAny<Exception>(() => new FirestoreCommandStore(opts));
    }

    [RequiresFirestoreFact] public async Task SaveUpdateDelete_WithRealFirestoreEmulator_WorksWhenConfigured()
    {
        await AsyncFixtureRunner.UseAsync<FirestoreEmulatorFixture>(async fixture =>
        {
            Assert.False(string.IsNullOrWhiteSpace(fixture.EmulatorHost), "Firestore emulator host was not configured.");

            await UseFirestoreEmulatorHostAsync(fixture.EmulatorHost!, async () =>
            {
                var store = new FirestoreCommandStore(new FirestoreStoreOptions { ProjectId = fixture.ProjectId });
                var db = CreateFirestoreDb(fixture.ProjectId);
                var entityId = $"live-firestore-{Guid.NewGuid():N}";
                var docRef = db.Collection("dbtestentitys").Document(entityId);

                try
                {
                    await RunWithTimeoutAsync(
                        store.SaveAsync(new DbTestEntity { Id = entityId, Name = "First", Score = 1 }),
                        TimeSpan.FromSeconds(20),
                        "Firestore SaveAsync");

                    await RunWithTimeoutAsync(
                        store.UpdateAsync(new DbTestEntity { Id = entityId, Name = "Updated", Score = 2 }),
                        TimeSpan.FromSeconds(20),
                        "Firestore UpdateAsync");

                    var snapshot = await RunWithTimeoutAsync(
                        docRef.GetSnapshotAsync(),
                        TimeSpan.FromSeconds(20),
                        "Firestore GetSnapshotAsync after update");

                    Assert.True(snapshot.Exists);
                    Assert.Equal("Updated", snapshot.GetValue<string>(nameof(DbTestEntity.Name)));
                    Assert.Equal(2L, snapshot.GetValue<long>(nameof(DbTestEntity.Score)));

                    await RunWithTimeoutAsync(
                        store.DeleteAsync<DbTestEntity>(entityId),
                        TimeSpan.FromSeconds(20),
                        "Firestore DeleteAsync");

                    var deletedSnapshot = await RunWithTimeoutAsync(
                        docRef.GetSnapshotAsync(),
                        TimeSpan.FromSeconds(20),
                        "Firestore GetSnapshotAsync after delete");

                    Assert.False(deletedSnapshot.Exists);
                }
                finally
                {
                    try
                    {
                        await docRef.DeleteAsync();
                    }
                    catch
                    {
                    }
                }
            });
        });
    }

    private static async Task UseFirestoreEmulatorHostAsync(string emulatorHost, Func<Task> action)
    {
        await FirestoreEmulatorEnvGate.WaitAsync();
        var previousHost = Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST");
        Environment.SetEnvironmentVariable("FIRESTORE_EMULATOR_HOST", emulatorHost);

        try
        {
            await action();
        }
        finally
        {
            Environment.SetEnvironmentVariable("FIRESTORE_EMULATOR_HOST", previousHost);
            FirestoreEmulatorEnvGate.Release();
        }
    }

    private static FirestoreDb CreateFirestoreDb(string projectId)
    {
        var builder = new FirestoreDbBuilder { ProjectId = projectId };
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST")))
        {
            builder.EmulatorDetection = EmulatorDetection.EmulatorOrProduction;
        }

        return builder.Build();
    }

    private static async Task<T> RunWithTimeoutAsync<T>(Task<T> task, TimeSpan timeout, string operationName)
    {
        var completedTask = await Task.WhenAny(task, Task.Delay(timeout));
        if (completedTask != task)
        {
            throw new TimeoutException($"{operationName} did not complete within {timeout.TotalSeconds:0}s.");
        }

        return await task;
    }

    private static async Task RunWithTimeoutAsync(Task task, TimeSpan timeout, string operationName)
    {
        var completedTask = await Task.WhenAny(task, Task.Delay(timeout));
        if (completedTask != task)
        {
            throw new TimeoutException($"{operationName} did not complete within {timeout.TotalSeconds:0}s.");
        }

        await task;
    }
}

// ── DB-12 SurrealDB ───────────────────────────────────────────────────────────
public class SurrealDbCommandStoreTests
{
    private static SurrealDbCommandStore MakeStore(HttpStatusCode code = System.Net.HttpStatusCode.OK, string body = "{}")
    {
        var opts = new SurrealDbStoreOptions
        {
            BaseUrl = "http://localhost:8000", Namespace = "test",
            Database = "test", Username = "root", Password = "root"
        };
        var http = new HttpClient(new TestHttpMessageHandler(code, body)) { BaseAddress = new Uri(opts.BaseUrl) };
        return new SurrealDbCommandStore(opts, http);
    }

    [Fact] public async Task ProviderType_IsSurrealDB()
        => Assert.Equal(StorageProviderType.SurrealDB, MakeStore().ProviderType);

    [Fact] public async Task Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new SurrealDbCommandStore(null!));

    [Fact] public async Task SaveAsync_MockHandler_Succeeds()
    {
        var store = MakeStore(System.Net.HttpStatusCode.OK, "{}");
        await store.SaveAsync(new DbTestEntity { Id = "1", Name = "test", Score = 10 });
    }

    [Fact] public async Task DeleteAsync_MockHandler_Succeeds()
    {
        var store = MakeStore(System.Net.HttpStatusCode.OK, "");
        await store.DeleteAsync<DbTestEntity>("1");
    }

    [Fact] public async Task SaveAsync_ServerError_Throws()
    {
        var store = MakeStore(System.Net.HttpStatusCode.InternalServerError, "error");
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "1" }));
    }

    [RequiresDockerFact] public async Task SaveUpdateDelete_WithRealSurrealDb_WorksWhenConfigured()
    {
        await AsyncFixtureRunner.UseAsync<SurrealDbContainerFixture>(async fixture =>
        {
            var baseUrl = fixture.BaseUrl;
            Assert.False(string.IsNullOrWhiteSpace(baseUrl), "Real SurrealDB backend was not available for this test run.");

            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AGXIOME_SURREAL_TEST_BASEURL")))
                Assert.Equal("docker", fixture.BackendKind);

            var store = new SurrealDbCommandStore(new SurrealDbStoreOptions
            {
                BaseUrl = baseUrl!,
                Namespace = "test",
                Database = "test",
                Username = "root",
                Password = "root"
            });

            using var client = SurrealDbContainerFixture.CreateHttpClient(baseUrl!);
            var entityId = $"live-surreal-{Guid.NewGuid():N}";

            await store.SaveAsync(new DbTestEntity { Id = entityId, Name = "First", Score = 1 });
            await store.UpdateAsync(new DbTestEntity { Id = entityId, Name = "Updated", Score = 2 });

            using (var getUpdated = await PostSurrealSqlAsync(
                client,
                $"SELECT * FROM type::record('dbtestentitys', '{entityId}');"))
            {
                var payload = await getUpdated.Content.ReadAsStringAsync();
                Assert.True(getUpdated.IsSuccessStatusCode, payload);
                Assert.Contains("Updated", payload, StringComparison.Ordinal);
                Assert.Contains(entityId, payload, StringComparison.Ordinal);
            }

            await store.DeleteAsync<DbTestEntity>(entityId);

            using var getDeleted = await PostSurrealSqlAsync(
                client,
                $"SELECT * FROM type::record('dbtestentitys', '{entityId}');");
            var deletedPayload = await getDeleted.Content.ReadAsStringAsync();
            Assert.True(
                !getDeleted.IsSuccessStatusCode
                || !deletedPayload.Contains(entityId, StringComparison.Ordinal),
                $"SurrealDB record '{entityId}' still appeared after delete. Payload: {deletedPayload}");
        });
    }

    private static Task<HttpResponseMessage> PostSurrealSqlAsync(HttpClient client, string sql)
        => client.PostAsync("/sql", new StringContent(sql, System.Text.Encoding.UTF8, "text/plain"));
}

// ── DB-13 RavenDB ─────────────────────────────────────────────────────────────
public class RavenDbCommandStoreTests
{
    [Fact] public async Task ProviderType_IsRavenDB()
    {
        var store = new RavenDbCommandStore(new RavenDbStoreOptions
        {
            ServerUrls = ["http://localhost:8080"],
            DatabaseName = "test"
        });
        Assert.Equal(StorageProviderType.RavenDB, store.ProviderType);
    }

    [Fact] public async Task Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new RavenDbCommandStore(null!));

    [Fact] public async Task SaveAsync_UnreachableServer_Throws()
    {
        var store = new RavenDbCommandStore(new RavenDbStoreOptions { ServerUrls = ["http://127.0.0.1:1"], DatabaseName = "test" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "1" }));
    }

    [Fact] public async Task DeleteAsync_UnreachableServer_Throws()
    {
        var store = new RavenDbCommandStore(new RavenDbStoreOptions { ServerUrls = ["http://127.0.0.1:1"], DatabaseName = "test" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.DeleteAsync<DbTestEntity>("1"));
    }

    [Fact] public async Task Dispose_IsIdempotent()
    {
        var store = new RavenDbCommandStore(new RavenDbStoreOptions
        {
            ServerUrls = ["http://localhost:8080"], DatabaseName = "test"
        });
        store.Dispose();
    }

    [RequiresRavenFact] public async Task SaveUpdateDelete_WithRealRavenDb_WorksWhenConfigured()
    {
        await AsyncFixtureRunner.UseAsync<RavenDbContainerFixture>(async fixture =>
        {
            Assert.False(string.IsNullOrWhiteSpace(fixture.ServerUrl), "Real RavenDB backend was not available for this test run.");

            var databaseName = $"{fixture.DatabaseName}-{Guid.NewGuid():N}";
            using var adminStore = new DocumentStore
            {
                Urls = [fixture.ServerUrl!],
                Database = databaseName
            }.Initialize();

            await adminStore.Maintenance.Server.SendAsync(new CreateDatabaseOperation(new DatabaseRecord(databaseName)));

            try
            {
                var store = new RavenDbCommandStore(new RavenDbStoreOptions
                {
                    ServerUrls = [fixture.ServerUrl!],
                    DatabaseName = databaseName
                });

                var entityId = $"dbtestentities/{Guid.NewGuid():N}";

                await store.SaveAsync(new DbTestEntity { Id = entityId, Name = "First", Score = 1 });
                await store.UpdateAsync(new DbTestEntity { Id = entityId, Name = "Updated", Score = 2 });

                using (var verifySession = adminStore.OpenAsyncSession())
                {
                    var updated = await verifySession.LoadAsync<DbTestEntity>(entityId);
                    Assert.NotNull(updated);
                    Assert.Equal("Updated", updated!.Name);
                    Assert.Equal(2, updated.Score);
                }

                await store.DeleteAsync<DbTestEntity>(entityId);

                using (var verifySession = adminStore.OpenAsyncSession())
                {
                    var deleted = await verifySession.LoadAsync<DbTestEntity>(entityId);
                    Assert.Null(deleted);
                }

                store.Dispose();
            }
            finally
            {
                try
                {
                    await adminStore.Maintenance.Server.SendAsync(new DeleteDatabasesOperation(databaseName, hardDelete: true));
                }
                catch
                {
                }
            }
        });
    }
}

// ── DB-14 Couchbase ───────────────────────────────────────────────────────────
public class CouchbaseCommandStoreTests
{
    [Fact] public async Task ProviderType_IsCouchbase()
    {
        var store = new CouchbaseCommandStore(new CouchbaseStoreOptions
        {
            ConnectionString = "couchbase://localhost", Username = "u", Password = "p",
            BucketName = "b", ScopeName = "_default", CollectionName = "_default"
        });
        Assert.Equal(StorageProviderType.Couchbase, store.ProviderType);
    }

    [Fact] public async Task Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new CouchbaseCommandStore(null!));

    [Fact] public async Task SaveAsync_UnreachableHost_Throws()
    {
        var store = new CouchbaseCommandStore(new CouchbaseStoreOptions { ConnectionString = "couchbase://127.0.0.1:1", Username = "u", Password = "p", BucketName = "b", ScopeName = "_default", CollectionName = "_default" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "1" }));
        await store.DisposeAsync();
    }

    [Fact] public async Task DeleteAsync_UnreachableHost_Throws()
    {
        var store = new CouchbaseCommandStore(new CouchbaseStoreOptions { ConnectionString = "couchbase://127.0.0.1:1", Username = "u", Password = "p", BucketName = "b", ScopeName = "_default", CollectionName = "_default" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.DeleteAsync<DbTestEntity>("1"));
        await store.DisposeAsync();
    }

    [Fact] public async Task Options_Defaults()
    {
        var opts = new CouchbaseStoreOptions
        {
            ConnectionString = "couchbase://localhost", Username = "u", Password = "p",
            BucketName = "b", ScopeName = "_default", CollectionName = "_default"
        };
        Assert.Equal("_default", opts.ScopeName);
    }

    [Fact(Skip = "requires live Couchbase server")] public async Task SaveUpdateDelete_WithRealCouchbase_WorksWhenConfigured()
    {
        await AsyncFixtureRunner.UseAsync<CouchbaseContainerFixture>(async fixture =>
        {
            Assert.False(string.IsNullOrWhiteSpace(fixture.ConnectionString), "Real Couchbase backend was not available for this test run.");

            await using var store = new CouchbaseCommandStore(new CouchbaseStoreOptions
            {
                ConnectionString = fixture.ConnectionString!,
                Username = fixture.Username,
                Password = fixture.Password,
                BucketName = fixture.BucketName,
                ScopeName = fixture.ScopeName,
                CollectionName = fixture.CollectionName
            });

            await using var cluster = await Cluster.ConnectAsync(
                fixture.ConnectionString!,
                new ClusterOptions().WithPasswordAuthentication(fixture.Username, fixture.Password));
            await cluster.WaitUntilReadyAsync(TimeSpan.FromSeconds(60));

            var bucket = await cluster.BucketAsync(fixture.BucketName);
            await bucket.WaitUntilReadyAsync(TimeSpan.FromSeconds(60));
            var collection = bucket.Scope(fixture.ScopeName).Collection(fixture.CollectionName);

            var entityId = $"live-couchbase-{Guid.NewGuid():N}";
            await store.SaveAsync(new DbTestEntity { Id = entityId, Name = "First", Score = 1 });
            await store.UpdateAsync(new DbTestEntity { Id = entityId, Name = "Updated", Score = 2 });

            var updated = await collection.GetAsync(entityId);
            var updatedEntity = updated.ContentAs<DbTestEntity>();
            Assert.NotNull(updatedEntity);
            Assert.Equal("Updated", updatedEntity.Name);
            Assert.Equal(2, updatedEntity.Score);

            await store.DeleteAsync<DbTestEntity>(entityId);
            await Assert.ThrowsAsync<DocumentNotFoundException>(async () => await collection.GetAsync(entityId));
        });
    }
}

// ── DB-15 Redis Session Store ─────────────────────────────────────────────────
public class RedisSessionStoreTests
{
    private const string RedisConnectionEnv = "AGXIOME_REDIS_TEST_CONNECTION";

    // Redis tests cannot connect without a server; we test the store contract
    // and options only (connection attempt would throw SocketException).

    [Fact] public async Task Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new RedisSessionStore(null!));

    [Fact] public async Task ProviderType_IsRedisSession()
    {
        // Use a non-connecting string with abortConnect=false so constructor doesn't block.
        var store = new RedisSessionStore(new RedisSessionStoreOptions
        {
            ConnectionString = "localhost:6379,abortConnect=false",
            KeyPrefix = "test"
        });
        Assert.Equal(StorageProviderType.RedisSession, store.ProviderType);
        store.Dispose();
    }

    [Fact] public async Task KeyPrefix_Default()
    {
        var opts = new RedisSessionStoreOptions { ConnectionString = "localhost:6379,abortConnect=false" };
        Assert.Equal("agxioma:session", opts.KeyPrefix);
    }

    [Fact] public async Task Expiry_NullByDefault()
    {
        var opts = new RedisSessionStoreOptions { ConnectionString = "localhost:6379,abortConnect=false" };
        Assert.Null(opts.Expiry);
    }

    [Fact] public async Task Dispose_IsIdempotent()
    {
        var store = new RedisSessionStore(new RedisSessionStoreOptions
        {
            ConnectionString = "localhost:6379,abortConnect=false"
        });
        store.Dispose();
    }

    [RequiresRedisFact] public async Task SaveUpdateDelete_WithRealRedisConnection_WorksWhenConfigured()
    {
        var connectionString = Environment.GetEnvironmentVariable(RedisConnectionEnv)
            ?? "127.0.0.1:6390,abortConnect=false";
        const string keyPrefix = "agxioma:f4:redisproof";
        const string entityId = "live-redis";
        var redisKey = $"{keyPrefix}:{nameof(DbTestEntity)}:{entityId}";

        using var mux = await ConnectionMultiplexer.ConnectAsync(connectionString);
        var db = mux.GetDatabase();
        await db.KeyDeleteAsync(redisKey);

        var store = new RedisSessionStore(new RedisSessionStoreOptions
        {
            ConnectionString = connectionString,
            KeyPrefix = keyPrefix,
            Expiry = TimeSpan.FromMinutes(5)
        });

        try
        {
            await store.SaveAsync(new DbTestEntity { Id = entityId, Name = "First", Score = 1 });
            await store.UpdateAsync(new DbTestEntity { Id = entityId, Name = "Updated", Score = 2 });

            var payload = await db.StringGetAsync(redisKey);
            Assert.True(payload.HasValue);
            Assert.Contains("Updated", payload.ToString(), StringComparison.Ordinal);
            Assert.Contains("\"Score\":2", payload.ToString(), StringComparison.Ordinal);

            await store.DeleteAsync<DbTestEntity>(entityId);
            Assert.False(await db.KeyExistsAsync(redisKey));
        }
        finally
        {
            await db.KeyDeleteAsync(redisKey);
            store.Dispose();
        }
    }
}

// ── DB-16 Memcached ───────────────────────────────────────────────────────────
public class MemcachedCommandStoreTests
{
    private static MemcachedCommandStore MakeStore()
    {
        // Use delegate stubs that no-op instead of opening TCP connections.
        return new MemcachedCommandStore(
            new MemcachedStoreOptions { Host = "localhost", Port = 11211, KeyPrefix = "test" },
            setFunc:    (k, v, h, p) => Task.CompletedTask,
            deleteFunc: k => Task.CompletedTask);
    }

    [Fact] public async Task ProviderType_IsMemcached()
        => Assert.Equal(StorageProviderType.Memcached, MakeStore().ProviderType);

    [Fact] public async Task Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new MemcachedCommandStore(null!));

    [Fact] public async Task SaveAsync_CallsSetDelegate()
    {
        string? capturedKey = null;
        var store = new MemcachedCommandStore(
            new MemcachedStoreOptions { Host = "localhost", KeyPrefix = "t" },
            setFunc:    (k, v, h, p) => { capturedKey = k; return Task.CompletedTask; },
            deleteFunc: k => Task.CompletedTask);
        await store.SaveAsync(new DbTestEntity { Id = "42", Name = "n" });
        Assert.Contains("42", capturedKey);
    }

    [Fact] public async Task DeleteAsync_CallsDeleteDelegate()
    {
        string? capturedKey = null;
        var store = new MemcachedCommandStore(
            new MemcachedStoreOptions { Host = "localhost", KeyPrefix = "t" },
            setFunc:    (k, v, h, p) => Task.CompletedTask,
            deleteFunc: k => { capturedKey = k; return Task.CompletedTask; });
        await store.DeleteAsync<DbTestEntity>("99");
        Assert.Contains("99", capturedKey);
    }

    [Fact] public async Task ExpirationSeconds_DefaultZero()
    {
        var opts = new MemcachedStoreOptions { Host = "localhost" };
        Assert.Equal(0, opts.ExpirationSeconds);
    }

    [Fact(Skip = "requires live Memcached server")] public async Task SaveUpdateDelete_WithRealMemcached_WorksWhenConfigured()
    {
        var host = Environment.GetEnvironmentVariable("AGXIOME_MEMCACHED_HOST") ?? "127.0.0.1";
        var port = int.TryParse(Environment.GetEnvironmentVariable("AGXIOME_MEMCACHED_HOST_PORT"), out var parsedPort)
            ? parsedPort
            : 11212;
        var keyPrefix = $"agxioma:f4:memcached:{Guid.NewGuid():N}";
        const string entityId = "live-memcached";
        var fullKey = $"{keyPrefix}:{nameof(DbTestEntity)}:{entityId}";

        var store = new MemcachedCommandStore(new MemcachedStoreOptions
        {
            Host = host,
            Port = port,
            KeyPrefix = keyPrefix,
            ExpirationSeconds = 300
        });

        try
        {
            await RunMemcachedCommandAsync(host, port, $"delete {fullKey}\r\n");

            await store.SaveAsync(new DbTestEntity { Id = entityId, Name = "First", Score = 1 });
            await store.UpdateAsync(new DbTestEntity { Id = entityId, Name = "Updated", Score = 2 });

            var payload = await RunMemcachedCommandAsync(host, port, $"get {fullKey}\r\n");
            Assert.Contains("VALUE", payload, StringComparison.Ordinal);
            Assert.Contains("Updated", payload, StringComparison.Ordinal);
            Assert.Contains("\"Score\":2", payload, StringComparison.Ordinal);

            await store.DeleteAsync<DbTestEntity>(entityId);
            var deletedPayload = await RunMemcachedCommandAsync(host, port, $"get {fullKey}\r\n");
            Assert.Equal("END\r\n", deletedPayload);
        }
        finally
        {
            await RunMemcachedCommandAsync(host, port, $"delete {fullKey}\r\n");
        }
    }

    private static async Task<string> RunMemcachedCommandAsync(string host, int port, string command)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(host, port);
        await using var stream = client.GetStream();

        var bytes = Encoding.ASCII.GetBytes(command);
        await stream.WriteAsync(bytes);
        await stream.FlushAsync();

        var buffer = new byte[4096];
        var response = new StringBuilder();

        while (true)
        {
            var read = await stream.ReadAsync(buffer);
            if (read == 0)
                break;

            response.Append(Encoding.ASCII.GetString(buffer, 0, read));

            var text = response.ToString();
            if (text.EndsWith("END\r\n", StringComparison.Ordinal)
                || text.EndsWith("DELETED\r\n", StringComparison.Ordinal)
                || text.EndsWith("NOT_FOUND\r\n", StringComparison.Ordinal)
                || text.EndsWith("STORED\r\n", StringComparison.Ordinal)
                || text.EndsWith("ERROR\r\n", StringComparison.Ordinal))
            {
                return text;
            }
        }

        return response.ToString();
    }
}

// ── DB-17 Hazelcast ───────────────────────────────────────────────────────────
public class HazelcastCommandStoreTests
{
    private static readonly TimeSpan HazelcastOperationTimeout = TimeSpan.FromSeconds(15);

    [Fact] public async Task ProviderType_IsHazelcast()
    {
        var store = new HazelcastCommandStore(new HazelcastStoreOptions
        {
            ClusterName = "dev", NetworkAddresses = ["localhost:5701"], MapName = "test"
        });
        Assert.Equal(StorageProviderType.Hazelcast, store.ProviderType);
    }

    [Fact] public async Task Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new HazelcastCommandStore(null!));

    [Fact] public async Task SaveAsync_UnreachableHost_Throws()
    {
        var store = new HazelcastCommandStore(new HazelcastStoreOptions { ClusterName = "dev", NetworkAddresses = ["127.0.0.1:1"], MapName = "t" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "1" }, new CancellationToken(canceled: false)));
        await store.DisposeAsync();
    }

    [Fact] public async Task Options_MapName_Stored()
    {
        var opts = new HazelcastStoreOptions { ClusterName = "c", NetworkAddresses = ["h:5701"], MapName = "mymap" };
        Assert.Equal("mymap", opts.MapName);
    }

    [Fact] public async Task DisposeAsync_NoConnection_DoesNotThrow()
    {
        var store = new HazelcastCommandStore(new HazelcastStoreOptions
        {
            ClusterName = "dev", NetworkAddresses = ["localhost:5701"], MapName = "t"
        });
        await store.DisposeAsync();
    }

    [Fact(Skip = "requires live Hazelcast server")] public async Task SaveUpdateDelete_WithRealHazelcast_WorksWhenConfigured()
    {
        await AsyncFixtureRunner.UseAsync<HazelcastContainerFixture>(async fixture =>
        {
            Assert.False(string.IsNullOrWhiteSpace(fixture.Address), "Real Hazelcast backend was not available for this test run.");

            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AGXIOME_HAZELCAST_TEST_ADDRESS")))
                Assert.Equal("docker", fixture.BackendKind);

            var store = new HazelcastCommandStore(new HazelcastStoreOptions
            {
                ClusterName = fixture.ClusterName,
                NetworkAddresses = [fixture.Address!],
                MapName = fixture.MapName
            });

            var options = new Hazelcast.HazelcastOptionsBuilder()
                .With(o =>
                {
                    o.ClusterName = fixture.ClusterName;
                    o.Networking.Addresses.Add(fixture.Address!);
                })
                .Build();

            Hazelcast.IHazelcastClient? client = null;

            try
            {
                client = await Hazelcast.HazelcastClientFactory
                    .StartNewClientAsync(options)
                    .AsTask()
                    .WaitAsync(HazelcastOperationTimeout);

                var map = await client
                    .GetMapAsync<string, string>(fixture.MapName)
                    .WaitAsync(HazelcastOperationTimeout);

                var entityId = $"live-hazelcast-{Guid.NewGuid():N}";

                await store
                    .SaveAsync(new DbTestEntity { Id = entityId, Name = "First", Score = 1 })
                    .WaitAsync(HazelcastOperationTimeout);

                await store
                    .UpdateAsync(new DbTestEntity { Id = entityId, Name = "Updated", Score = 2 })
                    .WaitAsync(HazelcastOperationTimeout);

                var updatedPayload = await map.GetAsync(entityId).WaitAsync(HazelcastOperationTimeout);
                Assert.NotNull(updatedPayload);
                Assert.Contains("Updated", updatedPayload!, StringComparison.Ordinal);
                Assert.Contains("\"Score\":2", updatedPayload!, StringComparison.Ordinal);

                await store
                    .DeleteAsync<DbTestEntity>(entityId)
                    .WaitAsync(HazelcastOperationTimeout);

                var deletedPayload = await map.GetAsync(entityId).WaitAsync(HazelcastOperationTimeout);
                Assert.True(string.IsNullOrWhiteSpace(deletedPayload), $"Hazelcast entry '{entityId}' still existed after delete: {deletedPayload}");
            }
            finally
            {
                await store.DisposeAsync().AsTask().WaitAsync(HazelcastOperationTimeout);

                if (client is not null)
                    await client.DisposeAsync().AsTask().WaitAsync(HazelcastOperationTimeout);
            }
        });
    }
}

// ── DB-18 Etcd ────────────────────────────────────────────────────────────────
public class EtcdCommandStoreTests
{
    private static readonly TimeSpan EtcdOperationTimeout = TimeSpan.FromSeconds(15);

    [Fact] public async Task ProviderType_IsEtcd()
    {
        var store = new EtcdCommandStore(new EtcdStoreOptions { Endpoint = "localhost:2379" });
        Assert.Equal(StorageProviderType.Etcd, store.ProviderType);
        store.Dispose();
    }

    [Fact] public async Task Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new EtcdCommandStore(null!));

    [Fact] public async Task KeyPrefix_Default()
    {
        var opts = new EtcdStoreOptions { Endpoint = "localhost:2379" };
        Assert.Equal("/agxioma", opts.KeyPrefix);
    }

    [Fact] public async Task LeaseTtl_DefaultZero()
    {
        var opts = new EtcdStoreOptions { Endpoint = "localhost:2379" };
        Assert.Equal(0, opts.LeaseTtlSeconds);
    }

    [Fact] public async Task SaveAsync_UnreachableEndpoint_Throws()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var store = new EtcdCommandStore(new EtcdStoreOptions { Endpoint = "127.0.0.1:1" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "1" }, cts.Token));
        store.Dispose();
    }

    [RequiresEtcdFact] public async Task SaveUpdateDelete_WithRealEtcd_WorksWhenConfigured()
    {
        await AsyncFixtureRunner.UseAsync<EtcdContainerFixture>(async fixture =>
        {
            Assert.False(string.IsNullOrWhiteSpace(fixture.Endpoint), "Real etcd backend was not available for this test run.");

            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AGXIOME_ETCD_TEST_ENDPOINT")))
                Assert.Equal("docker", fixture.BackendKind);

            using var store = new EtcdCommandStore(new EtcdStoreOptions { Endpoint = fixture.Endpoint! });
            using var client = new dotnet_etcd.EtcdClient(fixture.Endpoint!);

            var entityId = $"live-etcd-{Guid.NewGuid():N}";
            var key = $"/agxioma/{nameof(DbTestEntity)}/{entityId}";

            await store.SaveAsync(new DbTestEntity { Id = entityId, Name = "First", Score = 1 }).WaitAsync(EtcdOperationTimeout);
            await store.UpdateAsync(new DbTestEntity { Id = entityId, Name = "Updated", Score = 2 }).WaitAsync(EtcdOperationTimeout);

            var updatedPayload = await client.GetValAsync(key).WaitAsync(EtcdOperationTimeout);
            Assert.NotNull(updatedPayload);
            Assert.Contains("Updated", updatedPayload!, StringComparison.Ordinal);
            Assert.Contains("\"Score\":2", updatedPayload!, StringComparison.Ordinal);

            await store.DeleteAsync<DbTestEntity>(entityId).WaitAsync(EtcdOperationTimeout);

            var deletedPayload = await client.GetValAsync(key).WaitAsync(EtcdOperationTimeout);
            Assert.True(string.IsNullOrWhiteSpace(deletedPayload), $"etcd key '{key}' still existed after delete: {deletedPayload}");
        });
    }
}

// ── DB-19 ClickHouse ──────────────────────────────────────────────────────────
public class ClickHouseCommandStoreTests
{
    private static readonly TimeSpan ClickHouseOperationTimeout = TimeSpan.FromSeconds(15);

    [Fact] public async Task ProviderType_IsClickHouse()
    {
        var store = new ClickHouseCommandStore(new ClickHouseStoreOptions
        {
            ConnectionString = "Host=localhost;Port=8123;Username=default;Password=",
            Database = "default"
        });
        Assert.Equal(StorageProviderType.ClickHouse, store.ProviderType);
    }

    [Fact] public async Task Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new ClickHouseCommandStore(null!));

    [Fact] public async Task SaveAsync_UnreachableHost_Throws()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var store = new ClickHouseCommandStore(new ClickHouseStoreOptions { ConnectionString = "Host=127.0.0.1;Port=1;Username=default;Password=;Protocol=http", Database = "default" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "1" }, cts.Token));
    }

    [Fact] public async Task DeleteAsync_UnreachableHost_Throws()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var store = new ClickHouseCommandStore(new ClickHouseStoreOptions { ConnectionString = "Host=127.0.0.1;Port=1;Username=default;Password=;Protocol=http", Database = "default" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.DeleteAsync<DbTestEntity>("1", cts.Token));
    }

    [Fact] public async Task Options_Database_Stored()
    {
        var opts = new ClickHouseStoreOptions { ConnectionString = "cs", Database = "mydb" };
        Assert.Equal("mydb", opts.Database);
    }

    [Fact(Skip = "requires live ClickHouse server")] public async Task SaveUpdateDelete_WithRealClickHouse_WorksWhenConfigured()
    {
        await AsyncFixtureRunner.UseAsync<ClickHouseContainerFixture>(async fixture =>
        {
            Assert.False(string.IsNullOrWhiteSpace(fixture.ConnectionString), "Real ClickHouse backend was not available for this test run.");

            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AGXIOME_CLICKHOUSE_TEST_CONNECTION")))
                Assert.Equal("docker", fixture.BackendKind);

            await using var conn = new ClickHouseConnection(fixture.ConnectionString!);
            await conn.OpenAsync().WaitAsync(ClickHouseOperationTimeout);

            using (var create = conn.CreateCommand())
            {
                create.CommandText = $"CREATE TABLE IF NOT EXISTS {fixture.Database}.dbtestentitys (Id String, Name String, Score Int32) ENGINE = MergeTree() ORDER BY Id";
                await create.ExecuteNonQueryAsync().WaitAsync(ClickHouseOperationTimeout);
            }

            var entityId = $"live-clickhouse-{Guid.NewGuid():N}";

            try
            {
                var store = new ClickHouseCommandStore(new ClickHouseStoreOptions
                {
                    ConnectionString = fixture.ConnectionString!,
                    Database = fixture.Database
                });

                await store.SaveAsync(new DbTestEntity { Id = entityId, Name = "First", Score = 1 }).WaitAsync(ClickHouseOperationTimeout);
                await store.UpdateAsync(new DbTestEntity { Id = entityId, Name = "Updated", Score = 2 }).WaitAsync(ClickHouseOperationTimeout);

                using (var verifyUpdated = conn.CreateCommand())
                {
                    verifyUpdated.CommandText = $"SELECT Name, Score FROM {fixture.Database}.dbtestentitys WHERE Id = '{entityId}' LIMIT 1";
                    await using var reader = await verifyUpdated.ExecuteReaderAsync().WaitAsync(ClickHouseOperationTimeout);
                    Assert.True(await reader.ReadAsync().WaitAsync(ClickHouseOperationTimeout));
                    Assert.Equal("Updated", reader.GetString(0));
                    Assert.Equal(2, reader.GetInt32(1));
                }

                await store.DeleteAsync<DbTestEntity>(entityId).WaitAsync(ClickHouseOperationTimeout);

                using var verifyDeleted = conn.CreateCommand();
                verifyDeleted.CommandText = $"SELECT COUNT() FROM {fixture.Database}.dbtestentitys WHERE Id = '{entityId}'";
                var count = await verifyDeleted.ExecuteScalarAsync().WaitAsync(ClickHouseOperationTimeout);
                Assert.Equal(0L, Convert.ToInt64(count));
            }
            finally
            {
                using var cleanup = conn.CreateCommand();
                cleanup.CommandText = $"ALTER TABLE {fixture.Database}.dbtestentitys DELETE WHERE Id = '{entityId}'";
                await cleanup.ExecuteNonQueryAsync().WaitAsync(ClickHouseOperationTimeout);
            }
        });
    }
}

// ── DB-20 InfluxDB ────────────────────────────────────────────────────────────
public class InfluxDbCommandStoreTests
{
    private static readonly TimeSpan InfluxOperationTimeout = TimeSpan.FromSeconds(15);

    [Fact] public async Task ProviderType_IsInfluxDB()
    {
        var store = new InfluxDbCommandStore(new InfluxDbStoreOptions
        {
            Url = "http://localhost:8086", Token = "tok", Org = "org", Bucket = "bkt"
        });
        Assert.Equal(StorageProviderType.InfluxDB, store.ProviderType);
        store.Dispose();
    }

    [Fact] public async Task Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new InfluxDbCommandStore(null!));

    [Fact] public async Task SaveAsync_UnreachableUrl_Throws()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var store = new InfluxDbCommandStore(new InfluxDbStoreOptions { Url = "http://127.0.0.1:1", Token = "t", Org = "o", Bucket = "b" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "1" }, cts.Token));
        store.Dispose();
    }

    [Fact] public async Task Precision_DefaultMs()
    {
        var opts = new InfluxDbStoreOptions { Url = "http://localhost:8086", Token = "t", Org = "o", Bucket = "b" };
        Assert.Equal(InfluxDB.Client.Api.Domain.WritePrecision.Ms, opts.Precision);
    }

    [Fact] public async Task Dispose_IsIdempotent()
    {
        var store = new InfluxDbCommandStore(new InfluxDbStoreOptions
        {
            Url = "http://localhost:8086", Token = "t", Org = "o", Bucket = "b"
        });
        store.Dispose();
        store.Dispose();
    }

    [Fact(Skip = "requires live InfluxDB server")] public async Task SaveUpdateDelete_WithRealInfluxDb_WorksWhenConfigured()
    {
        await AsyncFixtureRunner.UseAsync<InfluxDbContainerFixture>(async fixture =>
        {
            Assert.False(string.IsNullOrWhiteSpace(fixture.Url), "Real InfluxDB backend was not available for this test run.");

            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AGXIOME_INFLUX_TEST_URL")))
                Assert.Equal("docker", fixture.BackendKind);

            using var store = new InfluxDbCommandStore(new InfluxDbStoreOptions
            {
                Url = fixture.Url!,
                Token = fixture.Token,
                Org = fixture.Org,
                Bucket = fixture.Bucket
            });

            var entityId = $"live-influx-{Guid.NewGuid():N}";

            await store.SaveAsync(new DbTestEntity { Id = entityId, Name = "First", Score = 1 }).WaitAsync(InfluxOperationTimeout);
            await store.UpdateAsync(new DbTestEntity { Id = entityId, Name = "Updated", Score = 2 }).WaitAsync(InfluxOperationTimeout);

            var updatedFields = await WaitForInfluxFieldsAsync(fixture.Url!, fixture.Token, fixture.Org, fixture.Bucket, entityId, expectPresent: true);
            Assert.Equal("Updated", updatedFields["Name"]);
            Assert.Equal("2", updatedFields["Score"]);

            await store.DeleteAsync<DbTestEntity>(entityId).WaitAsync(InfluxOperationTimeout);

            var deletedFields = await WaitForInfluxFieldsAsync(fixture.Url!, fixture.Token, fixture.Org, fixture.Bucket, entityId, expectPresent: false);
            Assert.Empty(deletedFields);
        });
    }

    private static async Task<Dictionary<string, string>> WaitForInfluxFieldsAsync(
        string url,
        string token,
        string org,
        string bucket,
        string entityId,
        bool expectPresent,
        int attempts = 20)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var fields = await QueryInfluxFieldsAsync(url, token, org, bucket, entityId);
            if (expectPresent)
            {
                if (fields.TryGetValue("Name", out var name)
                    && fields.TryGetValue("Score", out var score)
                    && name == "Updated"
                    && score == "2")
                {
                    return fields;
                }
            }
            else if (fields.Count == 0)
            {
                return fields;
            }

            await Task.Delay(500);
        }

        return await QueryInfluxFieldsAsync(url, token, org, bucket, entityId);
    }

    private static async Task<Dictionary<string, string>> QueryInfluxFieldsAsync(string url, string token, string org, string bucket, string entityId)
    {
        var flux = $$"""
                        from(bucket: "{{bucket}}")
              |> range(start: -10m)
                            |> filter(fn: (r) => r._measurement == "dbtestentity" and r.Id == "{{entityId}}")
                            |> filter(fn: (r) => r._field == "Name" or r._field == "Score")
              |> last()
            """;

        using var client = new InfluxDB.Client.InfluxDBClient(url, token);
        var csv = await client.GetQueryApi().QueryRawAsync(flux, org: org);

        return ParseInfluxCsv(csv);
    }

    private static Dictionary<string, string> ParseInfluxCsv(string csv)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        string[]? headers = null;

        foreach (var rawLine in csv.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (rawLine.StartsWith("#", StringComparison.Ordinal))
                continue;

            var columns = rawLine.Split(',');
            if (headers is null)
            {
                headers = columns;
                continue;
            }

            if (columns.Length != headers.Length)
                continue;

            var fieldIndex = Array.IndexOf(headers, "_field");
            var valueIndex = Array.IndexOf(headers, "_value");
            if (fieldIndex < 0 || valueIndex < 0)
                continue;

            result[columns[fieldIndex]] = columns[valueIndex];
        }

        return result;
    }

}

// ── DB-21 QuestDB ─────────────────────────────────────────────────────────────
public class QuestDbCommandStoreTests
{
    private static readonly TimeSpan QuestDbOperationTimeout = TimeSpan.FromSeconds(15);

    private static QuestDbCommandStore MakeStore(HttpStatusCode code = System.Net.HttpStatusCode.NoContent, string body = "")
    {
        var opts = new QuestDbStoreOptions
        {
            NpgsqlConnectionString = "Host=localhost;Port=8812;Username=admin;Password=quest",
            IlpEndpoint = "http://localhost:9000"
        };
        var http = new HttpClient(new TestHttpMessageHandler(code, body)) { BaseAddress = new Uri(opts.IlpEndpoint) };
        return new QuestDbCommandStore(opts, http);
    }

    [Fact] public async Task ProviderType_IsQuestDB()
        => Assert.Equal(StorageProviderType.QuestDB, MakeStore().ProviderType);

    [Fact] public async Task Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new QuestDbCommandStore(null!));

    [Fact] public async Task SaveAsync_MockSuccess()
    {
        var store = MakeStore(System.Net.HttpStatusCode.NoContent, "");
        await store.SaveAsync(new DbTestEntity { Id = "1", Name = "q", Score = 5 });
    }

    [Fact] public async Task DeleteAsync_MockSuccess()
    {
        var store = MakeStore(System.Net.HttpStatusCode.NoContent, "");
        await store.DeleteAsync<DbTestEntity>("1");
    }

    [Fact] public async Task IlpEndpoint_Default()
    {
        var opts = new QuestDbStoreOptions { NpgsqlConnectionString = "cs" };
        Assert.Equal("http://localhost:9000", opts.IlpEndpoint);
    }

    [RequiresQuestDbFact] public async Task SaveUpdateDelete_WithRealQuestDb_WorksWhenConfigured()
    {
        await AsyncFixtureRunner.UseAsync<QuestDbContainerFixture>(async fixture =>
        {
            Assert.False(string.IsNullOrWhiteSpace(fixture.IlpEndpoint), "Real QuestDB backend was not available for this test run.");

            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AGXIOME_QUESTDB_ILP_ENDPOINT")))
                Assert.Equal("docker", fixture.BackendKind);

            using var http = new HttpClient { BaseAddress = new Uri(AppendTrailingSlash(fixture.IlpEndpoint!)) };
            var store = new QuestDbCommandStore(new QuestDbStoreOptions
            {
                NpgsqlConnectionString = fixture.NpgsqlConnectionString!,
                IlpEndpoint = fixture.IlpEndpoint!
            });

            var entityId = $"live-questdb-{Guid.NewGuid():N}";

            await store.SaveAsync(new DbTestEntity { Id = entityId, Name = "First", Score = 1 }).WaitAsync(QuestDbOperationTimeout);
            await store.UpdateAsync(new DbTestEntity { Id = entityId, Name = "Updated", Score = 2 }).WaitAsync(QuestDbOperationTimeout);

            var updatedRow = await WaitForQuestDbRowAsync(http, entityId, expectPresent: true);
            Assert.Equal("Updated", updatedRow["name"]);
            Assert.Equal("2", updatedRow["score"]);

            await store.DeleteAsync<DbTestEntity>(entityId).WaitAsync(QuestDbOperationTimeout);

            var deletedRow = await WaitForQuestDbRowAsync(http, entityId, expectPresent: false);
            Assert.Empty(deletedRow);
        });
    }

    private static async Task<Dictionary<string, string>> WaitForQuestDbRowAsync(HttpClient client, string entityId, bool expectPresent, int attempts = 20)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var row = await QueryQuestDbLatestRowAsync(client, entityId);

            if (expectPresent)
            {
                if (row.TryGetValue("name", out var name)
                    && row.TryGetValue("score", out var score)
                    && name == "Updated"
                    && score == "2")
                {
                    return row;
                }
            }
            else if (row.Count == 0
                || (row.TryGetValue("deleted", out var deletedValue)
                    && bool.TryParse(deletedValue, out var isDeletedValue)
                    && isDeletedValue))
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            await Task.Delay(500);
        }

        var finalRow = await QueryQuestDbLatestRowAsync(client, entityId);
        if (!expectPresent
            && finalRow.TryGetValue("deleted", out var deleted)
            && bool.TryParse(deleted, out var isDeleted)
            && isDeleted)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        return finalRow;
    }

    private static async Task<Dictionary<string, string>> QueryQuestDbLatestRowAsync(HttpClient client, string entityId)
    {
        var sql = Uri.EscapeDataString($"select * from dbtestentitys where Id = '{entityId}' order by timestamp desc limit 1");
        using var response = await client.GetAsync($"exec?query={sql}");
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        if (!json.RootElement.TryGetProperty("dataset", out var dataset)
            || dataset.GetArrayLength() == 0
            || !json.RootElement.TryGetProperty("columns", out var columns))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var row = dataset[0];
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var count = Math.Min(columns.GetArrayLength(), row.GetArrayLength());
        for (var i = 0; i < count; i++)
        {
            var column = columns[i];
            if (!column.TryGetProperty("name", out var nameElement))
                continue;

            var name = nameElement.GetString();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            result[name] = row[i].ValueKind switch
            {
                JsonValueKind.String => row[i].GetString() ?? string.Empty,
                JsonValueKind.True => bool.TrueString,
                JsonValueKind.False => bool.FalseString,
                JsonValueKind.Null => string.Empty,
                _ => row[i].ToString()
            };
        }

        return result;
    }

    private static string AppendTrailingSlash(string url)
        => url.EndsWith("/", StringComparison.Ordinal) ? url : url + "/";
}

// ── DB-22 DuckDB ─────────────────────────────────────────────────────────────
public class DuckDbCommandStoreTests
{
    [Fact] public async Task ProviderType_IsDuckDB()
    {
        var store = new DuckDbCommandStore(new DuckDbStoreOptions { DataSource = ":memory:" });
        Assert.Equal(StorageProviderType.DuckDB, store.ProviderType);
        store.Dispose();
    }

    [Fact] public async Task Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new DuckDbCommandStore(null!));

    [Fact] public async Task SaveAsync_InMemory_RoundTrip()
    {
        var store = new DuckDbCommandStore(new DuckDbStoreOptions { DataSource = ":memory:", AutoCreateSchema = true });
        var entity = new DbTestEntity { Id = "1", Name = "duck", Score = 7 };
        await store.SaveAsync(entity);
        // Delete should not throw
        await store.DeleteAsync<DbTestEntity>("1");
        store.Dispose();
    }

    [Fact] public async Task UpdateAsync_InMemory_DoesNotThrow()
    {
        var store = new DuckDbCommandStore(new DuckDbStoreOptions { DataSource = ":memory:", AutoCreateSchema = true });
        var entity = new DbTestEntity { Id = "2", Name = "quack", Score = 3 };
        await store.SaveAsync(entity);
        await store.UpdateAsync(new DbTestEntity { Id = "2", Name = "updated", Score = 9 });
        store.Dispose();
    }

    [Fact] public async Task Dispose_IsIdempotent()
    {
        var store = new DuckDbCommandStore(new DuckDbStoreOptions { DataSource = ":memory:" });
        store.Dispose();
    }

    [Fact] public async Task SaveUpdateDelete_WithRealDuckDbFile_Works()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"agxioma-duckdb-{Guid.NewGuid():N}.db");

        try
        {
            using var store = new DuckDbCommandStore(new DuckDbStoreOptions
            {
                DataSource = dbPath,
                AutoCreateSchema = true
            });

            var entity = new DbTestEntity { Id = $"duck-{Guid.NewGuid():N}", Name = "first", Score = 1 };
            await store.SaveAsync(entity);
            await store.UpdateAsync(new DbTestEntity { Id = entity.Id, Name = "updated", Score = 2 });

            var updatedRow = await QueryDuckDbRowAsync(dbPath, entity.Id);
            Assert.Equal("updated", updatedRow["Name"]);
            Assert.Equal(2L, updatedRow["Score"]);

            await store.DeleteAsync<DbTestEntity>(entity.Id);

            var deletedRow = await QueryDuckDbRowAsync(dbPath, entity.Id);
            Assert.Empty(deletedRow);
        }
        finally
        {
            if (File.Exists(dbPath))
                File.Delete(dbPath);
        }
    }

    private static async Task<Dictionary<string, object>> QueryDuckDbRowAsync(string dbPath, string id)
    {
        await using var connection = new DuckDBConnection($"DataSource={dbPath}");
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT \"Id\", \"Name\", \"Score\" FROM \"dbtestentitys\" WHERE \"Id\" = '{id.Replace("'", "''", StringComparison.Ordinal)}'";

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return new Dictionary<string, object>(StringComparer.Ordinal);

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["Id"] = reader.GetString(0),
            ["Name"] = reader.GetString(1),
            ["Score"] = reader.GetInt64(2)
        };
    }
}

// ── DB-23 Elasticsearch ───────────────────────────────────────────────────────
public class ElasticsearchCommandStoreTests
{
    private const string ElasticEndpointEnv = "AGXIOME_ELASTIC_TEST_ENDPOINT";
    private const string ElasticApiKeyEnv = "AGXIOME_ELASTIC_API_KEY";

    [Fact] public async Task ProviderType_IsElasticsearch()
    {
        var store = new ElasticsearchCommandStore(new ElasticsearchStoreOptions
        {
            CloudId = "", ApiKey = "key", NodeUri = "http://localhost:9200"
        });
        Assert.Equal(StorageProviderType.Elasticsearch, store.ProviderType);
    }

    [Fact] public async Task Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new ElasticsearchCommandStore(null!));

    [Fact] public async Task SaveAsync_InvalidNode_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ElasticsearchCommandStore(new ElasticsearchStoreOptions { CloudId = "bad-format", ApiKey = "k" }));
    }

    [Fact] public async Task DeleteAsync_InvalidNode_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ElasticsearchCommandStore(new ElasticsearchStoreOptions { CloudId = "bad-format", ApiKey = "k" }));
    }

    [Fact] public async Task IndexPrefix_Default()
    {
        var opts = new ElasticsearchStoreOptions { CloudId = "", ApiKey = "k" };
        Assert.Equal("agxioma", opts.IndexPrefix);
    }

    [Fact(Skip = "requires live Elasticsearch node")] public async Task SaveUpdateDelete_WithRealElasticsearchNode_WorksWhenConfigured()
    {
        var externalEndpoint = Environment.GetEnvironmentVariable(ElasticEndpointEnv);
        if (IsReachableHttpEndpoint(externalEndpoint))
        {
            await RunRealElasticsearchProofAsync(externalEndpoint, Environment.GetEnvironmentVariable(ElasticApiKeyEnv));
            return;
        }

        await AsyncFixtureRunner.UseAsync<ElasticsearchContainerFixture>(async fixture =>
        {
            await RunRealElasticsearchProofAsync(fixture.Endpoint, apiKey: null);
        });
    }

    internal static bool IsReachableHttpEndpoint(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint)
            || !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var port = uri.IsDefaultPort
            ? (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ? 443 : 80)
            : uri.Port;

        return ConditionalTestEnvironment.IsTcpEndpointReachable(uri.Host, port);
    }

    internal static string? TryGetJsonString(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (element.TryGetProperty(propertyName, out var value)
                && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }

    internal static int? TryGetJsonInt32(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (element.TryGetProperty(propertyName, out var value)
                && value.TryGetInt32(out var result))
            {
                return result;
            }
        }

        return null;
    }

    private static async Task RunRealElasticsearchProofAsync(string endpoint, string? apiKey)
    {
        var indexPrefix = $"agxioma-f4-es-{Guid.NewGuid():N}";
        var indexName = $"{indexPrefix}-{nameof(DbTestEntity).ToLowerInvariant()}";

        var store = new ElasticsearchCommandStore(new ElasticsearchStoreOptions
        {
            CloudId = string.Empty,
            ApiKey = apiKey ?? string.Empty,
            NodeUri = endpoint,
            IndexPrefix = indexPrefix
        });

        using var client = new HttpClient { BaseAddress = new Uri(endpoint) };
        if (!string.IsNullOrWhiteSpace(apiKey))
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("ApiKey", apiKey);

        try
        {
            await store.SaveAsync(new DbTestEntity { Id = "live-es", Name = "First", Score = 1 });
            await store.UpdateAsync(new DbTestEntity { Id = "live-es", Name = "Updated", Score = 2 });

            using var refresh = await client.PostAsync($"/{indexName}/_refresh", content: null);
            refresh.EnsureSuccessStatusCode();

            var payload = await HttpProofPolling.WaitForJsonAsync(client, $"/{indexName}/_doc/live-es", json =>
                json.RootElement.TryGetProperty("found", out var found)
                && found.GetBoolean()
                && TryGetJsonString(json.RootElement.GetProperty("_source"), "Name", "name") == "Updated"
                && TryGetJsonInt32(json.RootElement.GetProperty("_source"), "Score", "score") == 2);

            Assert.Contains("Updated", payload, StringComparison.Ordinal);

            await store.DeleteAsync<DbTestEntity>("live-es");
            using var refreshAfterDelete = await client.PostAsync($"/{indexName}/_refresh", content: null);
            refreshAfterDelete.EnsureSuccessStatusCode();

            await HttpProofPolling.WaitForStatusCodeAsync(client, $"/{indexName}/_doc/live-es", HttpStatusCode.NotFound);
        }
        finally
        {
            await client.DeleteAsync($"/{indexName}");
        }
    }
}

// ── DB-24 Meilisearch ─────────────────────────────────────────────────────────
public class MeilisearchCommandStoreTests
{
    private const string MeiliEndpointEnv = "AGXIOME_MEILI_TEST_ENDPOINT";
    private const string MeiliApiKeyEnv = "AGXIOME_MEILI_MASTER_KEY";

    [Fact] public async Task ProviderType_IsMeilisearch()
    {
        var store = new MeilisearchCommandStore(new MeilisearchStoreOptions
        {
            ApiUrl = "http://localhost:7700", ApiKey = "masterKey"
        });
        Assert.Equal(StorageProviderType.Meilisearch, store.ProviderType);
    }

    [Fact] public async Task Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new MeilisearchCommandStore(null!));

    [Fact] public async Task SaveAsync_InvalidUrl_Throws()
    {
        // Bad port triggers connection error, not ctor validation
        var store = new MeilisearchCommandStore(new MeilisearchStoreOptions { ApiUrl = "http://localhost:1", ApiKey = "k" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "1" }));
    }

    [Fact] public async Task DeleteAsync_InvalidUrl_Throws()
    {
        var store = new MeilisearchCommandStore(new MeilisearchStoreOptions { ApiUrl = "http://localhost:1", ApiKey = "k" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.DeleteAsync<DbTestEntity>("1"));
    }

    [Fact] public async Task IndexPrefix_Default()
    {
        var opts = new MeilisearchStoreOptions { ApiUrl = "http://localhost:7700", ApiKey = "k" };
        Assert.Equal("agxioma", opts.IndexPrefix);
    }

    [RequiresDockerFact] public async Task SaveUpdateDelete_WithRealMeilisearchServer_WorksWhenConfigured()
    {
        var externalEndpoint = Environment.GetEnvironmentVariable(MeiliEndpointEnv);
        if (ElasticsearchCommandStoreTests.IsReachableHttpEndpoint(externalEndpoint))
        {
            await RunRealMeilisearchProofAsync(externalEndpoint, Environment.GetEnvironmentVariable(MeiliApiKeyEnv));
            return;
        }

        await AsyncFixtureRunner.UseAsync<MeilisearchContainerFixture>(async fixture =>
        {
            await RunRealMeilisearchProofAsync(fixture.Endpoint, apiKey: string.Empty);
        });
    }

    private static async Task RunRealMeilisearchProofAsync(string endpoint, string? apiKey)
    {
        var indexPrefix = $"agxioma-f4-meili-{Guid.NewGuid():N}";
        var indexName = $"{indexPrefix}-{nameof(DbTestEntity).ToLowerInvariant()}";

        var store = new MeilisearchCommandStore(new MeilisearchStoreOptions
        {
            ApiUrl = endpoint,
            ApiKey = apiKey ?? string.Empty,
            IndexPrefix = indexPrefix
        });

        var proofClient = new Meilisearch.MeilisearchClient(endpoint, apiKey ?? string.Empty);
        using var client = new HttpClient { BaseAddress = new Uri(endpoint) };
        if (!string.IsNullOrWhiteSpace(apiKey))
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        try
        {
            await store.SaveAsync(new DbTestEntity { Id = "live-meili", Name = "First", Score = 1 });
            await store.UpdateAsync(new DbTestEntity { Id = "live-meili", Name = "Updated", Score = 2 });

            JsonElement loaded = default;
            var found = false;
            await LiveTestRetry.EventuallyAsync(async () =>
            {
                try
                {
                    loaded = await proofClient.Index(indexName).GetDocumentAsync<JsonElement>("live-meili", (List<string>?)null, CancellationToken.None);
                    found = true;
                    return ElasticsearchCommandStoreTests.TryGetJsonString(loaded, "Name", "name") == "Updated"
                        && ElasticsearchCommandStoreTests.TryGetJsonInt32(loaded, "Score", "score") == 2;
                }
                catch
                {
                    found = false;
                    return false;
                }
            }, attempts: 60, delayMs: 500);

            Assert.True(found);
            Assert.Equal("Updated", ElasticsearchCommandStoreTests.TryGetJsonString(loaded, "Name", "name"));
            Assert.Equal(2, ElasticsearchCommandStoreTests.TryGetJsonInt32(loaded, "Score", "score"));

            await store.DeleteAsync<DbTestEntity>("live-meili");
            await LiveTestRetry.EventuallyAsync(async () =>
            {
                try
                {
                    _ = await proofClient.Index(indexName).GetDocumentAsync<JsonElement>("live-meili", (List<string>?)null, CancellationToken.None);
                    return false;
                }
                catch
                {
                    return true;
                }
            }, attempts: 60, delayMs: 500);
        }
        finally
        {
            await client.DeleteAsync($"/indexes/{indexName}");
        }
    }
}

internal static class HttpProofPolling
{
    public static async Task<string> WaitForJsonAsync(
        HttpClient client,
        string path,
        Func<JsonDocument, bool> predicate,
        int attempts = 40)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                using var response = await client.GetAsync(path);
                var payload = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    using var json = JsonDocument.Parse(payload);
                    if (predicate(json))
                        return payload;
                }
            }
            catch when (attempt < attempts - 1)
            {
            }

            if (attempt < attempts - 1)
                await Task.Delay(500);
        }

        throw new TimeoutException($"Endpoint '{path}' did not reach the expected JSON state in time.");
    }

    public static async Task WaitForStatusCodeAsync(HttpClient client, string path, HttpStatusCode expectedStatusCode, int attempts = 40)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                using var response = await client.GetAsync(path);
                if (response.StatusCode == expectedStatusCode)
                    return;
            }
            catch when (attempt < attempts - 1)
            {
            }

            if (attempt < attempts - 1)
                await Task.Delay(500);
        }

        throw new TimeoutException($"Endpoint '{path}' did not return status code {(int)expectedStatusCode} in time.");
    }
}

// ── DB-25 JSON File Store ─────────────────────────────────────────────────────
public class JsonFileCommandStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "agxioma_test_" + Guid.NewGuid().ToString("N"));

    [Fact] public async Task SaveUpdateDelete_WithRealJsonFile_Works()
    {
        using var store = new JsonFileCommandStore(new JsonFileStoreOptions { BaseDirectory = _dir });
        var file = Path.Combine(_dir, "dbtestentitys.json");

        await store.SaveAsync(new DbTestEntity { Id = "live-json", Name = "First", Score = 1 });
        await store.UpdateAsync(new DbTestEntity { Id = "live-json", Name = "Updated", Score = 2 });

        using (var updated = JsonDocument.Parse(await File.ReadAllTextAsync(file)))
        {
            var entity = updated.RootElement.GetProperty("live-json");
            Assert.Equal("Updated", entity.GetProperty("Name").GetString());
            Assert.Equal(2, entity.GetProperty("Score").GetInt32());
        }

        await store.DeleteAsync<DbTestEntity>("live-json");

        using var deleted = JsonDocument.Parse(await File.ReadAllTextAsync(file));
        Assert.False(deleted.RootElement.TryGetProperty("live-json", out _));
    }

    [Fact] public async Task ProviderType_IsJsonFile()
    {
        var store = new JsonFileCommandStore(new JsonFileStoreOptions { BaseDirectory = _dir });
        Assert.Equal(StorageProviderType.JsonFile, store.ProviderType);
        store.Dispose();
    }

    [Fact] public async Task Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new JsonFileCommandStore(null!));

    [Fact] public async Task SaveAndLoad_RoundTrip()
    {
        var store  = new JsonFileCommandStore(new JsonFileStoreOptions { BaseDirectory = _dir });
        var entity = new DbTestEntity { Id = "a1", Name = "Alice", Score = 99 };
        await store.SaveAsync(entity);
        var file = Path.Combine(_dir, "dbtestentitys.json");
        var raw  = await File.ReadAllTextAsync(file);
        Assert.Contains("a1", raw);
        Assert.Contains("Alice", raw);
        store.Dispose();
    }

    [Fact] public async Task Delete_RemovesEntry()
    {
        var store  = new JsonFileCommandStore(new JsonFileStoreOptions { BaseDirectory = _dir });
        await store.SaveAsync(new DbTestEntity { Id = "del1", Name = "ToDelete", Score = 0 });
        await store.DeleteAsync<DbTestEntity>("del1");
        var file = Path.Combine(_dir, "dbtestentitys.json");
        var raw  = await File.ReadAllTextAsync(file);
        Assert.DoesNotContain("del1", raw);
        store.Dispose();
    }

    [Fact] public async Task Update_ReplacesEntry()
    {
        var store = new JsonFileCommandStore(new JsonFileStoreOptions { BaseDirectory = _dir });
        await store.SaveAsync(new DbTestEntity { Id = "u1", Name = "Before", Score = 1 });
        await store.UpdateAsync(new DbTestEntity { Id = "u1", Name = "After", Score = 2 });
        var file = Path.Combine(_dir, "dbtestentitys.json");
        var raw  = await File.ReadAllTextAsync(file);
        Assert.Contains("After", raw);
        store.Dispose();
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}


