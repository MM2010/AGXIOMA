using AGXIOME.Core.Agent;
using AGXIOME.Core.Storage;
using AGXIOME.Shared.Config;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using AGXIOME.Storage.Stores;
using NSubstitute;
using StackExchange.Redis;
using Xunit;

namespace AGXIOME.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// F7 — Storage & Session Enhancement Tests (SS-01..SS-10)
//
// Strategy:
//   • SS-01 RedisSessionManager: 4 runnable (null-guard, options, mock roundtrip)
//     + 3 skipped (require live Redis). IConnectionMultiplexer is NSubstituted;
//     IDatabase operations are mocked per-test.
//   • SS-02..SS-10: will be added as implementations are completed.
//
// Total target: 70 tests (7 × 10 tasks).
// ─────────────────────────────────────────────────────────────────────────────

// ── SS-01 — Redis Session Manager ─────────────────────────────────────────────

public class RedisSessionManagerTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a mock IDatabase that returns default/empty values for all
    /// calls needed by CreateSession and GetSession flows.
    /// </summary>
    private static (IConnectionMultiplexer mux, IDatabase db) MakeMocks()
    {
        var db  = Substitute.For<IDatabase>();
        var mux = Substitute.For<IConnectionMultiplexer>();
        mux.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(db);

        // StringSetAsync — always succeeds (Expiration-based overload, Redis 2.13.1+)
        db.StringSetAsync(
            Arg.Any<RedisKey>(), Arg.Any<RedisValue>(),
            Arg.Any<Expiration>(),
            Arg.Any<ValueCondition>(), Arg.Any<CommandFlags>())
            .Returns(true);

        // StringGetAsync — return empty by default (no session stored)
        db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
           .Returns(RedisValue.Null);

        // SortedSetAddAsync
        db.SortedSetAddAsync(
            Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<double>(),
            Arg.Any<SortedSetWhen>(), Arg.Any<CommandFlags>())
           .Returns(true);

        // KeyExpireAsync
        db.KeyExpireAsync(Arg.Any<RedisKey>(), Arg.Any<TimeSpan?>(), Arg.Any<ExpireWhen>(), Arg.Any<CommandFlags>())
          .Returns(true);

        // ListRangeAsync — return empty list
        db.ListRangeAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CommandFlags>())
          .Returns(System.Array.Empty<RedisValue>());

        // SortedSetRangeByRankAsync — return empty list
        db.SortedSetRangeByRankAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<Order>(), Arg.Any<CommandFlags>())
          .Returns(System.Array.Empty<RedisValue>());

        return (mux, db);
    }

    // ── Test 1: null connection string ────────────────────────────────────────

    [Fact]
    public void Constructor_NullConnectionString_Throws()
        => Assert.Throws<ArgumentNullException>(() => new RedisSessionManager((string)null!));

    // ── Test 2: null multiplexer ──────────────────────────────────────────────

    [Fact]
    public void Constructor_NullMultiplexer_Throws()
        => Assert.Throws<ArgumentNullException>(() => new RedisSessionManager((IConnectionMultiplexer)null!));

    // ── Test 3: options defaults ──────────────────────────────────────────────

    [Fact]
    public void Options_DefaultValues_AreCorrect()
    {
        var opts = new RedisSessionManagerOptions();
        Assert.Equal("agxioma:sm", opts.KeyPrefix);
        Assert.Equal(TimeSpan.FromHours(24), opts.SessionTtl);
        Assert.Equal(1000, opts.MaxHistoryMessages);
    }

    // ── Test 4: CreateSession returns valid session ───────────────────────────

    [Fact]
    public async Task CreateSession_ReturnsSessionWithCorrectFields()
    {
        var (mux, db) = MakeMocks();
        using var mgr = new RedisSessionManager(mux);

        var session = await mgr.CreateSessionAsync("user1", "agent1", SessionType.Agent);

        Assert.Equal("user1",        session.UserId);
        Assert.Equal("agent1",       session.ComponentId);
        Assert.Equal(SessionType.Agent, session.Type);
        Assert.False(string.IsNullOrEmpty(session.Id));
        Assert.True(session.CreatedAt <= DateTime.UtcNow);

        // Verify session was written to Redis
        await db.Received().StringSetAsync(
            Arg.Any<RedisKey>(), Arg.Any<RedisValue>(),
            Arg.Any<Expiration>(),
            Arg.Any<ValueCondition>(), Arg.Any<CommandFlags>());
    }

    // ── Test 5: GetSession returns null when not found ────────────────────────

    [Fact]
    public async Task GetSession_NotFound_ReturnsNull()
    {
        var (mux, _) = MakeMocks();
        using var mgr = new RedisSessionManager(mux);

        var result = await mgr.GetSessionAsync("nonexistent-id");

        Assert.Null(result);
    }

    // ── Test 6: Dispose does not throw ────────────────────────────────────────

    [Fact]
    public void Dispose_WithInjectedMux_DoesNotThrow()
    {
        // When mux is injected, Dispose must be idempotent and must NOT
        // dispose the caller-owned multiplexer.
        var (mux, _) = MakeMocks();
        var mgr = new RedisSessionManager(mux);
        mgr.Dispose();
        mgr.Dispose(); // second call must not throw
        mux.DidNotReceive().Dispose(); // injected mux is not owned
    }

    // ── Test 7: live Redis (skipped without server) ───────────────────────────

    [Fact(Skip = "requires live Redis server on localhost:6379")]
    public async Task CreateAndGet_RoundTrip_LiveRedis()
    {
        using var mgr = new RedisSessionManager(
            "localhost:6379,abortConnect=false",
            new RedisSessionManagerOptions { SessionTtl = TimeSpan.FromMinutes(1) });

        var created = await mgr.CreateSessionAsync("u1", "c1", SessionType.Agent);
        var fetched = await mgr.GetSessionAsync(created.Id);

        Assert.NotNull(fetched);
        Assert.Equal(created.Id,     fetched!.Id);
        Assert.Equal(created.UserId, fetched.UserId);
    }
}

// ── SS-02 — Agent Storage Bridge ──────────────────────────────────────────────

public class AgentStorageBridgeTests
{
    // Helper: build a minimal AgentSnapshot with a fixed ID.
    private static AgentSnapshot MakeSnapshot(string id = "agent-01")
        => new AgentSnapshot
        {
            Id     = id,
            Name   = $"TestAgent-{id}",
            Config = new AgentConfig { Id = id },
        };

    // Helper: build a mock ICommandStore that silently accepts all calls.
    private static ICommandStore MakeCommandStore()
    {
        var store = Substitute.For<ICommandStore>();
        store.SaveAsync(Arg.Any<AgentStorageEntry>(), Arg.Any<CancellationToken>())
             .Returns(Task.CompletedTask);
        store.UpdateAsync(Arg.Any<AgentStorageEntry>(), Arg.Any<CancellationToken>())
             .Returns(Task.CompletedTask);
        store.DeleteAsync<AgentStorageEntry>(Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Task.CompletedTask);
        return store;
    }

    // ── Test 1: null command store ────────────────────────────────────────────

    [Fact]
    public void Constructor_NullCommandStore_Throws()
        => Assert.Throws<ArgumentNullException>(() => new AgentStorageBridge(null!));

    // ── Test 2: Save + Exists round-trip ─────────────────────────────────────

    [Fact]
    public async Task Save_ThenExists_ReturnsTrue()
    {
        var bridge = new AgentStorageBridge(MakeCommandStore());
        var snap   = MakeSnapshot();

        await bridge.SaveAsync(snap, userId: "u1");

        Assert.True(await bridge.ExistsAsync(snap.Id));
    }

    // ── Test 3: Save + Load round-trip ───────────────────────────────────────

    [Fact]
    public async Task Save_ThenLoad_ReturnsSnapshot()
    {
        var bridge = new AgentStorageBridge(MakeCommandStore());
        var snap   = MakeSnapshot("agent-rtrip");

        await bridge.SaveAsync(snap);
        var loaded = await bridge.LoadAsync(snap.Id);

        Assert.NotNull(loaded);
        Assert.Equal(snap.Id,   loaded!.Id);
        Assert.Equal(snap.Name, loaded.Name);
    }

    // ── Test 4: Load unknown ID returns null ──────────────────────────────────

    [Fact]
    public async Task Load_UnknownId_ReturnsNull()
    {
        var bridge = new AgentStorageBridge(MakeCommandStore());
        Assert.Null(await bridge.LoadAsync("does-not-exist"));
    }

    // ── Test 5: Delete removes entry ─────────────────────────────────────────

    [Fact]
    public async Task Delete_RemovesExistingAgent()
    {
        var bridge = new AgentStorageBridge(MakeCommandStore());
        var snap   = MakeSnapshot("agent-del");

        await bridge.SaveAsync(snap);
        await bridge.DeleteAsync(snap.Id);

        Assert.False(await bridge.ExistsAsync(snap.Id));
    }

    // ── Test 6: List returns saved entries ────────────────────────────────────

    [Fact]
    public async Task List_AfterSaves_ReturnsAllEntries()
    {
        var bridge = new AgentStorageBridge(MakeCommandStore());
        await bridge.SaveAsync(MakeSnapshot("a1"), userId: "user1");
        await bridge.SaveAsync(MakeSnapshot("a2"), userId: "user1");
        await bridge.SaveAsync(MakeSnapshot("a3"), userId: "user2");

        var allList   = await bridge.ListAsync();
        var user1List = await bridge.ListAsync(userId: "user1");

        Assert.Equal(3, allList.Count);
        Assert.Equal(2, user1List.Count);
        Assert.All(user1List, e => Assert.Equal("user1", e.UserId));
    }

    // ── Test 7: Update writes new snapshot ───────────────────────────────────

    [Fact]
    public async Task SaveTwice_UpdatesSnapshot()
    {
        var store  = MakeCommandStore();
        var bridge = new AgentStorageBridge(store);
        var snap   = MakeSnapshot("agent-upd");

        await bridge.SaveAsync(snap);
        var updated = snap with { Name = "UpdatedName" };
        await bridge.SaveAsync(updated);

        var loaded = await bridge.LoadAsync(snap.Id);
        Assert.Equal("UpdatedName", loaded!.Name);

        // First save: SaveAsync; second save: UpdateAsync.
        await store.Received(1).SaveAsync(Arg.Any<AgentStorageEntry>(), Arg.Any<CancellationToken>());
        await store.Received(1).UpdateAsync(Arg.Any<AgentStorageEntry>(), Arg.Any<CancellationToken>());
    }
}

// ── SS-03 — Team Storage Bridge ───────────────────────────────────────────────

public class TeamStorageBridgeTests
{
    // Helper: build a minimal TeamSnapshot with a fixed ID.
    private static TeamSnapshot MakeSnapshot(string id = "team-01")
        => new TeamSnapshot
        {
            Id       = id,
            Name     = $"TestTeam-{id}",
            Topology = TeamTopology.Router,
            Mode     = TeamMode.Coordinate,
            Config   = new TeamConfig { Name = $"TestTeam-{id}" },
            AgentConfigs = new[]
            {
                new AgentConfig { Id = "agent-a", Name = "AgentA" },
                new AgentConfig { Id = "agent-b", Name = "AgentB" },
            },
        };

    // Helper: mock ICommandStore that silently accepts all calls.
    private static ICommandStore MakeCommandStore()
    {
        var store = Substitute.For<ICommandStore>();
        store.SaveAsync(Arg.Any<TeamStorageEntry>(), Arg.Any<CancellationToken>())
             .Returns(Task.CompletedTask);
        store.UpdateAsync(Arg.Any<TeamStorageEntry>(), Arg.Any<CancellationToken>())
             .Returns(Task.CompletedTask);
        store.DeleteAsync<TeamStorageEntry>(Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Task.CompletedTask);
        return store;
    }

    // ── Test 1: null command store ────────────────────────────────────────────

    [Fact]
    public void Constructor_NullCommandStore_Throws()
        => Assert.Throws<ArgumentNullException>(() => new TeamStorageBridge(null!));

    // ── Test 2: Save + Exists round-trip ─────────────────────────────────────

    [Fact]
    public async Task Save_ThenExists_ReturnsTrue()
    {
        var bridge = new TeamStorageBridge(MakeCommandStore());
        var snap   = MakeSnapshot();

        await bridge.SaveAsync(snap, userId: "u1");

        Assert.True(await bridge.ExistsAsync(snap.Id));
    }

    // ── Test 3: Save + Load round-trip ───────────────────────────────────────

    [Fact]
    public async Task Save_ThenLoad_ReturnsSnapshot()
    {
        var bridge = new TeamStorageBridge(MakeCommandStore());
        var snap   = MakeSnapshot("team-rtrip");

        await bridge.SaveAsync(snap);
        var loaded = await bridge.LoadAsync(snap.Id);

        Assert.NotNull(loaded);
        Assert.Equal(snap.Id,   loaded!.Id);
        Assert.Equal(snap.Name, loaded.Name);
        Assert.Equal(snap.Topology, loaded.Topology);
        Assert.Equal(2, loaded.AgentConfigs.Count);
    }

    // ── Test 4: Load unknown ID returns null ──────────────────────────────────

    [Fact]
    public async Task Load_UnknownId_ReturnsNull()
    {
        var bridge = new TeamStorageBridge(MakeCommandStore());
        Assert.Null(await bridge.LoadAsync("does-not-exist"));
    }

    // ── Test 5: Delete removes entry ─────────────────────────────────────────

    [Fact]
    public async Task Delete_RemovesExistingTeam()
    {
        var bridge = new TeamStorageBridge(MakeCommandStore());
        var snap   = MakeSnapshot("team-del");

        await bridge.SaveAsync(snap);
        await bridge.DeleteAsync(snap.Id);

        Assert.False(await bridge.ExistsAsync(snap.Id));
    }

    // ── Test 6: List returns saved entries filtered by userId ─────────────────

    [Fact]
    public async Task List_AfterSaves_ReturnsAllAndFilteredEntries()
    {
        var bridge = new TeamStorageBridge(MakeCommandStore());
        await bridge.SaveAsync(MakeSnapshot("t1"), userId: "userA");
        await bridge.SaveAsync(MakeSnapshot("t2"), userId: "userA");
        await bridge.SaveAsync(MakeSnapshot("t3"), userId: "userB");

        var all    = await bridge.ListAsync();
        var userA  = await bridge.ListAsync(userId: "userA");

        Assert.Equal(3, all.Count);
        Assert.Equal(2, userA.Count);
        Assert.All(userA, e => Assert.Equal("userA", e.UserId));
    }

    // ── Test 7: Update writes new snapshot ───────────────────────────────────

    [Fact]
    public async Task SaveTwice_UpdatesSnapshot()
    {
        var store  = MakeCommandStore();
        var bridge = new TeamStorageBridge(store);
        var snap   = MakeSnapshot("team-upd");

        await bridge.SaveAsync(snap);
        var updated = snap with { Name = "UpdatedTeamName" };
        await bridge.SaveAsync(updated);

        var loaded = await bridge.LoadAsync(snap.Id);
        Assert.Equal("UpdatedTeamName", loaded!.Name);

        // First save: SaveAsync; second save: UpdateAsync.
        await store.Received(1).SaveAsync(Arg.Any<TeamStorageEntry>(), Arg.Any<CancellationToken>());
        await store.Received(1).UpdateAsync(Arg.Any<TeamStorageEntry>(), Arg.Any<CancellationToken>());
    }
}

// ── SS-04 — Workflow History Store ────────────────────────────────────────────

public class WorkflowHistoryStoreTests
{
    // Helper: build a WorkflowHistoryEntry for a given workflowId.
    private static WorkflowHistoryEntry MakeEntry(
        string workflowId = "wf-01", WorkflowStatus status = WorkflowStatus.Completed)
    {
        var steps = new List<StepResult>
        {
            new StepResult("Step1", StepStatus.Completed, "output-1", TimeSpan.FromMilliseconds(100)),
            new StepResult("Step2", StepStatus.Completed, "output-2", TimeSpan.FromMilliseconds(200)),
        };
        return WorkflowHistoryStore.FromResult(
            new WorkflowResult(workflowId, status, steps),
            startedAt:  DateTimeOffset.UtcNow.AddSeconds(-1),
            finishedAt: DateTimeOffset.UtcNow
        );
    }

    // Helper: silent ICommandStore mock.
    private static ICommandStore MakeCommandStore()
    {
        var store = Substitute.For<ICommandStore>();
        store.SaveAsync(Arg.Any<WorkflowHistoryEntry>(), Arg.Any<CancellationToken>())
             .Returns(Task.CompletedTask);
        store.DeleteAsync<WorkflowHistoryEntry>(Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Task.CompletedTask);
        return store;
    }

    // ── Test 1: null command store ────────────────────────────────────────────

    [Fact]
    public void Constructor_NullCommandStore_Throws()
        => Assert.Throws<ArgumentNullException>(() => new WorkflowHistoryStore(null!));

    // ── Test 2: Append + Get round-trip ──────────────────────────────────────

    [Fact]
    public async Task Append_ThenGet_ReturnsEntry()
    {
        var hist  = new WorkflowHistoryStore(MakeCommandStore());
        var entry = MakeEntry();

        await hist.AppendAsync(entry);
        var fetched = await hist.GetAsync(entry.RunId);

        Assert.NotNull(fetched);
        Assert.Equal(entry.RunId,      fetched!.RunId);
        Assert.Equal(entry.WorkflowId, fetched.WorkflowId);
        Assert.Equal(WorkflowStatus.Completed, fetched.Status);
    }

    // ── Test 3: Get unknown runId returns null ────────────────────────────────

    [Fact]
    public async Task Get_UnknownRunId_ReturnsNull()
    {
        var hist = new WorkflowHistoryStore(MakeCommandStore());
        Assert.Null(await hist.GetAsync("no-such-run"));
    }

    // ── Test 4: ListByWorkflow returns newest-first ───────────────────────────

    [Fact]
    public async Task ListByWorkflow_ReturnsNewestFirst()
    {
        var hist = new WorkflowHistoryStore(MakeCommandStore());
        var e1 = MakeEntry("wf-list");
        var e2 = MakeEntry("wf-list");
        var e3 = MakeEntry("wf-other");  // different workflow

        await hist.AppendAsync(e1);
        await hist.AppendAsync(e2);
        await hist.AppendAsync(e3);

        var result = await hist.ListByWorkflowAsync("wf-list");

        Assert.Equal(2, result.Count);
        // Newest-first: e2 appended after e1 so it should be first.
        Assert.Equal(e2.RunId, result[0].RunId);
        Assert.Equal(e1.RunId, result[1].RunId);
    }

    // ── Test 5: GetStep returns correct step ──────────────────────────────────

    [Fact]
    public async Task GetStep_ExistingStep_ReturnsResult()
    {
        var hist  = new WorkflowHistoryStore(MakeCommandStore());
        var entry = MakeEntry();

        await hist.AppendAsync(entry);
        var step = await hist.GetStepAsync(entry.RunId, "Step1");

        Assert.NotNull(step);
        Assert.Equal("Step1", step!.StepName);
        Assert.Equal(StepStatus.Completed, step.Status);
    }

    // ── Test 6: GetStep for missing step returns null ─────────────────────────

    [Fact]
    public async Task GetStep_MissingStep_ReturnsNull()
    {
        var hist  = new WorkflowHistoryStore(MakeCommandStore());
        var entry = MakeEntry();

        await hist.AppendAsync(entry);
        var step = await hist.GetStepAsync(entry.RunId, "NoSuchStep");

        Assert.Null(step);
    }

    // ── Test 7: Delete removes entry and workflow index ───────────────────────

    [Fact]
    public async Task Delete_RemovesRunAndWorkflowIndex()
    {
        var hist  = new WorkflowHistoryStore(MakeCommandStore());
        var entry = MakeEntry("wf-del");

        await hist.AppendAsync(entry);
        await hist.DeleteAsync(entry.RunId);

        Assert.Null(await hist.GetAsync(entry.RunId));

        var list = await hist.ListByWorkflowAsync("wf-del");
        Assert.Empty(list);
    }
}

// ── SS-05 — Workflow Serialization ────────────────────────────────────────────

public class WorkflowSerializerTests
{
    private static WorkflowDefinition MakeDefinition(string id = "wf-def-01")
        => new WorkflowDefinition
        {
            Id        = id,
            Name      = $"Workflow-{id}",
            EntryStep = "step-a",
            Steps = new[]
            {
                new WorkflowStepDescriptor { Name = "step-a", Type = StepType.Agent, HandlerRef = "agent-x" },
                new WorkflowStepDescriptor { Name = "step-b", Type = StepType.Function, HandlerRef = "func-y" },
            },
            Transitions = new[]
            {
                new WorkflowTransitionDescriptor { From = "step-a", To = "step-b" },
            },
        };

    // ── Test 1: Serialize produces non-empty JSON ──────────────────────────────

    [Fact]
    public void Serialize_ProducesJson()
    {
        var def  = MakeDefinition();
        var json = WorkflowSerializer.Serialize(def);
        Assert.False(string.IsNullOrWhiteSpace(json));
        Assert.Contains("step-a", json);
    }

    // ── Test 2: Deserialize restores fields ───────────────────────────────────

    [Fact]
    public void Deserialize_RestoresDefinition()
    {
        var def  = MakeDefinition("roundtrip");
        var json = WorkflowSerializer.Serialize(def);
        var back = WorkflowSerializer.Deserialize(json);

        Assert.Equal(def.Id,        back.Id);
        Assert.Equal(def.Name,      back.Name);
        Assert.Equal(def.EntryStep, back.EntryStep);
        Assert.Equal(2, back.Steps.Count);
        Assert.Single(back.Transitions);
    }

    // ── Test 3: StepType enum round-trip ──────────────────────────────────────

    [Fact]
    public void Serialize_EnumRoundTrip_StepType()
    {
        var def  = MakeDefinition();
        var json = WorkflowSerializer.Serialize(def);
        var back = WorkflowSerializer.Deserialize(json);
        Assert.Equal(StepType.Agent,    back.Steps[0].Type);
        Assert.Equal(StepType.Function, back.Steps[1].Type);
    }

    // ── Test 4: Null definition throws ────────────────────────────────────────

    [Fact]
    public void Serialize_NullDefinition_Throws()
        => Assert.Throws<ArgumentNullException>(() => WorkflowSerializer.Serialize(null!));

    // ── Test 5: Empty json throws ─────────────────────────────────────────────

    [Fact]
    public void Deserialize_EmptyJson_Throws()
        => Assert.Throws<ArgumentException>(() => WorkflowSerializer.Deserialize(""));

    // ── Test 6: FromEntry returns null for null entry ─────────────────────────

    [Fact]
    public void FromEntry_Null_ReturnsNull()
        => Assert.Null(WorkflowSerializer.FromEntry(null));

    // ── Test 7: SaveAsync calls SaveAsync on store ────────────────────────────

    [Fact]
    public async Task SaveAsync_CallsCommandStore()
    {
        var store = Substitute.For<ICommandStore>();
        store.SaveAsync(Arg.Any<WorkflowDefinitionEntry>(), Arg.Any<CancellationToken>())
             .Returns(Task.CompletedTask);

        await WorkflowSerializer.SaveAsync(MakeDefinition(), store);

        await store.Received(1).SaveAsync(
            Arg.Any<WorkflowDefinitionEntry>(), Arg.Any<CancellationToken>());
    }
}

// ── SS-06 — Workflow HITL Store ───────────────────────────────────────────────

public class WorkflowHitlStoreTests
{
    private static ICommandStore MakeCommandStore()
    {
        var store = Substitute.For<ICommandStore>();
        store.SaveAsync(Arg.Any<WorkflowPauseRecord>(), Arg.Any<CancellationToken>())
             .Returns(Task.CompletedTask);
        store.UpdateAsync(Arg.Any<WorkflowPauseRecord>(), Arg.Any<CancellationToken>())
             .Returns(Task.CompletedTask);
        return store;
    }

    // ── Test 1: null command store throws ────────────────────────────────────

    [Fact]
    public void Constructor_NullCommandStore_Throws()
        => Assert.Throws<ArgumentNullException>(() => new WorkflowHitlStore(null!));

    // ── Test 2: Pause creates Pending checkpoint ──────────────────────────────

    [Fact]
    public async Task Pause_CreatesPendingCheckpoint()
    {
        var store = new WorkflowHitlStore(MakeCommandStore());
        var record = await store.PauseAsync(
            "run-1", "wf-1", "StepApprove",
            HITLRequirementType.Confirmation, new { Amount = 100 });

        Assert.Equal(HITLStatus.Pending, record.Status);
        Assert.Equal("run-1",        record.RunId);
        Assert.Equal("StepApprove",  record.StepName);
        Assert.False(string.IsNullOrEmpty(record.CheckpointId));
    }

    // ── Test 3: Get returns stored checkpoint ────────────────────────────────

    [Fact]
    public async Task Get_ReturnsPausedCheckpoint()
    {
        var store  = new WorkflowHitlStore(MakeCommandStore());
        var paused = await store.PauseAsync("run-2", "wf-2", "S1",
            HITLRequirementType.UserInput, "input needed");

        var fetched = await store.GetAsync(paused.CheckpointId);
        Assert.NotNull(fetched);
        Assert.Equal(paused.CheckpointId, fetched!.CheckpointId);
    }

    // ── Test 4: Resume marks Approved ────────────────────────────────────────

    [Fact]
    public async Task Resume_Approved_MarksApproved()
    {
        var store  = new WorkflowHitlStore(MakeCommandStore());
        var paused = await store.PauseAsync("run-3", "wf-3", "S2",
            HITLRequirementType.Confirmation, "confirm?");

        var resumed = await store.ResumeAsync(
            paused.CheckpointId,
            new HitlResolution { Approved = true, Notes = "looks good" });

        Assert.Equal(HITLStatus.Approved, resumed.Status);
        Assert.NotNull(resumed.ResolvedAt);
    }

    // ── Test 5: Resume on non-existent checkpoint throws ─────────────────────

    [Fact]
    public async Task Resume_NonExistent_Throws()
        => await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new WorkflowHitlStore(MakeCommandStore())
                .ResumeAsync("no-such-id", new HitlResolution { Approved = true }));

    // ── Test 6: GetPending returns only Pending records ───────────────────────

    [Fact]
    public async Task GetPending_ReturnsOnlyPending()
    {
        var store  = new WorkflowHitlStore(MakeCommandStore());
        var p1 = await store.PauseAsync("run-4", "wf-4", "S1", HITLRequirementType.Confirmation, "x");
        var p2 = await store.PauseAsync("run-4", "wf-4", "S2", HITLRequirementType.Confirmation, "y");
        await store.ResumeAsync(p1.CheckpointId, new HitlResolution { Approved = false });

        var pending = await store.GetPendingAsync("run-4");
        Assert.Single(pending);
        Assert.Equal(p2.CheckpointId, pending[0].CheckpointId);
    }

    // ── Test 7: ExpireAsync marks expired checkpoints ─────────────────────────

    [Fact]
    public async Task Expire_MarksExpiredCheckpoints()
    {
        var store = new WorkflowHitlStore(MakeCommandStore());
        // Pause with a tiny negative expiry (already expired).
        await store.PauseAsync("run-5", "wf-5", "S3",
            HITLRequirementType.Confirmation, "x",
            expiry: TimeSpan.FromMilliseconds(-1));

        var count = await store.ExpireAsync();
        Assert.Equal(1, count);

        var pending = await store.GetPendingAsync("run-5");
        Assert.Empty(pending);
    }
}

// ── SS-07 — Content Store ─────────────────────────────────────────────────────

public class ContentStoreTests
{
    private static ICommandStore MakeCommandStore()
    {
        var store = Substitute.For<ICommandStore>();
        store.SaveAsync(Arg.Any<ContentEntry>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        store.UpdateAsync(Arg.Any<ContentEntry>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        store.DeleteAsync<ContentEntry>(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        return store;
    }

    private static ContentEntry MakeEntry(
        string id = "c-01", ContentTag tag = ContentTag.Guide, string? ns = "docs")
        => new ContentEntry
        {
            Id    = id,
            Title = $"Title-{id}",
            Body  = $"Body content for {id}",
            Tag   = tag,
            Namespace = ns,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    // ── Test 1: null command store ────────────────────────────────────────────

    [Fact]
    public void Constructor_NullCommandStore_Throws()
        => Assert.Throws<ArgumentNullException>(() => new ContentStore(null!));

    // ── Test 2: Upsert + Get round-trip ──────────────────────────────────────

    [Fact]
    public async Task Upsert_ThenGet_ReturnsEntry()
    {
        var cs = new ContentStore(MakeCommandStore());
        var e  = MakeEntry();
        await cs.UpsertAsync(e);
        var fetched = await cs.GetAsync(e.Id);
        Assert.NotNull(fetched);
        Assert.Equal(e.Title, fetched!.Title);
    }

    // ── Test 3: Get unknown returns null ──────────────────────────────────────

    [Fact]
    public async Task Get_Unknown_ReturnsNull()
    {
        var cs = new ContentStore(MakeCommandStore());
        Assert.Null(await cs.GetAsync("no-such"));
    }

    // ── Test 4: Delete removes entry ─────────────────────────────────────────

    [Fact]
    public async Task Delete_RemovesEntry()
    {
        var cs = new ContentStore(MakeCommandStore());
        var e  = MakeEntry("c-del");
        await cs.UpsertAsync(e);
        await cs.DeleteAsync(e.Id);
        Assert.Null(await cs.GetAsync(e.Id));
    }

    // ── Test 5: Search by tag ─────────────────────────────────────────────────

    [Fact]
    public async Task Search_ByTag_FiltersCorrectly()
    {
        var cs = new ContentStore(MakeCommandStore());
        await cs.UpsertAsync(MakeEntry("c1", ContentTag.Guide,     "docs"));
        await cs.UpsertAsync(MakeEntry("c2", ContentTag.Faq,       "docs"));
        await cs.UpsertAsync(MakeEntry("c3", ContentTag.Guide,     "docs"));

        var results = await cs.SearchAsync(tag: ContentTag.Guide);
        Assert.Equal(2, results.Count);
    }

    // ── Test 6: Search by keyword ─────────────────────────────────────────────

    [Fact]
    public async Task Search_ByKeyword_FiltersCorrectly()
    {
        var cs = new ContentStore(MakeCommandStore());
        await cs.UpsertAsync(MakeEntry("c-kw1", ns: "x") with { Title = "Alpha Guide" });
        await cs.UpsertAsync(MakeEntry("c-kw2", ns: "x") with { Title = "Beta Reference" });

        var results = await cs.SearchAsync(keyword: "alpha");
        Assert.Single(results);
        Assert.Equal("c-kw1", results[0].Id);
    }

    // ── Test 7: List by namespace ─────────────────────────────────────────────

    [Fact]
    public async Task List_ByNamespace_FiltersCorrectly()
    {
        var cs = new ContentStore(MakeCommandStore());
        await cs.UpsertAsync(MakeEntry("c-ns1", ns: "ns-a"));
        await cs.UpsertAsync(MakeEntry("c-ns2", ns: "ns-a"));
        await cs.UpsertAsync(MakeEntry("c-ns3", ns: "ns-b"));

        var nsA = await cs.ListAsync("ns-a");
        Assert.Equal(2, nsA.Count);
        Assert.All(nsA, e => Assert.Equal("ns-a", e.Namespace));
    }
}

// ── SS-08 — Team Tool Policy Store ────────────────────────────────────────────

public class TeamToolPolicyStoreTests
{
    private static ICommandStore MakeCommandStore()
    {
        var store = Substitute.For<ICommandStore>();
        store.SaveAsync(Arg.Any<TeamToolPolicy>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        store.UpdateAsync(Arg.Any<TeamToolPolicy>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        store.DeleteAsync<TeamToolPolicy>(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        return store;
    }

    private static TeamToolPolicy MakePolicy(string teamId = "team-01")
        => new TeamToolPolicy
        {
            TeamId = teamId,
            DefaultTools = new[]
            {
                new DefaultToolEntry { TypeName = "SearchTool",  Scope = ToolScope.AllAgents },
                new DefaultToolEntry { TypeName = "LogTool",     Scope = ToolScope.SupervisorOnly },
                new DefaultToolEntry { TypeName = "DataFetcher", Scope = ToolScope.WorkersOnly },
            },
        };

    // ── Test 1: null command store throws ─────────────────────────────────────

    [Fact]
    public void Constructor_NullCommandStore_Throws()
        => Assert.Throws<ArgumentNullException>(() => new TeamToolPolicyStore(null!));

    // ── Test 2: SetPolicy + GetPolicy round-trip ──────────────────────────────

    [Fact]
    public async Task SetPolicy_ThenGet_ReturnsPolicy()
    {
        var s      = new TeamToolPolicyStore(MakeCommandStore());
        var policy = MakePolicy();
        await s.SetPolicyAsync(policy);
        var fetched = await s.GetPolicyAsync(policy.TeamId);
        Assert.NotNull(fetched);
        Assert.Equal(3, fetched!.DefaultTools.Count);
    }

    // ── Test 3: GetPolicy unknown returns null ────────────────────────────────

    [Fact]
    public async Task GetPolicy_Unknown_ReturnsNull()
    {
        var s = new TeamToolPolicyStore(MakeCommandStore());
        Assert.Null(await s.GetPolicyAsync("no-such-team"));
    }

    // ── Test 4: ClearPolicy removes entry ────────────────────────────────────

    [Fact]
    public async Task ClearPolicy_RemovesEntry()
    {
        var s = new TeamToolPolicyStore(MakeCommandStore());
        await s.SetPolicyAsync(MakePolicy("t-del"));
        await s.ClearPolicyAsync("t-del");
        Assert.Null(await s.GetPolicyAsync("t-del"));
    }

    // ── Test 5: GetEffectiveTools — AllAgents scope ───────────────────────────

    [Fact]
    public async Task GetEffectiveTools_AllAgentsScope_ReturnsCorrectTools()
    {
        var s = new TeamToolPolicyStore(MakeCommandStore());
        await s.SetPolicyAsync(MakePolicy("t-scope"));

        // AllAgents scope: includes AllAgents entries + WorkersOnly entries
        var tools = await s.GetEffectiveToolsAsync("t-scope", ToolScope.WorkersOnly);
        Assert.Equal(2, tools.Count); // SearchTool(All) + DataFetcher(Workers)
    }

    // ── Test 6: ListPolicies returns all ──────────────────────────────────────

    [Fact]
    public async Task ListPolicies_ReturnsAll()
    {
        var s = new TeamToolPolicyStore(MakeCommandStore());
        await s.SetPolicyAsync(MakePolicy("t1"));
        await s.SetPolicyAsync(MakePolicy("t2"));
        var list = await s.ListPoliciesAsync();
        Assert.Equal(2, list.Count);
    }

    // ── Test 7: GetEffectiveTools unknown team returns empty ──────────────────

    [Fact]
    public async Task GetEffectiveTools_UnknownTeam_ReturnsEmpty()
    {
        var s = new TeamToolPolicyStore(MakeCommandStore());
        var tools = await s.GetEffectiveToolsAsync("no-team", ToolScope.AllAgents);
        Assert.Empty(tools);
    }
}

// ── SS-09 — Callable Member Registry ─────────────────────────────────────────

public class CallableMemberRegistryTests
{
    private static CallableMemberDescriptor MakeDescriptor(
        string name, CallableMemberType type = CallableMemberType.Agent)
        => new CallableMemberDescriptor
        {
            Name     = name,
            Type     = type,
            EntityId = $"entity-{name}",
        };

    // ── Test 1: Register + Resolve ────────────────────────────────────────────

    [Fact]
    public async Task Register_ThenResolve_ReturnsDescriptor()
    {
        var reg = new CallableMemberRegistry();
        var desc = MakeDescriptor("researcher");
        await reg.RegisterAsync("scope-1", desc);
        var resolved = await reg.ResolveAsync("scope-1", "researcher");
        Assert.NotNull(resolved);
        Assert.Equal("researcher", resolved!.Name);
    }

    // ── Test 2: Resolve unknown returns null ──────────────────────────────────

    [Fact]
    public async Task Resolve_Unknown_ReturnsNull()
    {
        var reg = new CallableMemberRegistry();
        Assert.Null(await reg.ResolveAsync("scope-x", "nobody"));
    }

    // ── Test 3: List all in scope ─────────────────────────────────────────────

    [Fact]
    public async Task List_AllInScope_ReturnsAll()
    {
        var reg = new CallableMemberRegistry();
        await reg.RegisterAsync("s", MakeDescriptor("a", CallableMemberType.Agent));
        await reg.RegisterAsync("s", MakeDescriptor("t", CallableMemberType.Tool));
        await reg.RegisterAsync("s", MakeDescriptor("b", CallableMemberType.Agent));

        var all = await reg.ListAsync("s");
        Assert.Equal(3, all.Count);
    }

    // ── Test 4: List filtered by type ─────────────────────────────────────────

    [Fact]
    public async Task List_FilteredByType_ReturnsOnlyMatching()
    {
        var reg = new CallableMemberRegistry();
        await reg.RegisterAsync("s2", MakeDescriptor("a1", CallableMemberType.Agent));
        await reg.RegisterAsync("s2", MakeDescriptor("t1", CallableMemberType.Tool));

        var agents = await reg.ListAsync("s2", CallableMemberType.Agent);
        Assert.Single(agents);
        Assert.Equal("a1", agents[0].Name);
    }

    // ── Test 5: Unregister removes member ────────────────────────────────────

    [Fact]
    public async Task Unregister_RemovesMember()
    {
        var reg = new CallableMemberRegistry();
        await reg.RegisterAsync("s3", MakeDescriptor("x"));
        await reg.UnregisterAsync("s3", "x");
        Assert.Null(await reg.ResolveAsync("s3", "x"));
    }

    // ── Test 6: ClearScope removes all ───────────────────────────────────────

    [Fact]
    public async Task ClearScope_RemovesAllMembers()
    {
        var reg = new CallableMemberRegistry();
        await reg.RegisterAsync("s4", MakeDescriptor("a"));
        await reg.RegisterAsync("s4", MakeDescriptor("b"));
        await reg.ClearScopeAsync("s4");

        var list = await reg.ListAsync("s4");
        Assert.Empty(list);
    }

    // ── Test 7: Register replaces existing ───────────────────────────────────

    [Fact]
    public async Task Register_ReplacesExistingByName()
    {
        var reg = new CallableMemberRegistry();
        await reg.RegisterAsync("s5", MakeDescriptor("x") with { Role = "old" });
        await reg.RegisterAsync("s5", MakeDescriptor("x") with { Role = "new" });

        var resolved = await reg.ResolveAsync("s5", "x");
        Assert.Equal("new", resolved!.Role);
    }
}

// ── SS-10 — Distributed RAG Team ─────────────────────────────────────────────

public class DistributedRagTeamTests
{
    // Build a mock IKnowledgeBase that returns a fixed chunk for any query.
    private static IKnowledgeBase MakeKnowledgeBase(
        string docId, float score, string content)
    {
        var kb = Substitute.For<IKnowledgeBase>();
        kb.SearchAsync(
              Arg.Any<string>(),
              Arg.Any<KnowledgeSearchOptions?>(),
              Arg.Any<CancellationToken>())
          .Returns(new List<KnowledgeChunk>
          {
              new KnowledgeChunk(docId, content, score)
          });
        return kb;
    }

    private static DistributedRagTeam MakeTeam(
        IReadOnlyDictionary<string, IKnowledgeBase> sources,
        int maxParallel = 8)
    {
        return new DistributedRagTeam(
            desc => sources[desc.Id],
            maxParallelSources: maxParallel);
    }

    // ── Test 1: null resolver throws ──────────────────────────────────────────

    [Fact]
    public void Constructor_NullResolver_Throws()
        => Assert.Throws<ArgumentNullException>(
            () => new DistributedRagTeam(null!));

    // ── Test 2: Register + ListSources ───────────────────────────────────────

    [Fact]
    public async Task Register_ThenList_ReturnsSources()
    {
        var team = new DistributedRagTeam(_ => Substitute.For<IKnowledgeBase>());
        await team.RegisterSourceAsync(new RagSourceDescriptor { Id = "src-1", Name = "KB1" });
        var list = await team.ListSourcesAsync();
        Assert.Single(list);
    }

    // ── Test 3: SearchAsync merges results ───────────────────────────────────

    [Fact]
    public async Task SearchAsync_MergesMultipleSources()
    {
        var kbs = new Dictionary<string, IKnowledgeBase>
        {
            ["s1"] = MakeKnowledgeBase("doc-1", 0.9f, "result from s1"),
            ["s2"] = MakeKnowledgeBase("doc-2", 0.8f, "result from s2"),
        };
        var team = MakeTeam(kbs);
        await team.RegisterSourceAsync(new RagSourceDescriptor { Id = "s1", Name = "S1" });
        await team.RegisterSourceAsync(new RagSourceDescriptor { Id = "s2", Name = "S2" });

        var result = await team.SearchAsync(new DistributedRagQuery { Query = "test", MinScore = 0.5 });

        Assert.Equal(2, result.Chunks.Count);
        Assert.Equal(2, result.TotalSourcesQueried);
        Assert.Equal(2, result.SourcesWithResults);
        // Highest-score first.
        Assert.Equal("doc-1", result.Chunks[0].DocumentId);
    }

    // ── Test 4: Paused source is excluded ─────────────────────────────────────

    [Fact]
    public async Task SearchAsync_ExcludesPausedSource()
    {
        var kbs = new Dictionary<string, IKnowledgeBase>
        {
            ["s1"] = MakeKnowledgeBase("doc-1", 0.9f, "from s1"),
            ["s2"] = MakeKnowledgeBase("doc-2", 0.8f, "from s2"),
        };
        var team = MakeTeam(kbs);
        await team.RegisterSourceAsync(new RagSourceDescriptor { Id = "s1", Name = "S1" });
        await team.RegisterSourceAsync(new RagSourceDescriptor { Id = "s2", Name = "S2" });
        await team.UpdateSourceStatusAsync("s2", RagSourceStatus.Paused);

        var result = await team.SearchAsync(new DistributedRagQuery { Query = "test", MinScore = 0.5 });

        Assert.Equal(1, result.TotalSourcesQueried);
        Assert.Single(result.Chunks);
        Assert.Equal("doc-1", result.Chunks[0].DocumentId);
    }

    // ── Test 5: Unregister removes source ─────────────────────────────────────

    [Fact]
    public async Task Unregister_RemovesSource()
    {
        var team = new DistributedRagTeam(_ => Substitute.For<IKnowledgeBase>());
        await team.RegisterSourceAsync(new RagSourceDescriptor { Id = "src-x", Name = "X" });
        await team.UnregisterSourceAsync("src-x");
        var list = await team.ListSourcesAsync();
        Assert.Empty(list);
    }

    // ── Test 6: Empty query throws ────────────────────────────────────────────

    [Fact]
    public async Task SearchAsync_EmptyQuery_Throws()
    {
        var team = new DistributedRagTeam(_ => Substitute.For<IKnowledgeBase>());
        await Assert.ThrowsAsync<ArgumentException>(() =>
            team.SearchAsync(new DistributedRagQuery { Query = "" }));
    }

    // ── Test 7: TopKTotal limits merged results ───────────────────────────────

    [Fact]
    public async Task SearchAsync_TopKTotal_LimitsResults()
    {
        // Two sources each returning 1 chunk → total 2 available, limit to 1.
        var kbs = new Dictionary<string, IKnowledgeBase>
        {
            ["s1"] = MakeKnowledgeBase("doc-1", 0.9f, "x"),
            ["s2"] = MakeKnowledgeBase("doc-2", 0.8f, "y"),
        };
        var team = MakeTeam(kbs);
        await team.RegisterSourceAsync(new RagSourceDescriptor { Id = "s1", Name = "S1" });
        await team.RegisterSourceAsync(new RagSourceDescriptor { Id = "s2", Name = "S2" });

        var result = await team.SearchAsync(
            new DistributedRagQuery { Query = "q", TopKTotal = 1, MinScore = 0.5 });

        Assert.Single(result.Chunks);
    }
}
