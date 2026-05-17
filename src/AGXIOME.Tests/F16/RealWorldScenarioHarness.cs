using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using AGXIOME.Orchestration.Abstractions;
using AGXIOME.Orchestration.Infrastructure;
using AGXIOME.Orchestration.Models;
using AGXIOME.Orchestration.Tools.Master;
using AGXIOME.Orchestration.Tools.SubAgent;
using AGXIOME.Shared.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace AGXIOME.Tests.F16;

internal static class ScenarioJson
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    internal static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    internal static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);

    internal static JsonElement ToElement(object? value)
    {
        if (value is null)
        {
            return JsonSerializer.SerializeToElement<object?>(null, Options);
        }

        return JsonSerializer.SerializeToElement(value, value.GetType(), Options);
    }
}

public sealed class RealInfraFixture : IAsyncLifetime
{
    public RedisContainer Redis { get; } = new RedisBuilder("redis:7.2-alpine").Build();

    public PostgreSqlContainer Postgres { get; } = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("agxiome")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string RedisConnectionString => Redis.GetConnectionString();

    public string PostgresConnectionString => Postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await Redis.StartAsync();
        await Postgres.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await Redis.DisposeAsync().AsTask();
        await Postgres.DisposeAsync().AsTask();
    }
}

[CollectionDefinition(CollectionName)]
public sealed class RealInfraCollection : ICollectionFixture<RealInfraFixture>
{
    public const string CollectionName = "real-infra";
}

internal sealed class RealWorldOrchestrationScenarioBuilder
{
    private string? _sqlitePath;
    private string? _redisConnectionString;
    private Func<ICompletionNotifier>? _notifierFactory;

    public RealWorldOrchestrationScenarioBuilder WithSqliteStore(string? sqlitePath = null)
    {
        _sqlitePath = sqlitePath;
        return this;
    }

    public RealWorldOrchestrationScenarioBuilder WithRedisHotStore(string redisConnectionString)
    {
        _redisConnectionString = redisConnectionString;
        return this;
    }

    public RealWorldOrchestrationScenarioBuilder WithCompletionNotifier(Func<ICompletionNotifier> notifierFactory)
    {
        _notifierFactory = notifierFactory;
        return this;
    }

    public async Task<RealWorldOrchestrationScenario> BuildAsync()
    {
        var sqlitePath = _sqlitePath ?? Path.Combine(Path.GetTempPath(), $"agxiome_f16_{Guid.NewGuid():N}.db");
        var coldStore = new SqliteOrchestrationStore(sqlitePath);
        await coldStore.InitializeAsync();

        IHotStore hotStore = _redisConnectionString is { Length: > 0 }
            ? await RedisHotStore.ConnectAsync(_redisConnectionString)
            : new InMemoryHotStore();

        var notifier = _notifierFactory?.Invoke() ?? new InMemoryCompletionNotifier();

        return new RealWorldOrchestrationScenario(sqlitePath, hotStore, coldStore, notifier);
    }
}

internal sealed class RealWorldOrchestrationScenario : IAsyncDisposable
{
    public RealWorldOrchestrationScenario(
        string sqlitePath,
        IHotStore hotStore,
        SqliteOrchestrationStore coldStore,
        ICompletionNotifier notifier)
    {
        SqlitePath = sqlitePath;
        HotStore = hotStore;
        ColdStore = coldStore;
        Notifier = notifier;
        SaveResultTool = new SaveResultTool(hotStore, coldStore);
        NotifyCompletionTool = new NotifyCompletionTool(notifier, hotStore);
        PollResultsTool = new PollResultsTool(notifier, hotStore, coldStore);
    }

    public string SqlitePath { get; }
    public IHotStore HotStore { get; }
    public SqliteOrchestrationStore ColdStore { get; }
    public ICompletionNotifier Notifier { get; }
    public SaveResultTool SaveResultTool { get; }
    public NotifyCompletionTool NotifyCompletionTool { get; }
    public PollResultsTool PollResultsTool { get; }

    public Task SavePlanAsync(PlanState plan, CancellationToken ct = default) => ColdStore.SavePlanAsync(plan, ct);

    public Task<ToolResult> SaveResultAsync(
        Guid planId,
        Guid taskId,
        string output,
        bool success = true,
        string? summary = null,
        long? elapsedMs = null,
        CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, object>
        {
            ["planId"] = planId.ToString(),
            ["taskId"] = taskId.ToString(),
            ["output"] = output,
            ["success"] = success
        };

        if (summary is not null)
        {
            parameters["summary"] = summary;
        }

        if (elapsedMs.HasValue)
        {
            parameters["elapsedMs"] = elapsedMs.Value;
        }

        return SaveResultTool.ExecuteAsync(new ToolContext("f16", planId.ToString(), Parameters: parameters), ct);
    }

    public Task<ToolResult> NotifyAsync(Guid planId, Guid taskId, CancellationToken ct = default) =>
        NotifyCompletionTool.ExecuteAsync(
            new ToolContext(
                "f16",
                planId.ToString(),
                Parameters: new Dictionary<string, object>
                {
                    ["planId"] = planId.ToString(),
                    ["taskId"] = taskId.ToString()
                }),
            ct);

    public Task<ToolResult> PollAsync(Guid planId, IReadOnlyList<Guid> expectedTaskIds, int timeoutSeconds, CancellationToken ct = default) =>
        PollResultsTool.ExecuteAsync(
            new ToolContext(
                "f16",
                planId.ToString(),
                Parameters: new Dictionary<string, object>
                {
                    ["planId"] = planId.ToString(),
                    ["expectedTaskIds"] = expectedTaskIds,
                    ["timeoutSeconds"] = timeoutSeconds
                }),
            ct);

    public async ValueTask DisposeAsync()
    {
        switch (Notifier)
        {
            case IAsyncDisposable asyncDisposableNotifier:
                await asyncDisposableNotifier.DisposeAsync();
                break;
            case IDisposable disposableNotifier:
                disposableNotifier.Dispose();
                break;
        }

        switch (HotStore)
        {
            case IAsyncDisposable asyncDisposableHotStore:
                await asyncDisposableHotStore.DisposeAsync();
                break;
            case IDisposable disposableHotStore:
                disposableHotStore.Dispose();
                break;
        }

        await ColdStore.DisposeAsync();
    }
}

internal sealed class RedisHotStore : IHotStore, IAsyncDisposable
{
    private readonly ConnectionMultiplexer _connection;
    private readonly IDatabase _database;
    private readonly ISubscriber _subscriber;

    private RedisHotStore(ConnectionMultiplexer connection)
    {
        _connection = connection;
        _database = connection.GetDatabase();
        _subscriber = connection.GetSubscriber();
    }

    public static async Task<RedisHotStore> ConnectAsync(string connectionString)
    {
        var connection = await ConnectionMultiplexer.ConnectAsync(connectionString);
        return new RedisHotStore(connection);
    }

    public Task SetAsync(string key, string value, TimeSpan? ttl = null, CancellationToken ct = default) =>
        ttl.HasValue
            ? _database.StringSetAsync(key, value, ttl.Value)
            : _database.StringSetAsync(key, value);

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        var value = await _database.StringGetAsync(key);
        return value.HasValue ? value.ToString() : null;
    }

    public Task DeleteAsync(string key, CancellationToken ct = default) => _database.KeyDeleteAsync(key);

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default) => _database.KeyExistsAsync(key);

    public Task<long> IncrementAsync(string key, CancellationToken ct = default) => _database.StringIncrementAsync(key);

    public Task SetAddAsync(string key, string member, CancellationToken ct = default) => _database.SetAddAsync(key, member);

    public async Task<IReadOnlySet<string>> SetMembersAsync(string key, CancellationToken ct = default)
    {
        var values = await _database.SetMembersAsync(key);
        return new HashSet<string>(values.Select(static value => value.ToString()));
    }

    public async IAsyncEnumerable<string> SubscribeAsync(string channel,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var redisChannel = new RedisChannel(channel, RedisChannel.PatternMode.Literal);

        var queue = await _subscriber.SubscribeAsync(redisChannel);

        try
        {
            await foreach (var message in queue.WithCancellation(ct))
            {
                yield return message.Message.ToString();
            }
        }
        finally
        {
            await _subscriber.UnsubscribeAsync(redisChannel);
        }
    }

    public Task PublishAsync(string channel, string message, CancellationToken ct = default) =>
        _subscriber.PublishAsync(RedisChannel.Literal(channel), message);

    public async ValueTask DisposeAsync()
    {
        await _connection.CloseAsync();
        await _connection.DisposeAsync();
    }
}

internal sealed class SqliteOrchestrationStore : IOrchestrationStore, IAsyncDisposable
{
    private readonly string _connectionString;

    public SqliteOrchestrationStore(string databasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath) ?? Path.GetTempPath());

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false,
            Cache = SqliteCacheMode.Shared,
            Mode = SqliteOpenMode.ReadWriteCreate,
            DefaultTimeout = 1
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await using var connection = await OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS plans (
                plan_id TEXT PRIMARY KEY,
                status INTEGER NOT NULL,
                updated_at TEXT NOT NULL,
                payload TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS task_specs (
                task_id TEXT PRIMARY KEY,
                plan_id TEXT NOT NULL,
                status INTEGER NOT NULL,
                started_at TEXT NULL,
                payload TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS results (
                task_id TEXT PRIMARY KEY,
                plan_id TEXT NOT NULL,
                completed_at TEXT NOT NULL,
                payload TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task SavePlanAsync(PlanState plan, CancellationToken ct = default)
    {
        await using var connection = await OpenConnectionAsync(ct);
        await UpsertPlanAsync(connection, null, plan, ct);
    }

    public async Task<PlanState?> GetPlanAsync(Guid planId, CancellationToken ct = default)
    {
        await using var connection = await OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT payload FROM plans WHERE plan_id = $planId LIMIT 1;";
        command.Parameters.AddWithValue("$planId", planId.ToString());
        var payload = await command.ExecuteScalarAsync(ct) as string;
        return payload is null ? null : ScenarioJson.Deserialize<PlanState>(payload);
    }

    public async Task UpdatePlanStatusAsync(Guid planId, OrchestratorPlanStatus status, string? finalSummary = null, CancellationToken ct = default)
    {
        var existing = await GetPlanAsync(planId, ct);
        if (existing is null)
        {
            return;
        }

        var updated = existing with
        {
            Status = status,
            FinalSummary = finalSummary ?? existing.FinalSummary,
            CompletedAt = status is OrchestratorPlanStatus.Completed or OrchestratorPlanStatus.Failed or OrchestratorPlanStatus.Cancelled
                ? DateTimeOffset.UtcNow
                : existing.CompletedAt,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await SavePlanAsync(updated, ct);
    }

    public async Task AdvancePlanStepAsync(Guid planId, int newStep, IReadOnlyDictionary<Guid, TaskResult> newResults, CancellationToken ct = default)
    {
        await using var connection = await OpenConnectionAsync(ct);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct);

        var existing = await GetPlanAsync(planId, ct);
        if (existing is null)
        {
            await transaction.CommitAsync(ct);
            return;
        }

        var merged = new Dictionary<Guid, TaskResult>(existing.CompletedResults);
        foreach (var pair in newResults)
        {
            merged[pair.Key] = pair.Value;
            await UpsertResultAsync(connection, transaction, pair.Value, ct);
        }

        var updated = existing with
        {
            CurrentStep = newStep,
            Status = OrchestratorPlanStatus.Running,
            CompletedResults = merged,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await UpsertPlanAsync(connection, transaction, updated, ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<PlanState>> GetActivePlansAsync(CancellationToken ct = default)
    {
        await using var connection = await OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT payload FROM plans WHERE status IN ($running, $paused);";
        command.Parameters.AddWithValue("$running", (int)OrchestratorPlanStatus.Running);
        command.Parameters.AddWithValue("$paused", (int)OrchestratorPlanStatus.Paused);

        var plans = new List<PlanState>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var payload = reader.GetString(0);
            var plan = ScenarioJson.Deserialize<PlanState>(payload);
            if (plan is not null)
            {
                plans.Add(plan);
            }
        }

        return plans;
    }

    public async Task SaveTaskSpecAsync(TaskSpec spec, CancellationToken ct = default)
    {
        await using var connection = await OpenConnectionAsync(ct);
        await UpsertTaskSpecAsync(connection, null, spec, ct);
    }

    public async Task<TaskSpec?> GetTaskSpecAsync(Guid taskId, CancellationToken ct = default)
    {
        await using var connection = await OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT payload FROM task_specs WHERE task_id = $taskId LIMIT 1;";
        command.Parameters.AddWithValue("$taskId", taskId.ToString());
        var payload = await command.ExecuteScalarAsync(ct) as string;
        return payload is null ? null : ScenarioJson.Deserialize<TaskSpec>(payload);
    }

    public async Task UpdateTaskStatusAsync(Guid taskId, OrchestratorTaskStatus status, CancellationToken ct = default)
    {
        var existing = await GetTaskSpecAsync(taskId, ct);
        if (existing is null)
        {
            return;
        }

        var updated = existing with
        {
            Status = status,
            StartedAt = status == OrchestratorTaskStatus.Running
                ? existing.StartedAt ?? DateTimeOffset.UtcNow
                : existing.StartedAt
        };

        await SaveTaskSpecAsync(updated, ct);
    }

    public async Task SaveResultAsync(TaskResult result, CancellationToken ct = default)
    {
        await using var connection = await OpenConnectionAsync(ct);
        await UpsertResultAsync(connection, null, result, ct);
    }

    public async Task<TaskResult?> GetResultAsync(Guid taskId, CancellationToken ct = default)
    {
        await using var connection = await OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT payload FROM results WHERE task_id = $taskId LIMIT 1;";
        command.Parameters.AddWithValue("$taskId", taskId.ToString());
        var payload = await command.ExecuteScalarAsync(ct) as string;
        return payload is null ? null : ScenarioJson.Deserialize<TaskResult>(payload);
    }

    public async Task<IReadOnlyList<TaskResult>> GetPlanResultsAsync(Guid planId, CancellationToken ct = default)
    {
        await using var connection = await OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT payload FROM results WHERE plan_id = $planId ORDER BY completed_at, task_id;";
        command.Parameters.AddWithValue("$planId", planId.ToString());

        var results = new List<TaskResult>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var payload = reader.GetString(0);
            var result = ScenarioJson.Deserialize<TaskResult>(payload);
            if (result is not null)
            {
                results.Add(result);
            }
        }

        return results;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken ct)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }

    private static async Task UpsertPlanAsync(SqliteConnection connection, SqliteTransaction? transaction, PlanState plan, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO plans(plan_id, status, updated_at, payload)
            VALUES ($planId, $status, $updatedAt, $payload)
            ON CONFLICT(plan_id) DO UPDATE SET
                status = excluded.status,
                updated_at = excluded.updated_at,
                payload = excluded.payload;
            """;
        command.Parameters.AddWithValue("$planId", plan.PlanId.ToString());
        command.Parameters.AddWithValue("$status", (int)plan.Status);
        command.Parameters.AddWithValue("$updatedAt", plan.UpdatedAt.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue("$payload", ScenarioJson.Serialize(plan));
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task UpsertTaskSpecAsync(SqliteConnection connection, SqliteTransaction? transaction, TaskSpec spec, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO task_specs(task_id, plan_id, status, started_at, payload)
            VALUES ($taskId, $planId, $status, $startedAt, $payload)
            ON CONFLICT(task_id) DO UPDATE SET
                plan_id = excluded.plan_id,
                status = excluded.status,
                started_at = excluded.started_at,
                payload = excluded.payload;
            """;
        command.Parameters.AddWithValue("$taskId", spec.TaskId.ToString());
        command.Parameters.AddWithValue("$planId", spec.PlanId.ToString());
        command.Parameters.AddWithValue("$status", (int)spec.Status);
        command.Parameters.AddWithValue("$startedAt", spec.StartedAt?.UtcDateTime.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$payload", ScenarioJson.Serialize(spec));
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task UpsertResultAsync(SqliteConnection connection, SqliteTransaction? transaction, TaskResult result, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO results(task_id, plan_id, completed_at, payload)
            VALUES ($taskId, $planId, $completedAt, $payload)
            ON CONFLICT(task_id) DO UPDATE SET
                plan_id = excluded.plan_id,
                completed_at = excluded.completed_at,
                payload = excluded.payload;
            """;
        command.Parameters.AddWithValue("$taskId", result.TaskId.ToString());
        command.Parameters.AddWithValue("$planId", result.PlanId.ToString());
        command.Parameters.AddWithValue("$completedAt", result.CompletedAt.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue("$payload", ScenarioJson.Serialize(result));
        await command.ExecuteNonQueryAsync(ct);
    }
}

internal sealed class RecordingPlanOrchestrator : IPlanOrchestrator
{
    private readonly ConcurrentBag<Guid> _resumedPlanIds = [];

    public IReadOnlyCollection<Guid> ResumedPlanIds => _resumedPlanIds.ToArray();

    public Task<Guid> StartAsync(string request, string sessionId, OrchestratorOptions? options = null, CancellationToken ct = default) =>
        Task.FromResult(Guid.NewGuid());

    public Task ResumeAsync(Guid planId, CancellationToken ct = default)
    {
        _resumedPlanIds.Add(planId);
        return Task.CompletedTask;
    }

    public Task PauseAsync(Guid planId, CancellationToken ct = default) => Task.CompletedTask;

    public Task CancelAsync(Guid planId, CancellationToken ct = default) => Task.CompletedTask;

    public Task<PlanState?> GetStateAsync(Guid planId, CancellationToken ct = default) => Task.FromResult<PlanState?>(null);

    public async IAsyncEnumerable<OrchestratorEvent> WatchAsync(Guid planId, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.CompletedTask;
        yield break;
    }
}