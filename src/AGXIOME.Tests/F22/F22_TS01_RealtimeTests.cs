using AGXIOME.Orchestration.Infrastructure;
using AGXIOME.Orchestration.Models;
using AGXIOME.Realtime;
using AGXIOME.Realtime.Hubs;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using Xunit;

namespace AGXIOME.Tests.F22;

// ══════════════════════════════════════════════════════════════════════════════
// TS01  SignalRCompletionNotifier — channel routing
// ══════════════════════════════════════════════════════════════════════════════

[Trait("Phase", "F22")]
public class F22_TS01_SignalRCompletionNotifier_HappyPath
{
    private static (SignalRCompletionNotifier notifier, IHubContext<AgentHub, IAgentClient> hub) Build()
    {
        var hub         = Substitute.For<IHubContext<AgentHub, IAgentClient>>();
        var clients     = Substitute.For<IHubClients<IAgentClient>>();
        var groupClient = Substitute.For<IAgentClient>();

        hub.Clients.Returns(clients);
        clients.Group(Arg.Any<string>()).Returns(groupClient);

        return (new SignalRCompletionNotifier(hub), hub);
    }

    [Fact]
    public async Task PublishAsync_WritesToChannel_SubscriberReceivesResult()
    {
        var (notifier, _) = Build();
        var planId = Guid.NewGuid();

        var result = new TaskResult
        {
            TaskId     = Guid.NewGuid(),
            PlanId     = planId,
            Success    = true,
            Output     = "done",
            CompletedAt = DateTimeOffset.UtcNow
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        await notifier.PublishAsync(result, cts.Token);

        // Drain one item from the subscriber async enumerable
        TaskResult? received = null;
        await foreach (var r in notifier.SubscribeAsync(planId, cts.Token))
        {
            received = r;
            break; // got our item
        }

        Assert.NotNull(received);
        Assert.Equal(result.TaskId, received!.TaskId);
    }

    [Fact]
    public async Task PublishAsync_HITLPaused_CallsOnPlanWaitingForHITL()
    {
        var (notifier, hub) = Build();
        var planId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        var result = new TaskResult
        {
            TaskId           = taskId,
            PlanId           = planId,
            Success          = false,
            Output           = "",
            IsHitlPaused     = true,
            HitlRequirementId = "req-123"
        };

        using var cts = new CancellationTokenSource(2000);
        await notifier.PublishAsync(result, cts.Token);

        // Verify SignalR group received the HITL pause call
        await hub.Clients.Group($"plan:{planId}")
            .Received(1)
            .OnPlanWaitingForHITL(planId.ToString(), taskId.ToString(), "req-123");
    }

    [Fact]
    public async Task PublishAsync_NormalResult_DoesNotCallHITL()
    {
        var (notifier, hub) = Build();
        var planId = Guid.NewGuid();

        var result = new TaskResult
        {
            TaskId  = Guid.NewGuid(),
            PlanId  = planId,
            Success = true,
            Output  = "ok"
        };

        using var cts = new CancellationTokenSource(2000);
        await notifier.PublishAsync(result, cts.Token);

        await hub.Clients.Group(Arg.Any<string>())
            .DidNotReceive()
            .OnPlanWaitingForHITL(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task NotifyResolvedAsync_PushesResumedResultToChannel()
    {
        var (notifier, _) = Build();
        var planId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        using var cts = new CancellationTokenSource(3000);
        await notifier.NotifyResolvedAsync(taskId, planId, "req-abc", approved: true, cts.Token);

        TaskResult? resumed = null;
        await foreach (var r in notifier.SubscribeAsync(planId, cts.Token))
        {
            resumed = r;
            break;
        }

        Assert.NotNull(resumed);
        Assert.Equal(taskId, resumed!.TaskId);
        Assert.Equal(planId, resumed.PlanId);
    }
}

[Trait("Phase", "F22")]
public class F22_TS01_SignalRCompletionNotifier_Offensive
{
    private static SignalRCompletionNotifier Build()
    {
        var hub         = Substitute.For<IHubContext<AgentHub, IAgentClient>>();
        var clients     = Substitute.For<IHubClients<IAgentClient>>();
        var groupClient = Substitute.For<IAgentClient>();
        hub.Clients.Returns(clients);
        clients.Group(Arg.Any<string>()).Returns(groupClient);
        return new SignalRCompletionNotifier(hub);
    }

    [Fact]
    public async Task TwoDifferentPlans_IndependentChannels()
    {
        var notifier = Build();
        var planA    = Guid.NewGuid();
        var planB    = Guid.NewGuid();

        using var cts = new CancellationTokenSource(3000);

        var rA = new TaskResult { TaskId = Guid.NewGuid(), PlanId = planA, Success = true, Output = "a" };
        var rB = new TaskResult { TaskId = Guid.NewGuid(), PlanId = planB, Success = true, Output = "b" };

        await notifier.PublishAsync(rA, cts.Token);
        await notifier.PublishAsync(rB, cts.Token);

        // Drain plan A — must receive only rA
        TaskResult? receivedA = null;
        await foreach (var r in notifier.SubscribeAsync(planA, cts.Token))
        {
            receivedA = r; break;
        }

        Assert.Equal("a", receivedA?.Output);
    }

    [Fact]
    public async Task SubscribeAsync_EmptyPlan_BlocksUntilCancelled()
    {
        var notifier = Build();
        var planId   = Guid.NewGuid();

        using var cts = new CancellationTokenSource(200); // quick timeout

        int count = 0;
        try
        {
            await foreach (var _ in notifier.SubscribeAsync(planId, cts.Token))
                count++;
        }
        catch (OperationCanceledException) { /* expected: channel blocks until cancelled */ }

        Assert.Equal(0, count);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// TS02  InMemoryCompletionNotifier — baseline verification
// ══════════════════════════════════════════════════════════════════════════════

[Trait("Phase", "F22")]
public class F22_TS02_InMemoryCompletionNotifier
{
    [Fact]
    public async Task PublishAndSubscribe_RoundTrip()
    {
        var notifier = new InMemoryCompletionNotifier();
        var planId   = Guid.NewGuid();

        var result = new TaskResult
        {
            TaskId  = Guid.NewGuid(),
            PlanId  = planId,
            Success = true,
            Output  = "hello"
        };

        using var cts = new CancellationTokenSource(3000);
        await notifier.PublishAsync(result, cts.Token);

        TaskResult? received = null;
        await foreach (var r in notifier.SubscribeAsync(planId, cts.Token))
        {
            received = r; break;
        }

        Assert.NotNull(received);
        Assert.Equal("hello", received!.Output);
    }

    [Fact]
    public async Task MultipleResults_OrderPreserved()
    {
        var notifier = new InMemoryCompletionNotifier();
        var planId   = Guid.NewGuid();

        using var cts = new CancellationTokenSource(3000);

        await notifier.PublishAsync(new TaskResult { TaskId = Guid.NewGuid(), PlanId = planId, Success = true, Output = "1" }, cts.Token);
        await notifier.PublishAsync(new TaskResult { TaskId = Guid.NewGuid(), PlanId = planId, Success = true, Output = "2" }, cts.Token);
        await notifier.PublishAsync(new TaskResult { TaskId = Guid.NewGuid(), PlanId = planId, Success = true, Output = "3" }, cts.Token);

        var outputs = new List<string>();
        await foreach (var r in notifier.SubscribeAsync(planId, cts.Token))
        {
            outputs.Add(r.Output);
            if (outputs.Count == 3) break;
        }

        Assert.Equal(new[] { "1", "2", "3" }, outputs);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// TS03  RealtimeGroupChatSwarm — instantiation contract
// ══════════════════════════════════════════════════════════════════════════════

[Trait("Phase", "F22")]
public class F22_TS03_RealtimeGroupChatSwarm_Contract
{
    private static IAgxiomeAgent MakeAgent(string id)
        => AGXIOME.Core.Agent.AgentFactory.CreateTestAgent(id);

    [Fact]
    public void Construct_WithTwoAgents_HasId()
    {
        var swarm = new RealtimeGroupChatSwarm(
            new[] { MakeAgent("a1"), MakeAgent("a2") });

        Assert.NotNull(swarm.Id);
        Assert.False(string.IsNullOrWhiteSpace(swarm.Id));
    }

    [Fact]
    public void Construct_LessThanTwoAgents_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new RealtimeGroupChatSwarm(new[] { MakeAgent("only-one") }));
    }

    [Fact]
    public void Construct_EmptyList_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new RealtimeGroupChatSwarm(Array.Empty<IAgxiomeAgent>()));
    }
}
