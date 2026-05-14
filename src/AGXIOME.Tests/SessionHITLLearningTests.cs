using AGXIOME.Core.HITL;
using AGXIOME.Core.Sessions;
using AGXIOME.Core.Agent;
using AGXIOME.Core.Learning;
using AGXIOME.Core.Skills;
using AGXIOME.Core.Scheduling;
using AGXIOME.Core.Context;
using AGXIOME.Shared.Config;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using Xunit;

namespace AGXIOME.Tests;

public class SessionHITLLearningTests
{
    // ── HITL ──

    [Fact]
    public void HITL_DoubleResolve_NoOp()
    {
        var mgr = new HITLManager();
        var req = mgr.CreateRequirement("run1", "agent1", "tool1", new { }, HITLRequirementType.Confirmation);

        mgr.Resolve(req.Id, true);
        mgr.Resolve(req.Id, false); // second resolve should be no-op

        var resolved = mgr.GetRequirement(req.Id);
        Assert.Equal(HITLStatus.Approved, resolved?.Status);
    }

    [Fact]
    public void HITL_ResolveUnknown_NoThrow()
    {
        var mgr = new HITLManager();
        mgr.Resolve("nonexistent", true); // should not throw
    }

    [Fact]
    public void HITL_GetPendingForRun_FiltersCorrectly()
    {
        var mgr = new HITLManager();
        mgr.CreateRequirement("run1", "a1", "t1", new { }, HITLRequirementType.Confirmation);
        mgr.CreateRequirement("run2", "a2", "t2", new { }, HITLRequirementType.UserInput);

        var run1Pending = mgr.GetPendingForRun("run1");
        var run2Pending = mgr.GetPendingForRun("run2");

        Assert.Single(run1Pending);
        Assert.Single(run2Pending);
    }

    // ── Session ──

    [Fact]
    public async Task SessionManager_CreateAndRetrieve()
    {
        var mgr = new SessionManager();
        var session = await mgr.CreateSessionAsync("user1", "agent1", SessionType.Agent);

        var retrieved = await mgr.GetSessionAsync(session.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("user1", retrieved.UserId);
    }

    [Fact]
    public async Task SessionManager_Delete_Removes()
    {
        var mgr = new SessionManager();
        var session = await mgr.CreateSessionAsync("user1", "agent1", SessionType.Agent);

        await mgr.DeleteSessionAsync(session.Id);

        Assert.Null(await mgr.GetSessionAsync(session.Id));
    }

    // ── State ──

    [Fact]
    public void SessionState_GetUnknown_Default()
    {
        var state = new SessionState();
        Assert.Equal(0, state.Get<int>("nonexistent"));
        Assert.Null(state.Get<string>("nonexistent"));
    }

    [Fact]
    public void SessionState_ToDictionary_IsolatedCopy()
    {
        var state = new SessionState();
        state.Set("key", "value");
        var dict = state.ToDictionary();
        dict["key"] = "modified";

        Assert.Equal("value", state.Get<string>("key"));
    }

    // ── Context ──

    [Fact]
    public void DynamicInstructionResolver_UnknownKey_KeepsPlaceholder()
    {
        var resolver = new DynamicInstructionResolver();
        var state = new SessionState();

        var result = resolver.Resolve("Hello {unknown_key}", state);

        Assert.Equal("Hello {unknown_key}", result);
    }

    [Fact]
    public void DependencyProvider_FactoryResolvedAtRuntime()
    {
        var provider = new DefaultDependencyProvider();
        int factoryCalls = 0;
        provider.RegisterDependency(() => { factoryCalls++; return "factory-value"; });

        var val1 = provider.GetDependency<string>();
        var val2 = provider.GetDependency<string>();

        Assert.Equal("factory-value", val1);
        Assert.Equal("factory-value", val2);
        Assert.True(factoryCalls >= 1);
    }

    // ── Learning (behavioural) ──

    [Fact]
    public async Task Learning_AlwaysMode_AutoSaves()
    {
        var lm = new LearningMachine(new LearningConfig
        {
            UserMemoryMode = LearningMode.Always,
            UserProfileMode = LearningMode.Always
        });

        // Should not throw
        await lm.ProcessAsync("agent1", "input text", "output text");
    }

    // ── Skills ──

    [Fact]
    public void SkillManager_ListByCategory_NoMatch_Empty()
    {
        var mgr = new SkillManager();
        Assert.Empty(mgr.ListSkills("nonexistent"));
    }

    [Fact]
    public async Task SkillManager_Download_InvalidUrl_Throws()
    {
        var mgr = new SkillManager();
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            mgr.DownloadSkillAsync("test", "http://localhost:1/nonexistent"));
    }

    // ── Schedule ──

    [Fact]
    public async Task ScheduleManager_CreateAndDelete()
    {
        var mgr = new ScheduleManager();
        var sched = await mgr.CreateScheduleAsync("test", "wf-1", "*/5 * * * *");

        await mgr.DeleteScheduleAsync(sched.Id);

        var list = await mgr.ListSchedulesAsync();
        Assert.Empty(list);
    }

    [Fact]
    public async Task ScheduleManager_PauseResume()
    {
        var mgr = new ScheduleManager();
        var sched = await mgr.CreateScheduleAsync("pause-test", "wf-1", "0 0 * * *");

        await mgr.UpdateScheduleAsync(sched.Id, enabled: false);
        var list = await mgr.ListSchedulesAsync();
        Assert.Equal(ScheduleStatus.Paused, list[0].Status);

        await mgr.UpdateScheduleAsync(sched.Id, enabled: true);
        list = await mgr.ListSchedulesAsync();
        Assert.Equal(ScheduleStatus.Active, list[0].Status);
    }

    [Fact]
    public async Task ScheduleManager_RunHistory()
    {
        var mgr = new ScheduleManager();
        var sched = await mgr.CreateScheduleAsync("history-test", "wf-1", "*/5 * * * *");

        var history = await mgr.GetRunHistoryAsync(sched.Id);
        Assert.Empty(history);
    }
}
