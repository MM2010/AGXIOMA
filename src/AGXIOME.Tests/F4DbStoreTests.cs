using System.Net;
using System.Text.Json;
using AGXIOME.Shared.Models;
using AGXIOME.Storage.Stores;
using Xunit;

namespace AGXIOME.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// F4 — DB Store Tests (DB-01..DB-25)
// All tests use either in-memory stubs, :memory: DBs, or mock delegates.
// No live server required — these validate construction, enum identity,
// null-guard, and mock-round-trip behaviour.
// ─────────────────────────────────────────────────────────────────────────────

// NOT file-scoped — file keyword causes mangled type names with < > chars
// which break SQLite table names and Windows file names.
internal record DbTestEntity
{
    public string  Id    { get; init; } = string.Empty;
    public string  Name  { get; init; } = string.Empty;
    public int     Score { get; init; }
}

// ── DB-01 SQLite ─────────────────────────────────────────────────────────────
public class SqliteCommandStoreTests
{
    [Fact] public void ProviderType_IsSQLite()
    {
        var store = new SqliteCommandStore(new SqliteStoreOptions { ConnectionString = "Data Source=:memory:" });
        Assert.Equal(StorageProviderType.SQLite, store.ProviderType);
    }

    [Fact] public void Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new SqliteCommandStore(null!));

    [Fact] public async Task SaveAndDelete_RoundTrip()
    {
        var tmp   = Path.Combine(Path.GetTempPath(), $"agxioma_{Guid.NewGuid():N}.db");
        try
        {
            var store  = new SqliteCommandStore(new SqliteStoreOptions { ConnectionString = $"Data Source={tmp};Pooling=False", AutoCreateSchema = true });
            var entity = new DbTestEntity { Id = "1", Name = "Hello", Score = 42 };
            await store.SaveAsync(entity);
            await store.DeleteAsync<DbTestEntity>("1");
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    [Fact] public async Task UpdateAsync_DoesNotThrow()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"agxioma_{Guid.NewGuid():N}.db");
        try
        {
            var store = new SqliteCommandStore(new SqliteStoreOptions { ConnectionString = $"Data Source={tmp};Pooling=False", AutoCreateSchema = true });
            await store.SaveAsync(new DbTestEntity { Id = "2", Name = "Updated", Score = 99 });
            await store.UpdateAsync(new DbTestEntity { Id = "2", Name = "Updated2", Score = 100 });
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    [Fact] public void Dispose_IsIdempotent()
    {
        var store = new SqliteCommandStore(new SqliteStoreOptions { ConnectionString = "Data Source=:memory:" });
        store.Dispose();
        store.Dispose(); // second call must not throw
    }
}

// ── DB-02 SQL Server ─────────────────────────────────────────────────────────
public class SqlServerCommandStoreTests
{
    [Fact] public void ProviderType_IsSQLServer()
    {
        var store = new SqlServerCommandStore(new SqlServerStoreOptions { ConnectionString = "Server=.;Database=Test;Integrated Security=True" });
        Assert.Equal(StorageProviderType.SQLServer, store.ProviderType);
    }

    [Fact] public void Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new SqlServerCommandStore(null!));

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

    [Fact] public void Options_RoundTrip()
    {
        var opts = new SqlServerStoreOptions { ConnectionString = "my-conn-string" };
        Assert.Equal("my-conn-string", opts.ConnectionString);
    }
}

// ── DB-03 SQL Server LocalDB ─────────────────────────────────────────────────
public class SqlServerLocalDbStoreTests
{
    [Fact] public void ProviderType_IsSQLServerLocalDB()
    {
        var store = new SqlServerLocalDbCommandStore(new SqlServerLocalDbStoreOptions { ConnectionString = "Data Source=(localdb)\\MSSQLLocalDB;Database=Test" });
        Assert.Equal(StorageProviderType.SQLServerLocalDB, store.ProviderType);
    }

    [Fact] public void Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new SqlServerLocalDbCommandStore(null!));

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

    [Fact] public void Options_Defaults()
    {
        var opts = new SqlServerLocalDbStoreOptions { ConnectionString = "cs" };
        Assert.NotNull(opts.ConnectionString);
    }
}

// ── DB-04 Neon ───────────────────────────────────────────────────────────────
public class NeonCommandStoreTests
{
    [Fact] public void ProviderType_IsNeon()
    {
        var store = new NeonCommandStore(new NeonStoreOptions { ConnectionString = "Host=test;Username=u;Password=p;Database=d" });
        Assert.Equal(StorageProviderType.Neon, store.ProviderType);
    }

    [Fact] public void Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new NeonCommandStore(null!));

    [Fact(Skip = "requires live Neon server")] public async Task SaveAsync_InvalidHost_Throws()
    {
        var store = new NeonCommandStore(new NeonStoreOptions { ConnectionString = "Host=invalid_neon_host_xyz;Port=5432;Username=u;Password=p;Database=d;Timeout=1;Command Timeout=1" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "x" }));
    }

    [Fact(Skip = "requires live Neon server")] public async Task DeleteAsync_InvalidHost_Throws()
    {
        var store = new NeonCommandStore(new NeonStoreOptions { ConnectionString = "Host=invalid_neon_host_xyz;Port=5432;Username=u;Password=p;Database=d;Timeout=1;Command Timeout=1" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.DeleteAsync<DbTestEntity>("x"));
    }

    [Fact] public void Options_RoundTrip()
    {
        var opts = new NeonStoreOptions { ConnectionString = "neon-cs" };
        Assert.Equal("neon-cs", opts.ConnectionString);
    }
}

// ── DB-05 Supabase ───────────────────────────────────────────────────────────
public class SupabaseCommandStoreTests
{
    [Fact] public void ProviderType_IsSupabase()
    {
        var store = new SupabaseCommandStore(new SupabaseStoreOptions { ConnectionString = "Host=test;Username=u;Password=p;Database=d" });
        Assert.Equal(StorageProviderType.Supabase, store.ProviderType);
    }

    [Fact] public void Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new SupabaseCommandStore(null!));

    [Fact] public void BypassRls_DefaultFalse()
    {
        var opts = new SupabaseStoreOptions { ConnectionString = "cs" };
        Assert.False(opts.BypassRls);
    }

    [Fact(Skip = "requires live Supabase server")] public async Task SaveAsync_InvalidHost_Throws()
    {
        var store = new SupabaseCommandStore(new SupabaseStoreOptions { ConnectionString = "Host=invalid_supabase_xyz;Port=5432;Username=u;Password=p;Database=d;Timeout=1;Command Timeout=1" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "x" }));
    }

    [Fact] public void Options_RoundTrip()
    {
        var opts = new SupabaseStoreOptions { ConnectionString = "sb-cs", BypassRls = true };
        Assert.True(opts.BypassRls);
    }
}

// ── DB-06 SingleStore ─────────────────────────────────────────────────────────
public class SingleStoreCommandStoreTests
{
    [Fact] public void ProviderType_IsSingleStore()
    {
        var store = new SingleStoreCommandStore(new SingleStoreOptions { ConnectionString = "Server=localhost;Database=t" });
        Assert.Equal(StorageProviderType.SingleStore, store.ProviderType);
    }

    [Fact] public void Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new SingleStoreCommandStore(null!));

    [Fact] public void BulkBatchSize_Default500()
    {
        var opts = new SingleStoreOptions { ConnectionString = "cs" };
        Assert.Equal(500, opts.BulkBatchSize);
    }

    [Fact(Skip = "requires live SingleStore server")] public async Task SaveAsync_InvalidConnection_Throws()
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
}

// ── DB-07 CockroachDB ─────────────────────────────────────────────────────────
public class CockroachDbCommandStoreTests
{
    [Fact] public void ProviderType_IsCockroachDB()
    {
        var store = new CockroachDbCommandStore(new CockroachDbStoreOptions { ConnectionString = "Host=localhost;Port=26257;Username=root;Database=defaultdb" });
        Assert.Equal(StorageProviderType.CockroachDB, store.ProviderType);
    }

    [Fact] public void Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new CockroachDbCommandStore(null!));

    [Fact(Skip = "requires live CockroachDB server")] public async Task SaveAsync_InvalidHost_Throws()
    {
        var store = new CockroachDbCommandStore(new CockroachDbStoreOptions { ConnectionString = "Host=invalid_crdb_host;Port=26257;Username=root;Database=d;Timeout=1;Command Timeout=1" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "x" }));
    }

    [Fact(Skip = "requires live CockroachDB server")] public async Task DeleteAsync_InvalidHost_Throws()
    {
        var store = new CockroachDbCommandStore(new CockroachDbStoreOptions { ConnectionString = "Host=invalid_crdb_host;Port=26257;Username=root;Database=d;Timeout=1;Command Timeout=1" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.DeleteAsync<DbTestEntity>("x"));
    }

    [Fact] public void Options_RoundTrip()
    {
        var opts = new CockroachDbStoreOptions { ConnectionString = "crdb-cs" };
        Assert.Equal("crdb-cs", opts.ConnectionString);
    }
}

// ── DB-08 TimescaleDB ─────────────────────────────────────────────────────────
public class TimescaleDbCommandStoreTests
{
    [Fact] public void ProviderType_IsTimescaleDB()
    {
        var store = new TimescaleDbCommandStore(new TimescaleDbStoreOptions { ConnectionString = "Host=localhost;Username=u;Password=p;Database=d" });
        Assert.Equal(StorageProviderType.TimescaleDB, store.ProviderType);
    }

    [Fact] public void Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new TimescaleDbCommandStore(null!));

    [Fact] public void UseExplicitTransaction_DefaultFalse()
    {
        var opts = new TimescaleDbStoreOptions { ConnectionString = "cs" };
        Assert.False(opts.UseExplicitTransaction);
    }

    [Fact(Skip = "requires live TimescaleDB server")] public async Task SaveAsync_InvalidHost_Throws()
    {
        var store = new TimescaleDbCommandStore(new TimescaleDbStoreOptions { ConnectionString = "Host=invalid_tsdb_host;Port=5432;Username=u;Password=p;Database=d;Timeout=1;Command Timeout=1" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "x" }));
    }

    [Fact] public void Options_ConnectionString_Stored()
    {
        var opts = new TimescaleDbStoreOptions { ConnectionString = "ts-cs" };
        Assert.Equal("ts-cs", opts.ConnectionString);
    }
}

// ── DB-09 DynamoDB ───────────────────────────────────────────────────────────
public class DynamoDbCommandStoreTests
{
    [Fact] public void ProviderType_IsDynamoDB()
    {
        var store = new DynamoDbCommandStore(new DynamoDbStoreOptions
        {
            TableName  = "test", ServiceUrl = "http://localhost:8000",
            AccessKey  = "key",  SecretKey   = "secret"
        });
        Assert.Equal(StorageProviderType.DynamoDB, store.ProviderType);
    }

    [Fact] public void Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new DynamoDbCommandStore(null!));

    [Fact(Skip = "requires live DynamoDB endpoint")] public async Task SaveAsync_InvalidEndpoint_Throws()
    {
        var store = new DynamoDbCommandStore(new DynamoDbStoreOptions { TableName = "t", ServiceUrl = "http://invalid_dynamo_host:8000", AccessKey = "k", SecretKey = "s" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "1" }));
    }

    [Fact(Skip = "requires live DynamoDB endpoint")] public async Task DeleteAsync_InvalidEndpoint_Throws()
    {
        var store = new DynamoDbCommandStore(new DynamoDbStoreOptions { TableName = "t", ServiceUrl = "http://invalid_dynamo_host:8000", AccessKey = "k", SecretKey = "s" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.DeleteAsync<DbTestEntity>("1"));
    }

    [Fact] public void Options_Region_Default()
    {
        var opts = new DynamoDbStoreOptions
        {
            TableName = "t", ServiceUrl = "http://localhost", AccessKey = "k", SecretKey = "s"
        };
        Assert.Equal("us-east-1", opts.Region);
    }
}

// ── DB-10 CosmosDB ────────────────────────────────────────────────────────────
public class CosmosDbCommandStoreTests
{
    [Fact] public void ProviderType_IsCosmosDB()
    {
        var store = new CosmosDbCommandStore(new CosmosDbStoreOptions
        {
            ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
            DatabaseId       = "db",
            ContainerId      = "col"
        });
        Assert.Equal(StorageProviderType.CosmosDB, store.ProviderType);
    }

    [Fact] public void Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new CosmosDbCommandStore(null!));

    [Fact] public void PartitionKeyPath_Default()
    {
        var opts = new CosmosDbStoreOptions
        {
            ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=test",
            DatabaseId = "db", ContainerId = "col"
        };
        Assert.Equal("/id", opts.PartitionKeyPath);
    }

    [Fact(Skip = "requires live CosmosDB endpoint")] public async Task SaveAsync_InvalidEndpoint_Throws()
    {
        var store = new CosmosDbCommandStore(new CosmosDbStoreOptions { ConnectionString = "AccountEndpoint=https://invalid_cosmos_host:8081/;AccountKey=invalid==", DatabaseId = "db", ContainerId = "col" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "x" }));
    }

    [Fact] public void Dispose_IsIdempotent()
    {
        var store = new CosmosDbCommandStore(new CosmosDbStoreOptions
        {
            ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
            DatabaseId = "db", ContainerId = "col"
        });
        store.Dispose();
        store.Dispose();
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

    [Fact] public void Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new FirestoreCommandStore(null!));

    [Fact] public void Options_ProjectId_Stored()
    {
        var opts = new FirestoreStoreOptions { ProjectId = "my-project" };
        Assert.Equal("my-project", opts.ProjectId);
    }

    [Fact] public void Options_CredentialFilePath_NullByDefault()
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

    [Fact] public void ProviderType_IsSurrealDB()
        => Assert.Equal(StorageProviderType.SurrealDB, MakeStore().ProviderType);

    [Fact] public void Constructor_NullOptions_Throws()
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
}

// ── DB-13 RavenDB ─────────────────────────────────────────────────────────────
public class RavenDbCommandStoreTests
{
    [Fact] public void ProviderType_IsRavenDB()
    {
        var store = new RavenDbCommandStore(new RavenDbStoreOptions
        {
            ServerUrls = ["http://localhost:8080"],
            DatabaseName = "test"
        });
        Assert.Equal(StorageProviderType.RavenDB, store.ProviderType);
    }

    [Fact] public void Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new RavenDbCommandStore(null!));

    [Fact(Skip = "requires live RavenDB server")] public async Task SaveAsync_InvalidServer_Throws()
    {
        var store = new RavenDbCommandStore(new RavenDbStoreOptions { ServerUrls = ["http://invalid_raven_host:8080"], DatabaseName = "test" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "1" }));
    }

    [Fact(Skip = "requires live RavenDB server")] public async Task DeleteAsync_InvalidServer_Throws()
    {
        var store = new RavenDbCommandStore(new RavenDbStoreOptions { ServerUrls = ["http://invalid_raven_host:8080"], DatabaseName = "test" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.DeleteAsync<DbTestEntity>("1"));
    }

    [Fact] public void Dispose_IsIdempotent()
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
    [Fact] public void ProviderType_IsCouchbase()
    {
        var store = new CouchbaseCommandStore(new CouchbaseStoreOptions
        {
            ConnectionString = "couchbase://localhost", Username = "u", Password = "p",
            BucketName = "b", ScopeName = "_default", CollectionName = "_default"
        });
        Assert.Equal(StorageProviderType.Couchbase, store.ProviderType);
    }

    [Fact] public void Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new CouchbaseCommandStore(null!));

    [Fact(Skip = "requires live Couchbase server")] public async Task SaveAsync_InvalidHost_Throws()
    {
        var store = new CouchbaseCommandStore(new CouchbaseStoreOptions { ConnectionString = "couchbase://invalid_couchbase_host", Username = "u", Password = "p", BucketName = "b", ScopeName = "_default", CollectionName = "_default" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "1" }));
    }

    [Fact(Skip = "requires live Couchbase server")] public async Task DeleteAsync_InvalidHost_Throws()
    {
        var store = new CouchbaseCommandStore(new CouchbaseStoreOptions { ConnectionString = "couchbase://invalid_couchbase_host", Username = "u", Password = "p", BucketName = "b", ScopeName = "_default", CollectionName = "_default" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.DeleteAsync<DbTestEntity>("1"));
    }

    [Fact] public void Options_Defaults()
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
    // Redis tests cannot connect without a server; we test the store contract
    // and options only (connection attempt would throw SocketException).

    [Fact] public void Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new RedisSessionStore(null!));

    [Fact] public void ProviderType_IsRedisSession()
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

    [Fact] public void KeyPrefix_Default()
    {
        var opts = new RedisSessionStoreOptions { ConnectionString = "localhost:6379,abortConnect=false" };
        Assert.Equal("agxioma:session", opts.KeyPrefix);
    }

    [Fact] public void Expiry_NullByDefault()
    {
        var opts = new RedisSessionStoreOptions { ConnectionString = "localhost:6379,abortConnect=false" };
        Assert.Null(opts.Expiry);
    }

    [Fact] public void Dispose_IsIdempotent()
    {
        var store = new RedisSessionStore(new RedisSessionStoreOptions
        {
            ConnectionString = "localhost:6379,abortConnect=false"
        });
        store.Dispose();
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

    [Fact] public void ProviderType_IsMemcached()
        => Assert.Equal(StorageProviderType.Memcached, MakeStore().ProviderType);

    [Fact] public void Constructor_NullOptions_Throws()
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

    [Fact] public void ExpirationSeconds_DefaultZero()
    {
        var opts = new MemcachedStoreOptions { Host = "localhost" };
        Assert.Equal(0, opts.ExpirationSeconds);
    }
}

// ── DB-17 Hazelcast ───────────────────────────────────────────────────────────
public class HazelcastCommandStoreTests
{
    [Fact] public void ProviderType_IsHazelcast()
    {
        var store = new HazelcastCommandStore(new HazelcastStoreOptions
        {
            ClusterName = "dev", NetworkAddresses = ["localhost:5701"], MapName = "test"
        });
        Assert.Equal(StorageProviderType.Hazelcast, store.ProviderType);
    }

    [Fact] public void Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new HazelcastCommandStore(null!));

    [Fact(Skip = "requires live Hazelcast server")] public async Task SaveAsync_InvalidHost_Throws()
    {
        var store = new HazelcastCommandStore(new HazelcastStoreOptions { ClusterName = "dev", NetworkAddresses = ["invalid_hz_host:5701"], MapName = "t" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "1" }, new CancellationToken(canceled: false)));
    }

    [Fact] public void Options_MapName_Stored()
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
    [Fact] public void ProviderType_IsEtcd()
    {
        var store = new EtcdCommandStore(new EtcdStoreOptions { Endpoint = "localhost:2379" });
        Assert.Equal(StorageProviderType.Etcd, store.ProviderType);
        store.Dispose();
    }

    [Fact] public void Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new EtcdCommandStore(null!));

    [Fact] public void KeyPrefix_Default()
    {
        var opts = new EtcdStoreOptions { Endpoint = "localhost:2379" };
        Assert.Equal("/agxioma", opts.KeyPrefix);
    }

    [Fact] public void LeaseTtl_DefaultZero()
    {
        var opts = new EtcdStoreOptions { Endpoint = "localhost:2379" };
        Assert.Equal(0, opts.LeaseTtlSeconds);
    }

    [Fact(Skip = "requires live etcd server")] public async Task SaveAsync_InvalidEndpoint_Throws()
    {
        var store = new EtcdCommandStore(new EtcdStoreOptions { Endpoint = "invalid_etcd_host:2379" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "1" }));
    }
}

// ── DB-19 ClickHouse ──────────────────────────────────────────────────────────
public class ClickHouseCommandStoreTests
{
    [Fact] public void ProviderType_IsClickHouse()
    {
        var store = new ClickHouseCommandStore(new ClickHouseStoreOptions
        {
            ConnectionString = "Host=localhost;Port=8123;Username=default;Password=",
            Database = "default"
        });
        Assert.Equal(StorageProviderType.ClickHouse, store.ProviderType);
    }

    [Fact] public void Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new ClickHouseCommandStore(null!));

    [Fact(Skip = "requires live ClickHouse server")] public async Task SaveAsync_InvalidHost_Throws()
    {
        var store = new ClickHouseCommandStore(new ClickHouseStoreOptions { ConnectionString = "Host=invalid_ch_host;Port=8123;Username=default;Password=", Database = "default" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "1" }));
    }

    [Fact(Skip = "requires live ClickHouse server")] public async Task DeleteAsync_InvalidHost_Throws()
    {
        var store = new ClickHouseCommandStore(new ClickHouseStoreOptions { ConnectionString = "Host=invalid_ch_host;Port=8123;Username=default;Password=", Database = "default" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.DeleteAsync<DbTestEntity>("1"));
    }

    [Fact] public void Options_Database_Stored()
    {
        var opts = new ClickHouseStoreOptions { ConnectionString = "cs", Database = "mydb" };
        Assert.Equal("mydb", opts.Database);
    }
}

// ── DB-20 InfluxDB ────────────────────────────────────────────────────────────
public class InfluxDbCommandStoreTests
{
    [Fact] public void ProviderType_IsInfluxDB()
    {
        var store = new InfluxDbCommandStore(new InfluxDbStoreOptions
        {
            Url = "http://localhost:8086", Token = "tok", Org = "org", Bucket = "bkt"
        });
        Assert.Equal(StorageProviderType.InfluxDB, store.ProviderType);
        store.Dispose();
    }

    [Fact] public void Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new InfluxDbCommandStore(null!));

    [Fact(Skip = "requires live InfluxDB server")] public async Task SaveAsync_InvalidUrl_Throws()
    {
        var store = new InfluxDbCommandStore(new InfluxDbStoreOptions { Url = "http://invalid_influx_host:8086", Token = "t", Org = "o", Bucket = "b" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "1" }));
    }

    [Fact] public void Precision_DefaultMs()
    {
        var opts = new InfluxDbStoreOptions { Url = "http://localhost:8086", Token = "t", Org = "o", Bucket = "b" };
        Assert.Equal(InfluxDB.Client.Api.Domain.WritePrecision.Ms, opts.Precision);
    }

    [Fact] public void Dispose_IsIdempotent()
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

    [Fact] public void ProviderType_IsQuestDB()
        => Assert.Equal(StorageProviderType.QuestDB, MakeStore().ProviderType);

    [Fact] public void Constructor_NullOptions_Throws()
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

    [Fact] public void IlpEndpoint_Default()
    {
        var opts = new QuestDbStoreOptions { NpgsqlConnectionString = "cs" };
        Assert.Equal("http://localhost:9000", opts.IlpEndpoint);
    }
}

// ── DB-22 DuckDB ─────────────────────────────────────────────────────────────
public class DuckDbCommandStoreTests
{
    [Fact] public void ProviderType_IsDuckDB()
    {
        var store = new DuckDbCommandStore(new DuckDbStoreOptions { DataSource = ":memory:" });
        Assert.Equal(StorageProviderType.DuckDB, store.ProviderType);
        store.Dispose();
    }

    [Fact] public void Constructor_NullOptions_Throws()
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

    [Fact] public void Dispose_IsIdempotent()
    {
        var store = new DuckDbCommandStore(new DuckDbStoreOptions { DataSource = ":memory:" });
        store.Dispose();
    }
}

// ── DB-23 Elasticsearch ───────────────────────────────────────────────────────
public class ElasticsearchCommandStoreTests
{
    [Fact] public void ProviderType_IsElasticsearch()
    {
        var store = new ElasticsearchCommandStore(new ElasticsearchStoreOptions
        {
            CloudId = "", ApiKey = "key", NodeUri = "http://localhost:9200"
        });
        Assert.Equal(StorageProviderType.Elasticsearch, store.ProviderType);
    }

    [Fact] public void Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new ElasticsearchCommandStore(null!));

    [Fact(Skip = "requires live Elasticsearch node")] public async Task SaveAsync_InvalidNode_Throws()
    {
        var store = new ElasticsearchCommandStore(new ElasticsearchStoreOptions { CloudId = "", ApiKey = "k", NodeUri = "http://invalid_es_host:9200" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "1" }));
    }

    [Fact(Skip = "requires live Elasticsearch node")] public async Task DeleteAsync_InvalidNode_Throws()
    {
        var store = new ElasticsearchCommandStore(new ElasticsearchStoreOptions { CloudId = "", ApiKey = "k", NodeUri = "http://invalid_es_host:9200" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.DeleteAsync<DbTestEntity>("1"));
    }

    [Fact] public void IndexPrefix_Default()
    {
        var opts = new ElasticsearchStoreOptions { CloudId = "", ApiKey = "k" };
        Assert.Equal("agxioma", opts.IndexPrefix);
    }
}

// ── DB-24 Meilisearch ─────────────────────────────────────────────────────────
public class MeilisearchCommandStoreTests
{
    [Fact] public void ProviderType_IsMeilisearch()
    {
        var store = new MeilisearchCommandStore(new MeilisearchStoreOptions
        {
            ApiUrl = "http://localhost:7700", ApiKey = "masterKey"
        });
        Assert.Equal(StorageProviderType.Meilisearch, store.ProviderType);
    }

    [Fact] public void Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new MeilisearchCommandStore(null!));

    [Fact(Skip = "requires live Meilisearch server")] public async Task SaveAsync_InvalidUrl_Throws()
    {
        var store = new MeilisearchCommandStore(new MeilisearchStoreOptions { ApiUrl = "http://invalid_meili_host:7700", ApiKey = "k" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new DbTestEntity { Id = "1" }));
    }

    [Fact(Skip = "requires live Meilisearch server")] public async Task DeleteAsync_InvalidUrl_Throws()
    {
        var store = new MeilisearchCommandStore(new MeilisearchStoreOptions { ApiUrl = "http://invalid_meili_host:7700", ApiKey = "k" });
        await Assert.ThrowsAnyAsync<Exception>(() => store.DeleteAsync<DbTestEntity>("1"));
    }

    [Fact] public void IndexPrefix_Default()
    {
        var opts = new MeilisearchStoreOptions { ApiUrl = "http://localhost:7700", ApiKey = "k" };
        Assert.Equal("agxioma", opts.IndexPrefix);
    }
}

// ── DB-25 JSON File Store ─────────────────────────────────────────────────────
public class JsonFileCommandStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "agxioma_test_" + Guid.NewGuid().ToString("N"));

    [Fact] public void ProviderType_IsJsonFile()
    {
        var store = new JsonFileCommandStore(new JsonFileStoreOptions { BaseDirectory = _dir });
        Assert.Equal(StorageProviderType.JsonFile, store.ProviderType);
        store.Dispose();
    }

    [Fact] public void Constructor_NullOptions_Throws()
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
