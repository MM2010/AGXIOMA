using AGXIOME.Shared.Interfaces;
using AGXIOME.Storage.Session;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Xunit;

namespace AGXIOME.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// F5 — ISessionStore<T> Adapter Tests
// Covers: InMemorySessionStore, SqliteSessionStore, RedisSessionStoreAdapter,
//         PostgresSessionStore, JsonFileSessionStore
// Each adapter must satisfy the same CRUD + HealthCheck contract.
// ─────────────────────────────────────────────────────────────────────────────

internal record TestSessionPayload
{
    public string UserId  { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public int    Turn    { get; init; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Shared CRUD behaviour — run against any ISessionStore<T> implementation
// ─────────────────────────────────────────────────────────────────────────────
internal static class SessionStoreBehaviourRunner
{
    public static async Task RunCrudAsync(ISessionStore<TestSessionPayload> store)
    {
        const string sid = "session-abc";

        // Initially absent
        var empty = await store.GetAsync(sid);
        Assert.Null(empty);

        // Set
        var payload = new TestSessionPayload { UserId = "u1", Message = "hello", Turn = 1 };
        await store.SetAsync(sid, payload);

        // Get
        var retrieved = await store.GetAsync(sid);
        Assert.NotNull(retrieved);
        Assert.Equal("u1",    retrieved.UserId);
        Assert.Equal("hello", retrieved.Message);
        Assert.Equal(1,       retrieved.Turn);

        // Update (overwrite)
        var updated = payload with { Message = "world", Turn = 2 };
        await store.SetAsync(sid, updated);
        var afterUpdate = await store.GetAsync(sid);
        Assert.Equal("world", afterUpdate!.Message);
        Assert.Equal(2, afterUpdate.Turn);

        // Remove
        await store.RemoveAsync(sid);
        var afterDelete = await store.GetAsync(sid);
        Assert.Null(afterDelete);
    }

    public static async Task RunHealthCheckAsync(ISessionStore<TestSessionPayload> store)
    {
        var healthy = await store.HealthCheckAsync();
        Assert.True(healthy);
    }
}

// ── InMemory ─────────────────────────────────────────────────────────────────
public class InMemorySessionStoreTests
{
    private static InMemorySessionStore<TestSessionPayload> CreateStore()
        => new();

    [Fact] public async Task CRUD_Works()
        => await SessionStoreBehaviourRunner.RunCrudAsync(CreateStore());

    [Fact] public async Task HealthCheck_ReturnsTrue()
        => await SessionStoreBehaviourRunner.RunHealthCheckAsync(CreateStore());

    [Fact] public async Task Get_NonExistentKey_ReturnsNull()
    {
        var store = CreateStore();
        var result = await store.GetAsync("no-such-session");
        Assert.Null(result);
    }

    [Fact] public async Task Remove_NonExistentKey_DoesNotThrow()
    {
        var store = CreateStore();
        var ex = await Record.ExceptionAsync(() => store.RemoveAsync("ghost-session"));
        Assert.Null(ex);
    }

    [Fact] public async Task Set_ConcurrentWrites_DoesNotThrow()
    {
        var store = CreateStore();
        var tasks = Enumerable.Range(0, 50).Select(i =>
            store.SetAsync($"session-{i}", new TestSessionPayload { UserId = $"u{i}", Turn = i }));
        await Task.WhenAll(tasks);
    }
}

// ── SQLite ────────────────────────────────────────────────────────────────────
public class SqliteSessionStoreAdapterTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"agxiome_f5_{Guid.NewGuid():N}.db");

    private SqliteSessionStore<TestSessionPayload> CreateStore()
        => new(new SqliteSessionStoreOptions { ConnectionString = $"Data Source={_dbPath};Pooling=False" });

    [Fact] public async Task CRUD_Works()
        => await SessionStoreBehaviourRunner.RunCrudAsync(CreateStore());

    [Fact] public async Task HealthCheck_ReturnsTrue()
        => await SessionStoreBehaviourRunner.RunHealthCheckAsync(CreateStore());

    [Fact] public async Task SchemaCreation_IsIdempotent()
    {
        // Constructing the store twice against the same DB must not throw
        _ = CreateStore();
        _ = CreateStore();
    }

    [Fact] public async Task Persist_AcrossInstances()
    {
        var connStr = $"Data Source={_dbPath};Pooling=False";
        var opts    = new SqliteSessionStoreOptions { ConnectionString = connStr };
        {
            var store1 = new SqliteSessionStore<TestSessionPayload>(opts);
            await store1.SetAsync("persist-key", new TestSessionPayload { UserId = "persisted", Turn = 42 });
        }
        {
            var store2 = new SqliteSessionStore<TestSessionPayload>(opts);
            var got    = await store2.GetAsync("persist-key");
            Assert.Equal("persisted", got!.UserId);
            Assert.Equal(42, got.Turn);
        }
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}

// ── JSON File ─────────────────────────────────────────────────────────────────
public class JsonFileSessionStoreAdapterTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"agxiome_f5_json_{Guid.NewGuid():N}");

    private JsonFileSessionStore<TestSessionPayload> CreateStore()
        => new(new JsonFileSessionStoreOptions { BaseDirectory = _dir });

    [Fact] public async Task CRUD_Works()
        => await SessionStoreBehaviourRunner.RunCrudAsync(CreateStore());

    [Fact] public async Task HealthCheck_ReturnsTrue()
        => await SessionStoreBehaviourRunner.RunHealthCheckAsync(CreateStore());

    [Fact] public async Task Persist_AcrossInstances()
    {
        var opts = new JsonFileSessionStoreOptions { BaseDirectory = _dir };
        {
            var store1 = new JsonFileSessionStore<TestSessionPayload>(opts);
            await store1.SetAsync("file-persist", new TestSessionPayload { UserId = "fileUser", Turn = 7 });
        }
        {
            var store2 = new JsonFileSessionStore<TestSessionPayload>(opts);
            var got    = await store2.GetAsync("file-persist");
            Assert.Equal("fileUser", got!.UserId);
            Assert.Equal(7, got.Turn);
        }
    }

    [Fact] public async Task Remove_DeletesFile()
    {
        var store = CreateStore();
        await store.SetAsync("to-delete", new TestSessionPayload { UserId = "x" });
        await store.RemoveAsync("to-delete");
        var after = await store.GetAsync("to-delete");
        Assert.Null(after);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}

// ── Redis (Testcontainers) ────────────────────────────────────────────────────
public class RedisSessionStoreAdapterContainerTests : IAsyncLifetime
{
    private RedisContainer? _container;
    private string? _connectionString;

    public async Task InitializeAsync()
    {
        if (!ConditionalTestEnvironment.IsDockerAvailable())
        {
            _connectionString = null;
            return;
        }

        _container = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .Build();

        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync().AsTask();
    }

    [RequiresDockerFact]
    public async Task CRUD_Works()
    {
        if (string.IsNullOrWhiteSpace(_connectionString)) return;
        using var store = new RedisSessionStoreAdapter<TestSessionPayload>(
            new RedisSessionAdapterOptions { ConnectionString = _connectionString, KeyPrefix = "f5test" });
        await SessionStoreBehaviourRunner.RunCrudAsync(store);
    }

    [RequiresDockerFact]
    public async Task HealthCheck_ReturnsTrue()
    {
        if (string.IsNullOrWhiteSpace(_connectionString)) return;
        using var store = new RedisSessionStoreAdapter<TestSessionPayload>(
            new RedisSessionAdapterOptions { ConnectionString = _connectionString });
        await SessionStoreBehaviourRunner.RunHealthCheckAsync(store);
    }

    [RequiresDockerFact]
    public async Task Expiry_ExpiredKey_ReturnsNull()
    {
        if (string.IsNullOrWhiteSpace(_connectionString)) return;
        using var store = new RedisSessionStoreAdapter<TestSessionPayload>(
            new RedisSessionAdapterOptions
            {
                ConnectionString = _connectionString,
                KeyPrefix        = "f5ttl",
                Expiry           = TimeSpan.FromMilliseconds(300)
            });

        await store.SetAsync("ttl-key", new TestSessionPayload { UserId = "ttl-user" });
        await Task.Delay(500);
        var expired = await store.GetAsync("ttl-key");
        Assert.Null(expired);
    }
}

// ── PostgreSQL (Testcontainers) ────────────────────────────────────────────────
public class PostgresSessionStoreAdapterContainerTests : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private string? _connectionString;

    public async Task InitializeAsync()
    {
        if (!ConditionalTestEnvironment.IsDockerAvailable())
        {
            _connectionString = null;
            return;
        }

        _container = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("agxiome_f5")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync().AsTask();
    }

    [RequiresDockerFact]
    public async Task CRUD_Works()
    {
        if (string.IsNullOrWhiteSpace(_connectionString)) return;
        var store = new PostgresSessionStore<TestSessionPayload>(
            new PostgresSessionStoreOptions { ConnectionString = _connectionString });
        await SessionStoreBehaviourRunner.RunCrudAsync(store);
    }

    [RequiresDockerFact]
    public async Task HealthCheck_ReturnsTrue()
    {
        if (string.IsNullOrWhiteSpace(_connectionString)) return;
        var store = new PostgresSessionStore<TestSessionPayload>(
            new PostgresSessionStoreOptions { ConnectionString = _connectionString });
        await SessionStoreBehaviourRunner.RunHealthCheckAsync(store);
    }

    [RequiresDockerFact]
    public async Task SchemaCreation_IsIdempotent()
    {
        if (string.IsNullOrWhiteSpace(_connectionString)) return;
        var opts = new PostgresSessionStoreOptions { ConnectionString = _connectionString };
        _ = new PostgresSessionStore<TestSessionPayload>(opts);
        _ = new PostgresSessionStore<TestSessionPayload>(opts); // second init must not throw
    }
}
