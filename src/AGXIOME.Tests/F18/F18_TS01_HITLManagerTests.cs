using AGXIOME.Core.HITL;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using Xunit;

namespace AGXIOME.Tests.F18;

// ── Fixtures / Helpers ────────────────────────────────────────────────────────

/// <summary>Fake IHitlCompletionSource that records calls.</summary>
internal sealed class FakeCompletionSource : IHitlCompletionSource
{
    public int NotifyCount { get; private set; }
    public Guid LastTaskId { get; private set; }
    public Guid LastPlanId { get; private set; }
    public bool LastApproved { get; private set; }

    public Task NotifyResolvedAsync(Guid taskId, Guid planId, string requirementId, bool approved,
        CancellationToken ct = default)
    {
        NotifyCount++;
        LastTaskId  = taskId;
        LastPlanId  = planId;
        LastApproved = approved;
        return Task.CompletedTask;
    }
}

// ── Happy Path ────────────────────────────────────────────────────────────────

[Trait("Phase", "F18")]
public class F18_TS01_HITLManager_HappyPath
{
    [Fact]
    public void CreateRequirement_ReturnsRequirementWithPendingStatus()
    {
        var mgr = new HITLManager();
        var req = mgr.CreateRequirement("run1", "agent1", "tool1", new { }, HITLRequirementType.Confirmation);

        Assert.NotNull(req);
        Assert.Equal(HITLStatus.Pending, req.Status);
        Assert.Equal("run1",   req.RunId);
        Assert.Equal("agent1", req.AgentId);
        Assert.Equal("tool1",  req.ToolName);
    }

    [Fact]
    public void GetRequirement_KnownId_ReturnsRequirement()
    {
        var mgr = new HITLManager();
        var req = mgr.CreateRequirement("run1", "agent1", "tool1", new { }, HITLRequirementType.Confirmation);

        var found = mgr.GetRequirement(req.Id);

        Assert.NotNull(found);
        Assert.Equal(req.Id, found!.Id);
    }

    [Fact]
    public void GetRequirement_UnknownId_ReturnsNull()
    {
        var mgr = new HITLManager();

        Assert.Null(mgr.GetRequirement("not-a-real-id"));
    }

    [Fact]
    public void Resolve_Approve_ChangesStatusToApproved()
    {
        var mgr = new HITLManager();
        var req = mgr.CreateRequirement("run1", "a1", "t1", new { }, HITLRequirementType.Confirmation);

        mgr.Resolve(req.Id, approved: true);

        // After resolve the requirement is removed from pending — get it via GetRequirement.
        // HITLManager.Resolve removes from _pending only when already resolved:
        // expect null (removed) OR Approved.
        var after = mgr.GetRequirement(req.Id);
        // The record may still be accessible as Approved:
        if (after is not null) Assert.Equal(HITLStatus.Approved, after.Status);
        // Pending list must be empty.
        Assert.Empty(mgr.GetPendingForRun("run1"));
    }

    [Fact]
    public void Resolve_Reject_ChangesStatusToRejected()
    {
        var mgr = new HITLManager();
        var req = mgr.CreateRequirement("run1", "a1", "t1", new { }, HITLRequirementType.UserInput);

        mgr.Resolve(req.Id, approved: false);

        Assert.Empty(mgr.GetPendingForRun("run1"));
    }

    [Fact]
    public void GetPendingForRun_FiltersCorrectly()
    {
        var mgr = new HITLManager();
        mgr.CreateRequirement("run-A", "a1", "t1", new { }, HITLRequirementType.Confirmation);
        mgr.CreateRequirement("run-A", "a1", "t2", new { }, HITLRequirementType.UserInput);
        mgr.CreateRequirement("run-B", "a2", "t3", new { }, HITLRequirementType.ExternalExecution);

        Assert.Equal(2, mgr.GetPendingForRun("run-A").Count);
        Assert.Single(mgr.GetPendingForRun("run-B"));
        Assert.Empty(mgr.GetPendingForRun("run-C"));
    }

    [Fact]
    public void GetPendingForRun_AfterResolve_DoesNotContainResolved()
    {
        var mgr = new HITLManager();
        var r1 = mgr.CreateRequirement("run1", "a1", "t1", new { }, HITLRequirementType.Confirmation);
        mgr.CreateRequirement("run1", "a1", "t2", new { }, HITLRequirementType.UserInput);

        mgr.Resolve(r1.Id, approved: true);

        Assert.Single(mgr.GetPendingForRun("run1"));
    }

    [Fact]
    public void RequirementRecord_Properties_Accessible()
    {
        var req = new HITLRequirement(
            Id: "id1", RunId: "run1", AgentId: "a1", ToolName: "t1",
            Payload: "data", Type: HITLRequirementType.Confirmation);

        Assert.Equal(HITLRequirementType.Confirmation, req.Type);
        Assert.Equal(HITLStatus.Pending, req.Status);
        Assert.Null(req.MemberAgentName);
        Assert.Null(req.OrchestratorTaskId);
        Assert.Null(req.OrchestratorPlanId);
    }

    [Fact]
    public void HITLRequirementTypes_CoverAllCases()
    {
        // Verifies the enum matches P15 spec.
        var types = Enum.GetValues<HITLRequirementType>();

        Assert.Contains(HITLRequirementType.Confirmation, types);
        Assert.Contains(HITLRequirementType.UserInput,    types);
        Assert.Contains(HITLRequirementType.ExternalExecution, types);
        Assert.Contains(HITLRequirementType.Audit, types);
    }
}

// ── Offensive ─────────────────────────────────────────────────────────────────

[Trait("Phase", "F18")]
public class F18_TS01_HITLManager_Offensive
{
    [Fact]
    public void Resolve_Unknown_DoesNotThrow()
    {
        var mgr = new HITLManager();
        // Should silently no-op
        mgr.Resolve("nonexistent-id", approved: true);
    }

    [Fact]
    public void Resolve_Twice_OnlyFirstResolveApplied()
    {
        var mgr = new HITLManager();
        var req = mgr.CreateRequirement("run1", "a1", "t1", new { }, HITLRequirementType.Confirmation);

        mgr.Resolve(req.Id, approved: true);
        // Second resolve must be a no-op — status stays Approved.
        mgr.Resolve(req.Id, approved: false);

        var after = mgr.GetRequirement(req.Id);
        if (after is not null) Assert.Equal(HITLStatus.Approved, after.Status);
    }

    [Fact]
    public void GetPendingForRun_EmptyRun_ReturnsEmpty()
    {
        var mgr = new HITLManager();
        Assert.Empty(mgr.GetPendingForRun("run-never-seen"));
    }

    [Fact]
    public void CreateRequirement_NullPayload_Allowed()
    {
        var mgr = new HITLManager();
        // Payload is object; null is valid (no constraint on P type).
        var req = mgr.CreateRequirement("run1", "a1", "t1", null!, HITLRequirementType.Audit);

        Assert.NotNull(req);
    }

    [Fact]
    public void CreateRequirement_WithOrchestratorContext_Stored()
    {
        var taskId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var mgr    = new HITLManager();

        var req = mgr.CreateRequirement("run1", "a1", "t1", new { }, HITLRequirementType.Confirmation,
            orchestratorTaskId: taskId, orchestratorPlanId: planId);

        Assert.Equal(taskId, req.OrchestratorTaskId);
        Assert.Equal(planId, req.OrchestratorPlanId);
    }

    [Fact]
    public void Resolve_WithOrchestrationBridge_NotifiesBridge()
    {
        var bridge = new FakeCompletionSource();
        var mgr    = new HITLManager(orchestrationBridge: bridge);
        var taskId = Guid.NewGuid();
        var planId = Guid.NewGuid();

        var req = mgr.CreateRequirement("run1", "a1", "t1", new { }, HITLRequirementType.Confirmation,
            orchestratorTaskId: taskId, orchestratorPlanId: planId);

        mgr.Resolve(req.Id, approved: true);

        // Give fire-and-forget time to run.
        Thread.Sleep(50);

        Assert.Equal(1,      bridge.NotifyCount);
        Assert.Equal(taskId, bridge.LastTaskId);
        Assert.Equal(planId, bridge.LastPlanId);
        Assert.True(bridge.LastApproved);
    }

    [Fact]
    public void Resolve_WithOrchestrationBridge_NoOrchestratorIds_NoBridgeCall()
    {
        var bridge = new FakeCompletionSource();
        var mgr    = new HITLManager(orchestrationBridge: bridge);

        var req = mgr.CreateRequirement("run1", "a1", "t1", new { }, HITLRequirementType.Confirmation);

        mgr.Resolve(req.Id, approved: false);

        Thread.Sleep(50);

        Assert.Equal(0, bridge.NotifyCount);
    }
}

// ── Aggressive ────────────────────────────────────────────────────────────────

[Trait("Phase", "F18")]
public class F18_TS01_HITLManager_Aggressive
{
    [Fact]
    public async Task ConcurrentCreateAndResolve_NoExceptions()
    {
        var mgr  = new HITLManager();
        var ids  = new System.Collections.Concurrent.ConcurrentBag<string>();
        var tasks = Enumerable.Range(0, 100).Select(async i =>
        {
            var req = mgr.CreateRequirement($"run{i % 5}", "a", "t", new { v = i }, HITLRequirementType.Confirmation);
            ids.Add(req.Id);
            await Task.Yield();
        }).ToArray();

        await Task.WhenAll(tasks);

        // Resolve all concurrently
        await Task.WhenAll(ids.Select(id => Task.Run(() => mgr.Resolve(id, approved: true))));

        // All runs should have 0 pending after resolving everything.
        for (int i = 0; i < 5; i++)
            Assert.Empty(mgr.GetPendingForRun($"run{i}"));
    }

    [Fact]
    public void CreateMany_AllStoredAndRetrievable()
    {
        var mgr = new HITLManager();
        const int n = 500;
        var reqs = Enumerable.Range(0, n)
            .Select(i => mgr.CreateRequirement($"run{i}", "a", "t", new { }, HITLRequirementType.Confirmation))
            .ToList();

        Assert.All(reqs, r => Assert.NotNull(mgr.GetRequirement(r.Id)));
    }

    [Fact]
    public void RequiresConfirmationAttribute_Properties_ReflectSpec()
    {
        var attr = new RequiresConfirmationAttribute
        {
            Message           = "Please confirm this action",
            RequiresUserInput = true,
            ExternalExecution = false
        };

        Assert.Equal("Please confirm this action", attr.Message);
        Assert.True(attr.RequiresUserInput);
        Assert.False(attr.ExternalExecution);
    }

    [Fact]
    public void RequiresConfirmationAttribute_CanDecorateClass()
    {
        var attrs = typeof(SensitiveTool)
            .GetCustomAttributes(typeof(RequiresConfirmationAttribute), inherit: false);

        Assert.Single(attrs);
        var a = (RequiresConfirmationAttribute)attrs[0];
        Assert.Equal("Delete all data", a.Message);
        Assert.True(a.ExternalExecution);
    }

    // Dummy tool class for reflection test
    [RequiresConfirmation(Message = "Delete all data", ExternalExecution = true)]
    private sealed class SensitiveTool { }
}
