// F15-TS06 — Orchestrator Tests (15 tests)
// Validates PlanOrchestrator state management, events, and cancel/pause logic.
// Uses InMemory implementations and NSubstitute for LLM/spawner.

using AGXIOME.Orchestration.Abstractions;
using AGXIOME.Orchestration.Infrastructure;
using AGXIOME.Orchestration.Models;
using AGXIOME.Orchestration.Orchestrator;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace AGXIOME.Tests.F15;

// ═══════════════════════════════════════════════════════════════════
// TS06-A  OrchestratorOptions defaults (3 tests)
// ═══════════════════════════════════════════════════════════════════

public sealed class F15_TS06_OrchestratorOptionsTests
{
    [Fact] public async Task OrchestratorOptions_DefaultMaxParallel_Is4()
        => Assert.Equal(4, new OrchestratorOptions().MaxParallelTasks);

    [Fact] public async Task OrchestratorOptions_DefaultTimeout_Is5Minutes()
        => Assert.Equal(TimeSpan.FromMinutes(5), new OrchestratorOptions().DefaultTaskTimeout);

    [Fact] public async Task OrchestratorOptions_EnableAutoResume_DefaultTrue()
        => Assert.True(new OrchestratorOptions().EnableAutoResume);
}

// ═══════════════════════════════════════════════════════════════════
// TS06-B  PlanOrchestrator — StartAsync (5 tests)
// ═══════════════════════════════════════════════════════════════════

public sealed class F15_TS06_PlanOrchestratorStartTests
{
    private static PlanOrchestrator MakeOrchestrator(ILLMProvider? llm = null)
    {
        llm ??= Substitute.For<ILLMProvider>();
        var spawner = Substitute.For<IAgentSpawner>();
        spawner.SpawnAsync(Arg.Any<TaskSpec>(), Arg.Any<CancellationToken>())
               .Returns(Task.FromResult(Guid.NewGuid().ToString("N")));

        var cold = new InMemoryOrchestrationStore();
        var hot = new InMemoryHotStore();
        var notifier = new InMemoryCompletionNotifier();
        cold.Clear(); hot.Clear(); notifier.Clear();

        return new PlanOrchestrator(cold, hot, notifier, spawner, llm,
            NullLogger<PlanOrchestrator>.Instance);
    }

    [Fact] public async Task StartAsync_ReturnsNonEmptyGuid()
    {
        var llm = Substitute.For<ILLMProvider>();
        // Respond with empty array so the loop terminates immediately
        llm.CompleteAsync(Arg.Any<LLMCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LLMWIKYResponse("[]"));

        var orch = MakeOrchestrator(llm);
        var planId = await orch.StartAsync("test request", "session-1");
        Assert.NotEqual(Guid.Empty, planId);
    }

    [Fact] public async Task StartAsync_TwoCalls_ReturnDistinctPlanIds()
    {
        var llm = Substitute.For<ILLMProvider>();
        llm.CompleteAsync(Arg.Any<LLMCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LLMWIKYResponse("[]"));

        var orch = MakeOrchestrator(llm);
        var id1 = await orch.StartAsync("r1", "s1");
        var id2 = await orch.StartAsync("r2", "s1");
        Assert.NotEqual(id1, id2);
    }

    [Fact] public async Task GetStateAsync_UnknownPlan_ReturnsNull()
    {
        var orch = MakeOrchestrator();
        var state = await orch.GetStateAsync(Guid.NewGuid());
        Assert.Null(state);
    }

    [Fact] public async Task CancelAsync_UnknownPlan_DoesNotThrow()
    {
        var orch = MakeOrchestrator();
        var ex = await Record.ExceptionAsync(() => orch.CancelAsync(Guid.NewGuid()));
        Assert.Null(ex);
    }

    [Fact] public async Task WatchAsync_CancelledImmediately_YieldsNoEvents()
    {
        var orch = MakeOrchestrator();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var events = new List<OrchestratorEvent>();
        try
        {
            await foreach (var e in orch.WatchAsync(Guid.NewGuid(), cts.Token))
                events.Add(e);
        }
        catch (OperationCanceledException) { }

        Assert.Empty(events);
    }
}

// ═══════════════════════════════════════════════════════════════════
// TS06-C  OrchestratorEvent hierarchy (4 tests)
// ═══════════════════════════════════════════════════════════════════

public sealed class F15_TS06_OrchestratorEventTests
{
    [Fact] public async Task PlanStartedEvent_IsOrchestratorEvent()
    {
        var e = new PlanStartedEvent(Guid.NewGuid(), DateTimeOffset.UtcNow);
        Assert.IsAssignableFrom<OrchestratorEvent>(e);
    }

    [Fact] public async Task PlanCompletedEvent_HasFinalSummary()
    {
        var e = new PlanCompletedEvent(Guid.NewGuid(), "summary text", DateTimeOffset.UtcNow);
        Assert.Equal("summary text", e.Summary);
    }

    [Fact] public async Task TaskSpawnedEvent_HasStepIndex()
    {
        var e = new TaskSpawnedEvent(Guid.NewGuid(), Guid.NewGuid(), 2, DateTimeOffset.UtcNow);
        Assert.Equal(2, e.StepIndex);
    }

    [Fact] public async Task StepAdvancedEvent_HasPrevAndNextStep()
    {
        var e = new StepAdvancedEvent(Guid.NewGuid(), 0, 1, DateTimeOffset.UtcNow);
        Assert.Equal(0, e.OldStep);
        Assert.Equal(1, e.NewStep);
    }
}

// ═══════════════════════════════════════════════════════════════════
// TS06-D  PlanResumeService (3 tests)
// ═══════════════════════════════════════════════════════════════════

public sealed class F15_TS06_PlanResumeServiceTests
{
    [Fact] public async Task StartAsync_NoActivePlans_DoesNotThrow()
    {
        var cold = new InMemoryOrchestrationStore(); cold.Clear();
        var orch = Substitute.For<IPlanOrchestrator>();
        var svc = new PlanResumeService(orch, cold, NullLogger<PlanResumeService>.Instance);
        var ex = await Record.ExceptionAsync(() => svc.StartAsync(CancellationToken.None));
        Assert.Null(ex);
    }

    [Fact] public async Task StopAsync_DoesNotThrow()
    {
        var cold = new InMemoryOrchestrationStore(); cold.Clear();
        var svc = new PlanResumeService(Substitute.For<IPlanOrchestrator>(), cold,
            NullLogger<PlanResumeService>.Instance);
        var ex = await Record.ExceptionAsync(() => svc.StopAsync(CancellationToken.None));
        Assert.Null(ex);
    }

    [Fact] public async Task StartAsync_WithActivePlan_CallsResumeOnce()
    {
        var cold = new InMemoryOrchestrationStore(); cold.Clear();
        var plan = new PlanState
        {
            PlanId = Guid.NewGuid(),
            OriginalRequest = "r",
            SessionId = "s",
            Tasks = new List<TaskSpec>(),
            CurrentStep = 0,
            Status = OrchestratorPlanStatus.Running
        };
        await cold.SavePlanAsync(plan);

        var orch = Substitute.For<IPlanOrchestrator>();
        var svc = new PlanResumeService(orch, cold, NullLogger<PlanResumeService>.Instance);
        await svc.StartAsync(CancellationToken.None);

        await orch.Received(1).ResumeAsync(plan.PlanId, Arg.Any<CancellationToken>());
    }
}
