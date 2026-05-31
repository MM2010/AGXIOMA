using AGXIOME.Runtime.UI;
using AGXIOME.Shared.Models;
using Xunit;
// Disambiguate: ApprovalService defines its own ApprovalStatus in AGXIOME.Runtime.UI
using ApprovalStatus = AGXIOME.Runtime.UI.ApprovalStatus;

namespace AGXIOME.Tests.F18;

// ── Happy Path ────────────────────────────────────────────────────────────────

[Trait("Phase", "F18")]
public class F18_TS02_ApprovalService_HappyPath
{
    private static ApprovalService Build() => new();

    [Fact]
    public void CreateRequest_ReturnsRequestWithPendingStatus()
    {
        var svc = Build();
        var req = svc.CreateRequest("agent1", "run1", "delete-all", "Delete all records");

        Assert.NotNull(req);
        Assert.Equal(ApprovalStatus.Pending, req.Status);
        Assert.Equal("agent1", req.AgentId);
        Assert.Equal("run1",   req.RunId);
        Assert.NotEmpty(req.RequestId);
    }

    [Fact]
    public async Task GetApprovalRequestAsync_KnownId_ReturnsRequest()
    {
        var svc = Build();
        var req = svc.CreateRequest("agent1", "run1", "act1", "ctx");

        var found = await svc.GetApprovalRequestAsync(req.RequestId);

        Assert.NotNull(found);
        Assert.Equal(req.RequestId, found!.RequestId);
    }

    [Fact]
    public async Task GetApprovalRequestAsync_UnknownId_ReturnsNull()
    {
        var svc  = Build();
        var found = await svc.GetApprovalRequestAsync("not-existing");

        Assert.Null(found);
    }

    [Fact]
    public async Task ApproveAsync_ChangesStatusToApproved()
    {
        var svc = Build();
        var req = svc.CreateRequest("agent1", "run1", "act1", "ctx");

        var result = await svc.ApproveAsync(req.RequestId, "looks good");

        Assert.True(result.Success);
        Assert.Equal(ApprovalStatus.Approved, result.NewStatus);
    }

    [Fact]
    public async Task RejectAsync_ChangesStatusToRejected()
    {
        var svc = Build();
        var req = svc.CreateRequest("agent1", "run1", "act1", "ctx");

        var result = await svc.RejectAsync(req.RequestId, "not allowed");

        Assert.True(result.Success);
        Assert.Equal(ApprovalStatus.Rejected, result.NewStatus);
    }

    [Fact]
    public async Task GetPendingApprovalsAsync_NoFilter_ReturnsAll()
    {
        var svc = Build();
        svc.CreateRequest("agent1", "run1", "act1", "ctx");
        svc.CreateRequest("agent2", "run2", "act2", "ctx");

        var pending = await svc.GetPendingApprovalsAsync();

        Assert.Equal(2, pending.Count);
    }

    [Fact]
    public async Task GetPendingApprovalsAsync_FilterByAgent_ReturnsSubset()
    {
        var svc = Build();
        svc.CreateRequest("agent1", "run1", "act1", "ctx");
        svc.CreateRequest("agent1", "run2", "act2", "ctx");
        svc.CreateRequest("agent2", "run3", "act3", "ctx");

        var agent1 = await svc.GetPendingApprovalsAsync("agent1");
        var agent2 = await svc.GetPendingApprovalsAsync("agent2");

        Assert.Equal(2, agent1.Count);
        Assert.Single(agent2);
    }

    [Fact]
    public async Task GetApprovalHistoryAsync_ApprovedAndRejected_Returned()
    {
        var svc = Build();
        var r1  = svc.CreateRequest("agent1", "run1", "act1", "ctx");
        var r2  = svc.CreateRequest("agent1", "run2", "act2", "ctx");

        await svc.ApproveAsync(r1.RequestId);
        await svc.RejectAsync(r2.RequestId, "reason");

        var history = await svc.GetApprovalHistoryAsync("agent1");

        Assert.Equal(2, history.Count);
        Assert.All(history, r => Assert.NotEqual(ApprovalStatus.Pending, r.Status));
    }

    [Fact]
    public async Task AfterApprove_NolongerInPendingList()
    {
        var svc = Build();
        var req = svc.CreateRequest("agent1", "run1", "act1", "ctx");

        await svc.ApproveAsync(req.RequestId);

        var pending = await svc.GetPendingApprovalsAsync("agent1");
        Assert.Empty(pending);
    }
}

// ── Offensive ─────────────────────────────────────────────────────────────────

[Trait("Phase", "F18")]
public class F18_TS02_ApprovalService_Offensive
{
    private static ApprovalService Build() => new();

    [Fact]
    public async Task ApproveAsync_UnknownId_ReturnsFalse()
    {
        var svc    = Build();
        var result = await svc.ApproveAsync("does-not-exist");

        Assert.False(result.Success);
    }

    [Fact]
    public async Task RejectAsync_UnknownId_ReturnsFalse()
    {
        var svc    = Build();
        var result = await svc.RejectAsync("does-not-exist", "n/a");

        Assert.False(result.Success);
    }

    [Fact]
    public async Task ApproveAlreadyApproved_ReturnsFalse()
    {
        var svc = Build();
        var req = svc.CreateRequest("a1", "r1", "act", "ctx");

        await svc.ApproveAsync(req.RequestId);
        var second = await svc.ApproveAsync(req.RequestId);

        Assert.False(second.Success);
    }

    [Fact]
    public async Task GetApprovalHistoryAsync_LimitRespected()
    {
        var svc = Build();
        for (int i = 0; i < 10; i++)
        {
            var r = svc.CreateRequest("a1", $"run{i}", $"act{i}", "ctx");
            await svc.ApproveAsync(r.RequestId);
        }

        var history = await svc.GetApprovalHistoryAsync("a1", limit: 3);

        Assert.True(history.Count <= 3);
    }

    [Fact]
    public void CreateRequest_CustomTtl_ExpiresAtIsInFuture()
    {
        var svc = Build();
        var req = svc.CreateRequest("a1", "r1", "act", "ctx", ttl: TimeSpan.FromSeconds(10));

        Assert.True(req.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task GetPendingApprovalsAsync_EmptyService_ReturnsEmpty()
    {
        var svc     = Build();
        var pending = await svc.GetPendingApprovalsAsync();

        Assert.Empty(pending);
    }
}
