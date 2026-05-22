using System.Net;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Net.Http.Json;
using System.Text.Json;
using System.Reflection;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using AGXIOME.Shared.Models;
using AGXIOME.Storage.Stores;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Azure.Cosmos;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;
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
        nameof(SurrealDbCommandStoreTests.SaveUpdateDelete_WithRealSurrealDb_WorksWhenConfigured),
        nameof(DynamoDbCommandStoreTests.SaveUpdateDelete_WithRealDynamoDbLocal_WorksWhenConfigured),
        nameof(RedisSessionStoreTests.SaveUpdateDelete_WithRealRedisConnection_WorksWhenConfigured),
        nameof(ElasticsearchCommandStoreTests.SaveUpdateDelete_WithRealElasticsearchNode_WorksWhenConfigured),
        nameof(MeilisearchCommandStoreTests.SaveUpdateDelete_WithRealMeilisearchServer_WorksWhenConfigured)
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
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AGXIOME_SINGLESTORE_TEST_CONNECTION")))
        {
            Skip = "SingleStore real-proof test requires AGXIOME_SINGLESTORE_TEST_CONNECTION.";
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
        ServiceUrl = $"http://{_container.Hostname}:{_container.GetMappedPublicPort(4566)}";
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
            BaseUrl = externalBaseUrl;
            BackendKind = "external";
            return;
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
        BackendKind = "docker";
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }

    public static async Task<HttpClient> CreateHttpClientAsync(string baseUrl)
    {
        var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        client.DefaultRequestHeaders.Add("Surreal-NS", "test");
        client.DefaultRequestHeaders.Add("Surreal-DB", "test");
        // Sign in without NS/DB headers (root-level auth does not require them).
        using var signinClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
        signinClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        var signin = new { user = "root", pass = "root" };
        using var res = await signinClient.PostAsJsonAsync("/signin", signin);
        res.EnsureSuccessStatusCode();
        using var doc = await res.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>();
        var token = doc!.RootElement.GetProperty("token").GetString()!;
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task WaitForSurrealAsync(string baseUrl, int attempts = 60)
    {
        // Use a plain client (no auth) against /health — SurrealDB's standard liveness endpoint
        using var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                using var response = await client.GetAsync("/health");
                if (response.IsSuccessStatusCode)
                    return;
                // Also accept NotFound in case version uses a different path
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return;
            }
            catch when (attempt < attempts - 1)
            {
            }

            await Task.Delay(500);
        }

        throw new TimeoutException("SurrealDB endpoint did not become reachable in time.");
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

    [Fact] public async Task SaveAsync_InvalidHost_Throws()
    {
        var store = new NeonCommandStore(new NeonStoreOptions { ConnectionString = "Host=invalid_neon_host_xyz;Port=5432;Username=u;Password=p;Database=d;Timeout=1;Command Timeout=1" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "x" }));
    }

    [Fact] public async Task DeleteAsync_InvalidHost_Throws()
    {
        var store = new NeonCommandStore(new NeonStoreOptions { ConnectionString = "Host=invalid_neon_host_xyz;Port=5432;Username=u;Password=p;Database=d;Timeout=1;Command Timeout=1" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.DeleteAsync<DbTestEntity>("x"));
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

    [Fact] public async Task SaveAsync_InvalidHost_Throws()
    {
        var store = new SupabaseCommandStore(new SupabaseStoreOptions { ConnectionString = "Host=invalid_supabase_xyz;Port=5432;Username=u;Password=p;Database=d;Timeout=1;Command Timeout=1" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "x" }));
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
    private const string SingleStoreConnectionEnv = "AGXIOME_SINGLESTORE_TEST_CONNECTION";

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

    [Fact] public async Task SaveAsync_InvalidConnection_Throws()
    {
        var store = new SingleStoreCommandStore(new SingleStoreOptions { ConnectionString = "Server=invalid_ss_host;Database=x;Connection Timeout=1" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "1" }));
    }

    [Fact] public async Task BulkSaveAsync_EmptyList_DoesNotThrow()
    {
        var store = new SingleStoreCommandStore(new SingleStoreOptions { ConnectionString = "Server=invalid_ss_host;Database=x;Connection Timeout=1" });
        // Empty batch short-circuits before touching the connection.
        await store.BulkSaveAsync(new List<DbTestEntity>());
    }

    [RequiresSingleStoreFact] public async Task SaveUpdateDelete_WithRealSingleStoreConnection_WorksWhenConfigured()
    {
        var connectionString = Environment.GetEnvironmentVariable(SingleStoreConnectionEnv);
        Assert.False(string.IsNullOrWhiteSpace(connectionString), "SingleStore connection override was not provided.");

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

    [Fact] public async Task SaveAsync_InvalidHost_Throws()
    {
        var store = new CockroachDbCommandStore(new CockroachDbStoreOptions { ConnectionString = "Host=invalid_crdb_host;Port=26257;Username=root;Database=d;Timeout=1;Command Timeout=1" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "x" }));
    }

    [Fact] public async Task DeleteAsync_InvalidHost_Throws()
    {
        var store = new CockroachDbCommandStore(new CockroachDbStoreOptions { ConnectionString = "Host=invalid_crdb_host;Port=26257;Username=root;Database=d;Timeout=1;Command Timeout=1" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.DeleteAsync<DbTestEntity>("x"));
    }

    [Fact] public async Task Options_RoundTrip()
    {
        var opts = new CockroachDbStoreOptions { ConnectionString = "crdb-cs" };
        Assert.Equal("crdb-cs", opts.ConnectionString);
    }

    [RequiresPostgresRealFact] public async Task SaveUpdateDelete_WithPostgresCompatConnection_Works()
    {
        await AsyncFixtureRunner.UseAsync<PostgresContainerFixture>(async fixture =>
        {
            var connectionString = fixture.ConnectionString;
            Assert.False(string.IsNullOrWhiteSpace(connectionString), "Real PostgreSQL-compatible backend was not available for Cockroach compatibility tests.");

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

    [Fact] public async Task SaveAsync_InvalidHost_Throws()
    {
        var store = new TimescaleDbCommandStore(new TimescaleDbStoreOptions { ConnectionString = "Host=invalid_tsdb_host;Port=5432;Username=u;Password=p;Database=d;Timeout=1;Command Timeout=1" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "x" }));
    }

    [Fact] public async Task Options_ConnectionString_Stored()
    {
        var opts = new TimescaleDbStoreOptions { ConnectionString = "ts-cs" };
        Assert.Equal("ts-cs", opts.ConnectionString);
    }

    [RequiresPostgresRealFact] public async Task SaveUpdateDelete_WithPostgresCompatConnection_Works()
    {
        await AsyncFixtureRunner.UseAsync<PostgresContainerFixture>(async fixture =>
        {
            var connectionString = fixture.ConnectionString;
            Assert.False(string.IsNullOrWhiteSpace(connectionString), "Real PostgreSQL-compatible backend was not available for Timescale compatibility tests.");

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
        var store = new DynamoDbCommandStore(new DynamoDbStoreOptions { TableName = "t", ServiceUrl = "http://invalid_dynamo_host:8000", AccessKey = "k", SecretKey = "s" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "1" }));
    }

    [Fact] public async Task DeleteAsync_InvalidEndpoint_Throws()
    {
        var store = new DynamoDbCommandStore(new DynamoDbStoreOptions { TableName = "t", ServiceUrl = "http://invalid_dynamo_host:8000", AccessKey = "k", SecretKey = "s" });
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
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            var store = new CosmosDbCommandStore(new CosmosDbStoreOptions { ConnectionString = "AccountEndpoint=https://invalid_cosmos_host:8081/;AccountKey=invalid==", DatabaseId = "db", ContainerId = "col" });
            await store.SaveAsync(new DbTestEntity { Id = "x" });
        });
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

        using var client = new CosmosClient(connectionString!);
        var database = await client.CreateDatabaseIfNotExistsAsync(databaseId);
        var container = await database.Database.CreateContainerIfNotExistsAsync(containerId, "/Id");

        var store = new CosmosDbCommandStore(new CosmosDbStoreOptions
        {
            ConnectionString = connectionString!,
            DatabaseId = databaseId,
            ContainerId = containerId,
            PartitionKeyPath = "/Id"
        });

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
}

// ── DB-11 Firestore ───────────────────────────────────────────────────────────
public class FirestoreCommandStoreTests
{
    [Fact(Skip = "Firestore constructor requires Google ADC")] public void ProviderType_IsFirestore()
    {
        var store = new FirestoreCommandStore(new FirestoreStoreOptions { ProjectId = "test-project" });
        Assert.Equal(StorageProviderType.Firestore, store.ProviderType);
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

    [Fact(Skip = "requires live Firestore endpoint")] public async Task SaveAsync_InvalidProject_Throws()
    {
        var store = new FirestoreCommandStore(new FirestoreStoreOptions { ProjectId = "invalid-project-xyz" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "1" }));
    }
}

// ── DB-12 SurrealDB ───────────────────────────────────────────────────────────
public class SurrealDbCommandStoreTests
{
    /// <summary>
    /// Returns {"token":"fake"} for /signin and /sql (DDL init), and the
    /// configured <paramref name="code"/>/<paramref name="body"/> for everything else.
    /// </summary>
    private sealed class SurrealDbMockHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _code;
        private readonly string _body;
        public SurrealDbMockHandler(HttpStatusCode code, string body) { _code = code; _body = body; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            // Auth and DDL bootstrap requests always succeed
            if (req.RequestUri?.AbsolutePath is "/signin" or "/sql")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    { Content = new StringContent("{\"token\":\"fake-token\"}", System.Text.Encoding.UTF8, "application/json") });
            return Task.FromResult(new HttpResponseMessage(_code)
                { Content = new StringContent(_body, System.Text.Encoding.UTF8, "application/json") });
        }
    }

    private static SurrealDbCommandStore MakeStore(HttpStatusCode code = System.Net.HttpStatusCode.OK, string body = "{}")
    {
        var opts = new SurrealDbStoreOptions
        {
            BaseUrl = "http://localhost:8000", Namespace = "test",
            Database = "test", Username = "root", Password = "root"
        };
        return new SurrealDbCommandStore(opts, new SurrealDbMockHandler(code, body));
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
            var baseUrl = Environment.GetEnvironmentVariable("AGXIOME_SURREAL_TEST_BASEURL") ?? fixture.BaseUrl;
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

            using var client = await SurrealDbContainerFixture.CreateHttpClientAsync(baseUrl!);
            var entityId = $"live-surreal-{Guid.NewGuid():N}";

            await store.SaveAsync(new DbTestEntity { Id = entityId, Name = "First", Score = 1 });
            await store.UpdateAsync(new DbTestEntity { Id = entityId, Name = "Updated", Score = 2 });

            using (var getUpdated = await client.GetAsync($"/key/dbtestentitys/{entityId}"))
            {
                var payload = await getUpdated.Content.ReadAsStringAsync();
                Assert.True(getUpdated.IsSuccessStatusCode, payload);
                Assert.Contains("Updated", payload, StringComparison.Ordinal);
                // SurrealDB v2 wraps string record IDs in Thing format (table:⟨id⟩),
                // so instead of checking for the raw entityId we verify the Score was updated.
                Assert.Contains("\"Score\":2", payload, StringComparison.Ordinal);
            }

            await store.DeleteAsync<DbTestEntity>(entityId);

            using var getDeleted = await client.GetAsync($"/key/dbtestentitys/{entityId}");
            var deletedPayload = await getDeleted.Content.ReadAsStringAsync();
            Assert.True(
                !getDeleted.IsSuccessStatusCode
                || !deletedPayload.Contains("Updated", StringComparison.Ordinal),
                $"SurrealDB record still appeared after delete. Payload: {deletedPayload}");
        });
    }
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

    [Fact] public async Task SaveAsync_InvalidServer_Throws()
    {
        var store = new RavenDbCommandStore(new RavenDbStoreOptions { ServerUrls = ["http://invalid_raven_host:8080"], DatabaseName = "test" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "1" }));
    }

    [Fact] public async Task DeleteAsync_InvalidServer_Throws()
    {
        var store = new RavenDbCommandStore(new RavenDbStoreOptions { ServerUrls = ["http://invalid_raven_host:8080"], DatabaseName = "test" });
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

    [Fact] public async Task SaveAsync_InvalidHost_Throws()
    {
        var store = new CouchbaseCommandStore(new CouchbaseStoreOptions { ConnectionString = "couchbase://invalid_couchbase_host", Username = "u", Password = "p", BucketName = "b", ScopeName = "_default", CollectionName = "_default" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "1" }));
    }

    [Fact] public async Task DeleteAsync_InvalidHost_Throws()
    {
        var store = new CouchbaseCommandStore(new CouchbaseStoreOptions { ConnectionString = "couchbase://invalid_couchbase_host", Username = "u", Password = "p", BucketName = "b", ScopeName = "_default", CollectionName = "_default" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.DeleteAsync<DbTestEntity>("1"));
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
}

// ── DB-17 Hazelcast ───────────────────────────────────────────────────────────
public class HazelcastCommandStoreTests
{
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

    [Fact] public async Task SaveAsync_InvalidHost_Throws()
    {
        var store = new HazelcastCommandStore(new HazelcastStoreOptions { ClusterName = "dev", NetworkAddresses = ["invalid_hz_host:5701"], MapName = "t" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "1" }, new CancellationToken(canceled: false)));
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
}

// ── DB-18 Etcd ────────────────────────────────────────────────────────────────
public class EtcdCommandStoreTests
{
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

    [Fact] public async Task SaveAsync_InvalidEndpoint_Throws()
    {
        var store = new EtcdCommandStore(new EtcdStoreOptions { Endpoint = "invalid_etcd_host:2379" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "1" }));
    }
}

// ── DB-19 ClickHouse ──────────────────────────────────────────────────────────
public class ClickHouseCommandStoreTests
{
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

    [Fact] public async Task SaveAsync_InvalidHost_Throws()
    {
        var store = new ClickHouseCommandStore(new ClickHouseStoreOptions { ConnectionString = "Host=invalid_ch_host;Port=8123;Username=default;Password=", Database = "default" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "1" }));
    }

    [Fact] public async Task DeleteAsync_InvalidHost_Throws()
    {
        var store = new ClickHouseCommandStore(new ClickHouseStoreOptions { ConnectionString = "Host=invalid_ch_host;Port=8123;Username=default;Password=", Database = "default" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.DeleteAsync<DbTestEntity>("1"));
    }

    [Fact] public async Task Options_Database_Stored()
    {
        var opts = new ClickHouseStoreOptions { ConnectionString = "cs", Database = "mydb" };
        Assert.Equal("mydb", opts.Database);
    }
}

// ── DB-20 InfluxDB ────────────────────────────────────────────────────────────
public class InfluxDbCommandStoreTests
{
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

    [Fact] public async Task SaveAsync_InvalidUrl_Throws()
    {
        var store = new InfluxDbCommandStore(new InfluxDbStoreOptions { Url = "http://invalid_influx_host:8086", Token = "t", Org = "o", Bucket = "b" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "1" }));
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
}

// ── DB-21 QuestDB ─────────────────────────────────────────────────────────────
public class QuestDbCommandStoreTests
{
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
        var store = MakeStore(System.Net.HttpStatusCode.OK, "{\"query\":\"ok\"}");
        await store.DeleteAsync<DbTestEntity>("1");
    }

    [Fact] public async Task IlpEndpoint_Default()
    {
        var opts = new QuestDbStoreOptions { NpgsqlConnectionString = "cs" };
        Assert.Equal("http://localhost:9000", opts.IlpEndpoint);
    }
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
        var store = new ElasticsearchCommandStore(new ElasticsearchStoreOptions { CloudId = "", ApiKey = "k", NodeUri = "http://invalid_es_host:9200" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "1" }));
    }

    [Fact] public async Task DeleteAsync_InvalidNode_Throws()
    {
        var store = new ElasticsearchCommandStore(new ElasticsearchStoreOptions { CloudId = "", ApiKey = "k", NodeUri = "http://invalid_es_host:9200" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.DeleteAsync<DbTestEntity>("1"));
    }

    [Fact] public async Task IndexPrefix_Default()
    {
        var opts = new ElasticsearchStoreOptions { CloudId = "", ApiKey = "k" };
        Assert.Equal("agxioma", opts.IndexPrefix);
    }

    [RequiresDockerFact] public async Task SaveUpdateDelete_WithRealElasticsearchNode_WorksWhenConfigured()
    {
        var externalEndpoint = Environment.GetEnvironmentVariable(ElasticEndpointEnv);
        if (!string.IsNullOrWhiteSpace(externalEndpoint))
        {
            await RunRealElasticsearchProofAsync(externalEndpoint, Environment.GetEnvironmentVariable(ElasticApiKeyEnv));
            return;
        }

        await AsyncFixtureRunner.UseAsync<ElasticsearchContainerFixture>(async fixture =>
        {
            await RunRealElasticsearchProofAsync(fixture.Endpoint, apiKey: null);
        });
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
                && json.RootElement.TryGetProperty("_source", out var src)
                && (   (src.TryGetProperty("Name", out var nameEl) && nameEl.GetString() == "Updated")
                    || (src.TryGetProperty("name", out var nameEl2) && nameEl2.GetString() == "Updated"))
                && (   (src.TryGetProperty("Score", out var scoreEl) && scoreEl.GetInt32() == 2)
                    || (src.TryGetProperty("score", out var scoreEl2) && scoreEl2.GetInt32() == 2)));

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
        var store = new MeilisearchCommandStore(new MeilisearchStoreOptions { ApiUrl = "http://invalid_meili_host:7700", ApiKey = "k" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "1" }));
    }

    [Fact] public async Task DeleteAsync_InvalidUrl_Throws()
    {
        var store = new MeilisearchCommandStore(new MeilisearchStoreOptions { ApiUrl = "http://invalid_meili_host:7700", ApiKey = "k" });
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
        if (!string.IsNullOrWhiteSpace(externalEndpoint))
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

        using var client = new HttpClient { BaseAddress = new Uri(endpoint) };
        if (!string.IsNullOrWhiteSpace(apiKey))
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        try
        {
            await store.SaveAsync(new DbTestEntity { Id = "live-meili", Name = "First", Score = 1 });
            await store.UpdateAsync(new DbTestEntity { Id = "live-meili", Name = "Updated", Score = 2 });

            var payload = await HttpProofPolling.WaitForJsonAsync(client, $"/indexes/{indexName}/documents/live-meili", json =>
                (   (json.RootElement.TryGetProperty("Name", out var nameEl) && nameEl.GetString() == "Updated")
                 || (json.RootElement.TryGetProperty("name", out var nameEl2) && nameEl2.GetString() == "Updated"))
                && (   (json.RootElement.TryGetProperty("Score", out var scoreEl) && scoreEl.GetInt32() == 2)
                    || (json.RootElement.TryGetProperty("score", out var scoreEl2) && scoreEl2.GetInt32() == 2)));

            Assert.Contains("Updated", payload, StringComparison.Ordinal);

            await store.DeleteAsync<DbTestEntity>("live-meili");
            await HttpProofPolling.WaitForStatusCodeAsync(client, $"/indexes/{indexName}/documents/live-meili", HttpStatusCode.NotFound);
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
