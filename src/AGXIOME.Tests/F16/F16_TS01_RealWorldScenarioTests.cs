using System.Diagnostics;
using System.Text.Json;
using AGXIOME.Orchestration.Abstractions;
using AGXIOME.Orchestration.Infrastructure;
using AGXIOME.Orchestration.Models;
using AGXIOME.Orchestration.Orchestrator;
using AGXIOME.Runtime.Hubs;
using AGXIOME.Storage.Stores;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using Npgsql;
using StackExchange.Redis;
using Microsoft.Extensions.Configuration;

namespace AGXIOME.Tests.F16;

[Collection(RealInfraCollection.CollectionName)]
public sealed class F16_TS01_RealWorldScenarioTests
{
    private const string TestApiKey = "expected-key";
    private readonly RealInfraFixture _fixture;

    public F16_TS01_RealWorldScenarioTests(RealInfraFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Persistence_RedisAndSqliteScenario_SurvivesProcessRestart()
    {
        var sqlitePath = Path.Combine(Path.GetTempPath(), $"f16_restart_{Guid.NewGuid():N}.db");
        var planId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        await using (var first = await new RealWorldOrchestrationScenarioBuilder()
            .WithSqliteStore(sqlitePath)
            .WithRedisHotStore(_fixture.RedisConnectionString)
            .BuildAsync())
        {
            await first.SavePlanAsync(MakePlan(planId, taskId, OrchestratorPlanStatus.Running));
            var save = await first.SaveResultAsync(planId, taskId, "persisted-output", summary: "saved once");

            Assert.True(save.Success);
            Assert.True(await first.HotStore.ExistsAsync(HotKeys.TaskResult(taskId)));
        }

        await using var second = await new RealWorldOrchestrationScenarioBuilder()
            .WithSqliteStore(sqlitePath)
            .WithRedisHotStore(_fixture.RedisConnectionString)
            .BuildAsync();

        var restoredPlan = await second.ColdStore.GetPlanAsync(planId);
        var restoredResult = await second.ColdStore.GetResultAsync(taskId);
        var restoredHotPayload = await second.HotStore.GetAsync(HotKeys.TaskResult(taskId));

        Assert.NotNull(restoredPlan);
        Assert.NotNull(restoredResult);
        Assert.NotNull(restoredHotPayload);
        Assert.Equal(OrchestratorPlanStatus.Running, restoredPlan!.Status);
        Assert.Equal("persisted-output", restoredResult!.Output);
        Assert.Contains("persisted-output", restoredHotPayload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Persistence_ResumeService_ResumesRunningPlanFromSqliteStore()
    {
        await using var scenario = await new RealWorldOrchestrationScenarioBuilder()
            .WithSqliteStore()
            .BuildAsync();

        var planId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        await scenario.SavePlanAsync(MakePlan(planId, taskId, OrchestratorPlanStatus.Running));

        var recorder = new RecordingPlanOrchestrator();
        using var host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddSingleton<IPlanOrchestrator>(recorder);
                services.AddSingleton<IOrchestrationStore>(scenario.ColdStore);
                services.AddHostedService<PlanResumeService>();
            })
            .Build();

        await host.StartAsync();
        await host.StopAsync();

        Assert.Contains(planId, recorder.ResumedPlanIds);
    }

    [Fact]
    public async Task Persistence_PostgresCommandStore_WritesUpdatesAndDeletesAgainstRealContainer()
    {
        await using var connection = new NpgsqlConnection(_fixture.PostgresConnectionString);
        await connection.OpenAsync();

        await using (var create = connection.CreateCommand())
        {
            create.CommandText = """
                CREATE TABLE IF NOT EXISTS pgproberecords (
                    id text PRIMARY KEY,
                    value integer NOT NULL
                );
                """;
            await create.ExecuteNonQueryAsync();
        }

        var store = new PostgresCommandStore(_fixture.PostgresConnectionString);
        await store.SaveAsync(new PgProbeRecord { Id = "pg-1", Value = 10 });
        await store.UpdateAsync(new PgProbeRecord { Id = "pg-1", Value = 99 });

        await using (var count = connection.CreateCommand())
        {
            count.CommandText = "SELECT value FROM pgproberecords WHERE id = 'pg-1';";
            var value = (int)(await count.ExecuteScalarAsync() ?? -1);
            Assert.Equal(99, value);
        }

        await store.DeleteAsync<PgProbeRecord>("pg-1");

        await using var verifyDelete = connection.CreateCommand();
        verifyDelete.CommandText = "SELECT COUNT(*) FROM pgproberecords WHERE id = 'pg-1';";
        var remaining = (long)(await verifyDelete.ExecuteScalarAsync() ?? -1L);
        Assert.Equal(0L, remaining);
    }

    [Fact]
    public async Task Concurrency_DuplicateNotifications_AreIgnoredAfterFirstCompletion()
    {
        await using var scenario = await new RealWorldOrchestrationScenarioBuilder()
            .WithSqliteStore()
            .WithRedisHotStore(_fixture.RedisConnectionString)
            .BuildAsync();

        var planId = Guid.NewGuid();
        var firstTask = Guid.NewGuid();
        var secondTask = Guid.NewGuid();

        var pollTask = scenario.PollAsync(planId, [firstTask, secondTask], timeoutSeconds: 5);

        await scenario.SaveResultAsync(planId, firstTask, "task-1");
        await scenario.NotifyAsync(planId, firstTask);
        await scenario.NotifyAsync(planId, firstTask);

        await scenario.SaveResultAsync(planId, secondTask, "task-2");
        await scenario.NotifyAsync(planId, secondTask);

        var poll = await pollTask;
        var results = await scenario.ColdStore.GetPlanResultsAsync(planId);
        var taskIds = results.Select(static result => result.TaskId).OrderBy(static id => id).ToArray();
        var expectedTaskIds = new[] { firstTask, secondTask }.OrderBy(static id => id).ToArray();

        Assert.True(poll.Success);
        Assert.Equal(2, results.Count);
        Assert.Equal(expectedTaskIds, taskIds);
    }

    [Fact]
    public async Task Concurrency_RedisPubSub_PreservesPublishedOrder()
    {
        await using var scenario = await new RealWorldOrchestrationScenarioBuilder()
            .WithRedisHotStore(_fixture.RedisConnectionString)
            .BuildAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var receivedTask = Task.Run(async () =>
        {
            var received = new List<string>();
            await foreach (var message in scenario.HotStore.SubscribeAsync("f16:pubsub", cts.Token))
            {
                received.Add(message);
                if (received.Count == 3)
                {
                    break;
                }
            }
            return received;
        }, cts.Token);

        await Task.Delay(250, cts.Token);
        await scenario.HotStore.PublishAsync("f16:pubsub", "1", cts.Token);
        await scenario.HotStore.PublishAsync("f16:pubsub", "2", cts.Token);
        await scenario.HotStore.PublishAsync("f16:pubsub", "3", cts.Token);

        var received = await receivedTask;
        Assert.Equal(["1", "2", "3"], received);
    }

    [Fact]
    public async Task FailureInjection_PollTimeout_ReturnsPartialResultsAndTimedOutTasks()
    {
        await using var scenario = await new RealWorldOrchestrationScenarioBuilder()
            .WithSqliteStore()
            .BuildAsync();

        var planId = Guid.NewGuid();
        var completedTask = Guid.NewGuid();
        var timedOutTask = Guid.NewGuid();

        var pollTask = scenario.PollAsync(planId, [completedTask, timedOutTask], timeoutSeconds: 1);

        await scenario.SaveResultAsync(planId, completedTask, "ready");
        await scenario.NotifyAsync(planId, completedTask);

        var poll = await pollTask;
        var payload = ScenarioJson.ToElement(poll.Data);
        var timedOutTasks = payload.GetProperty("timedOutTasks").EnumerateArray().Select(static item => item.GetString()).ToArray();
        var allComplete = payload.GetProperty("allComplete").GetBoolean();

        Assert.True(poll.Success);
        Assert.False(allComplete);
        Assert.Single(timedOutTasks);
        Assert.Equal(timedOutTask.ToString(), timedOutTasks[0]);
    }

    [Fact]
    public async Task FailureInjection_PollHonorsParentCancellation()
    {
        await using var scenario = await new RealWorldOrchestrationScenarioBuilder()
            .WithSqliteStore()
            .BuildAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        var planId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            scenario.PollAsync(planId, [taskId], timeoutSeconds: 30, cts.Token));
    }

    [Fact]
    public async Task FailureInjection_SqliteFileLock_IsObservableUnderRealContention()
    {
        var sqlitePath = Path.Combine(Path.GetTempPath(), $"f16_lock_{Guid.NewGuid():N}.db");
        await using var scenario = await new RealWorldOrchestrationScenarioBuilder()
            .WithSqliteStore(sqlitePath)
            .BuildAsync();

        await using var lockingConnection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = sqlitePath,
            Pooling = false,
            Mode = SqliteOpenMode.ReadWriteCreate,
            DefaultTimeout = 1
        }.ToString());
        await lockingConnection.OpenAsync();
        await using var command = lockingConnection.CreateCommand();
        command.CommandText = "BEGIN EXCLUSIVE;";
        await command.ExecuteNonQueryAsync();

        var ex = await Record.ExceptionAsync(() => scenario.ColdStore.SaveResultAsync(new TaskResult
        {
            TaskId = Guid.NewGuid(),
            PlanId = Guid.NewGuid(),
            Success = true,
            Output = "lock-test"
        }));

        await using var release = lockingConnection.CreateCommand();
        release.CommandText = "ROLLBACK;";
        await release.ExecuteNonQueryAsync();

        Assert.NotNull(ex);
        Assert.IsType<SqliteException>(ex);
    }

    [Fact]
    public async Task FailureInjection_HitlPause_PersistsWaitingStateUntilResolution()
    {
        await using var scenario = await new RealWorldOrchestrationScenarioBuilder()
            .WithSqliteStore()
            .WithRedisHotStore(_fixture.RedisConnectionString)
            .BuildAsync();

        var notifier = Assert.IsType<InMemoryCompletionNotifier>(scenario.Notifier);
        var planId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var requirementId = $"hitl-{Guid.NewGuid():N}";

        await scenario.SavePlanAsync(MakePlan(planId, taskId, OrchestratorPlanStatus.Running));

        var pollTask = scenario.PollAsync(planId, [taskId], timeoutSeconds: 5);

        await notifier.PublishAsync(new TaskResult
        {
            TaskId = taskId,
            PlanId = planId,
            Success = true,
            Output = "waiting for human approval",
            Summary = "paused for HITL",
            IsHitlPaused = true,
            HitlRequirementId = requirementId
        });

        var deadline = DateTime.UtcNow.AddSeconds(5);
        string? hotPlanStatus = null;
        while (DateTime.UtcNow < deadline)
        {
            hotPlanStatus = await scenario.HotStore.GetAsync(HotKeys.PlanStatus(planId));
            if (string.Equals(hotPlanStatus, OrchestratorPlanStatus.WaitingForHITL.ToString(), StringComparison.Ordinal))
            {
                break;
            }

            await Task.Delay(50);
        }

        var persistedPlan = await scenario.ColdStore.GetPlanAsync(planId);

        Assert.Equal(OrchestratorPlanStatus.WaitingForHITL.ToString(), hotPlanStatus);
        Assert.Equal(OrchestratorPlanStatus.WaitingForHITL, persistedPlan?.Status);
        Assert.False(pollTask.IsCompleted);

        await notifier.NotifyResolvedAsync(taskId, planId, requirementId, approved: true);

        var poll = await pollTask;
        var resumedResult = await scenario.ColdStore.GetResultAsync(taskId);

        Assert.True(poll.Success);
        Assert.NotNull(resumedResult);
        Assert.False(resumedResult!.IsHitlPaused);
        Assert.Equal(requirementId, resumedResult.HitlRequirementId);
        Assert.True(resumedResult.Success);
    }

    [Fact]
    public async Task Realtime_RuntimeSignalR_GroupFanout_ReachesJoinedPlanClient()
    {
        using var factory = CreateAuthenticatedRuntimeFactory();
        var planId = Guid.NewGuid().ToString();
        var startedPlans = new List<string>();

        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress!, "/hubs/agent"), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.Headers["X-Api-Key"] = TestApiKey;
            })
            .Build();

        connection.On<string>("OnPlanStarted", payload => startedPlans.Add(payload));

        await connection.StartAsync();
        await connection.InvokeAsync("JoinPlan", planId);

        var hubContext = factory.Services.GetRequiredService<IHubContext<AgentHub>>();
        await hubContext.Clients.Group($"plan:{planId}").SendAsync("OnPlanStarted", planId);

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (startedPlans.Count == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }

        await connection.DisposeAsync();
        Assert.Contains(planId, startedPlans);
    }

    [Fact]
    public async Task Realtime_RuntimeSignalR_RequestReplay_ReplaysRedisBufferedEventsAfterReconnect()
    {
        using var factory = new ReplayEnabledRuntimeFactory(_fixture.RedisConnectionString);
        var runId = $"run-{Guid.NewGuid():N}";
        var replayed = new List<AgentRunEvent>();
        var replayStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var replayCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var firstConnection = CreateAgentConnection(factory);
        await firstConnection.StartAsync();
        await firstConnection.InvokeAsync("StartRun", runId, "agent-a");
        await firstConnection.InvokeAsync("CancelRun", runId);
        await firstConnection.InvokeAsync("ResumeRun", runId);
        await firstConnection.DisposeAsync();

        var secondConnection = CreateAgentConnection(factory);
        secondConnection.On<object>("ReplayStart", _ => replayStarted.TrySetResult());
        secondConnection.On<AgentRunEvent>("ReplayEvent", evt => replayed.Add(evt));
        secondConnection.On<object>("ReplayCompleted", _ => replayCompleted.TrySetResult());

        await secondConnection.StartAsync();
        await secondConnection.InvokeAsync("RequestReplay", runId, 10);

        await replayStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await replayCompleted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await secondConnection.DisposeAsync();

        Assert.Equal(3, replayed.Count);
        Assert.Equal(["started", "cancelled", "resumed"], replayed.Select(static evt => evt.Type).ToArray());
        Assert.True(replayed.Zip(replayed.Skip(1)).All(static pair => pair.First.Seq < pair.Second.Seq));
        Assert.All(replayed, evt => Assert.Equal(runId, evt.RunId));
    }

    [Fact]
    public async Task Load_RealStores_SaveResultsMaintainP95AndNoLoss()
    {
        await using var scenario = await new RealWorldOrchestrationScenarioBuilder()
            .WithSqliteStore()
            .WithRedisHotStore(_fixture.RedisConnectionString)
            .BuildAsync();

        var planId = Guid.NewGuid();
        var durations = new List<double>(capacity: 64);
        var taskIds = Enumerable.Range(0, 64).Select(_ => Guid.NewGuid()).ToArray();

        foreach (var taskId in taskIds)
        {
            var sw = Stopwatch.StartNew();
            var save = await scenario.SaveResultAsync(planId, taskId, $"payload:{taskId:N}", elapsedMs: 10);
            sw.Stop();

            Assert.True(save.Success);
            durations.Add(sw.Elapsed.TotalMilliseconds);
        }

        var persisted = await scenario.ColdStore.GetPlanResultsAsync(planId);
        durations.Sort();
        var p95 = durations[(int)Math.Ceiling(durations.Count * 0.95) - 1];

        Assert.Equal(taskIds.Length, persisted.Count);
        Assert.True(p95 < 150, $"Expected p95 < 150ms for 64 real-store saves, observed {p95:F2}ms.");
    }

    [Fact]
    public async Task Load_RedisIncrement_ConcurrentWriters_DoNotLoseUpdates()
    {
        await using var scenario = await new RealWorldOrchestrationScenarioBuilder()
            .WithRedisHotStore(_fixture.RedisConnectionString)
            .BuildAsync();

        var key = $"f16:counter:{Guid.NewGuid():N}";
        var increments = Enumerable.Range(0, 256)
            .Select(_ => scenario.HotStore.IncrementAsync(key))
            .ToArray();

        await Task.WhenAll(increments);
        var finalValue = await scenario.HotStore.GetAsync(key);

        Assert.Equal("256", finalValue);
    }

    [Fact]
    public async Task Load_BurstConcurrentSaveNotifyPoll_CompletesWithoutLoss()
    {
        await using var scenario = await new RealWorldOrchestrationScenarioBuilder()
            .WithSqliteStore()
            .WithRedisHotStore(_fixture.RedisConnectionString)
            .BuildAsync();

        var planId = Guid.NewGuid();
        var taskIds = Enumerable.Range(0, 32).Select(_ => Guid.NewGuid()).ToArray();
        await scenario.SavePlanAsync(MakePlan(planId, taskIds[0], OrchestratorPlanStatus.Running) with
        {
            Tasks = taskIds.Select(taskId => new TaskSpec
            {
                TaskId = taskId,
                PlanId = planId,
                StepIndex = 0,
                Description = $"Burst task {taskId:N}"
            }).ToArray()
        });

        var sw = Stopwatch.StartNew();
        var pollTask = scenario.PollAsync(planId, taskIds, timeoutSeconds: 10);

        await Task.WhenAll(taskIds.Select(async taskId =>
        {
            var save = await scenario.SaveResultAsync(planId, taskId, $"burst:{taskId:N}", elapsedMs: 5);
            Assert.True(save.Success);
            var notify = await scenario.NotifyAsync(planId, taskId);
            Assert.True(notify.Success);
        }));

        var poll = await pollTask;
        sw.Stop();

        var payload = ScenarioJson.ToElement(poll.Data);
        var allComplete = payload.GetProperty("allComplete").GetBoolean();
        var timedOutTasks = payload.GetProperty("timedOutTasks").EnumerateArray().ToArray();
        var persisted = await scenario.ColdStore.GetPlanResultsAsync(planId);

        Assert.True(poll.Success);
        Assert.True(allComplete);
        Assert.Empty(timedOutTasks);
        Assert.Equal(taskIds.Length, persisted.Count);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), $"Expected burst completion under 5s, observed {sw.Elapsed.TotalSeconds:F2}s.");
    }

    private static PlanState MakePlan(Guid planId, Guid taskId, OrchestratorPlanStatus status) => new()
    {
        PlanId = planId,
        OriginalRequest = "real-world persistence probe",
        SessionId = "session-f16",
        Tasks = [new TaskSpec
        {
            TaskId = taskId,
            PlanId = planId,
            StepIndex = 0,
            Description = "Probe task"
        }],
        CurrentStep = 0,
        Status = status
    };

    private static HubConnection CreateAgentConnection(WebApplicationFactory<Program> factory) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress!, "/hubs/agent"), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.Headers["X-Api-Key"] = TestApiKey;
            })
            .Build();

    private static WebApplicationFactory<Program> CreateAuthenticatedRuntimeFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AGXIOME_BOOTSTRAP_KEY"] = TestApiKey
                });
            });
        });

    private sealed class PgProbeRecord
    {
        public string Id { get; init; } = string.Empty;
        public int Value { get; init; }
    }

    private sealed class ReplayEnabledRuntimeFactory : WebApplicationFactory<Program>
    {
        private readonly string _redisConnectionString;

        public ReplayEnabledRuntimeFactory(string redisConnectionString)
        {
            _redisConnectionString = redisConnectionString;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AGXIOME_BOOTSTRAP_KEY"] = TestApiKey,
                    ["ConnectionStrings:Redis"] = _redisConnectionString
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAgentEventReplayStore>();
                services.RemoveAll<IConnectionMultiplexer>();
                services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(_redisConnectionString));
                services.AddSingleton<IAgentEventReplayStore, RedisAgentEventReplayStore>();
            });
        }
    }
}