// F15-TS03 — Cold Store Contract Tests (10 tests)
// Validates InMemoryOrchestrationStore: plan CRUD, task status, results, pagination-like ops.

using AGXIOME.Orchestration.Infrastructure;
using AGXIOME.Orchestration.Models;
using TaskResult = AGXIOME.Orchestration.Models.TaskResult;

namespace AGXIOME.Tests.F15;

public sealed class F15_TS03_ColdStoreContractTests
{
    private static InMemoryOrchestrationStore Fresh() { var s = new InMemoryOrchestrationStore(); s.Clear(); return s; }

    private static PlanState MakePlan(Guid? id = null) => new()
    {
        PlanId = id ?? Guid.NewGuid(),
        OriginalRequest = "request",
        SessionId = "s1",
        Tasks = new List<TaskSpec>(),
        CurrentStep = 0,
        Status = OrchestratorPlanStatus.Running
    };

    private static TaskSpec MakeSpec(Guid planId, int step = 0) => new()
    {
        TaskId = Guid.NewGuid(),
        PlanId = planId,
        StepIndex = step,
        Description = "desc"
    };

    private static TaskResult MakeResult(Guid taskId, Guid planId, bool success = true) => new()
    {
        TaskId = taskId,
        PlanId = planId,
        Success = success,
        Output = "out",
        CompletedAt = DateTimeOffset.UtcNow,
        ElapsedTime = TimeSpan.FromSeconds(1)
    };

    [Fact] public async Task SaveAndGetPlan_Roundtrip()
    {
        var store = Fresh();
        var plan = MakePlan();
        await store.SavePlanAsync(plan);
        var loaded = await store.GetPlanAsync(plan.PlanId);
        Assert.NotNull(loaded);
        Assert.Equal(plan.PlanId, loaded!.PlanId);
    }

    [Fact] public async Task GetPlan_Missing_ReturnsNull()
    {
        var store = Fresh();
        Assert.Null(await store.GetPlanAsync(Guid.NewGuid()));
    }

    [Fact] public async Task UpdatePlanStatus_ChangesStatus()
    {
        var store = Fresh();
        var plan = MakePlan();
        await store.SavePlanAsync(plan);
        await store.UpdatePlanStatusAsync(plan.PlanId, OrchestratorPlanStatus.Completed);
        var loaded = await store.GetPlanAsync(plan.PlanId);
        Assert.Equal(OrchestratorPlanStatus.Completed, loaded!.Status);
    }

    [Fact] public async Task AdvancePlanStep_IncrementsStep()
    {
        var store = Fresh();
        var plan = MakePlan();
        await store.SavePlanAsync(plan);
        await store.AdvancePlanStepAsync(plan.PlanId, 1, new Dictionary<Guid, TaskResult>());
        var loaded = await store.GetPlanAsync(plan.PlanId);
        Assert.Equal(1, loaded!.CurrentStep);
    }

    [Fact] public async Task SaveAndGetTaskSpec_Roundtrip()
    {
        var store = Fresh();
        var plan = MakePlan();
        await store.SavePlanAsync(plan);
        var spec = MakeSpec(plan.PlanId);
        await store.SaveTaskSpecAsync(spec);
        var loaded = await store.GetTaskSpecAsync(spec.TaskId);
        Assert.NotNull(loaded);
        Assert.Equal(spec.TaskId, loaded!.TaskId);
    }

    [Fact] public async Task UpdateTaskStatus_ChangesStatus()
    {
        var store = Fresh();
        var plan = MakePlan();
        await store.SavePlanAsync(plan);
        var spec = MakeSpec(plan.PlanId);
        await store.SaveTaskSpecAsync(spec);
        await store.UpdateTaskStatusAsync(spec.TaskId, OrchestratorTaskStatus.Done);
        var loaded = await store.GetTaskSpecAsync(spec.TaskId);
        Assert.Equal(OrchestratorTaskStatus.Done, loaded!.Status);
    }

    [Fact] public async Task SaveAndGetResult_Roundtrip()
    {
        var store = Fresh();
        var planId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var result = MakeResult(taskId, planId);
        await store.SaveResultAsync(result);
        var loaded = await store.GetResultAsync(taskId);
        Assert.NotNull(loaded);
        Assert.Equal(taskId, loaded!.TaskId);
    }

    [Fact] public async Task GetResult_Missing_ReturnsNull()
    {
        var store = Fresh();
        Assert.Null(await store.GetResultAsync(Guid.NewGuid()));
    }

    [Fact] public async Task GetPlanResults_ReturnsAllSaved()
    {
        var store = Fresh();
        var planId = Guid.NewGuid();
        var r1 = MakeResult(Guid.NewGuid(), planId);
        var r2 = MakeResult(Guid.NewGuid(), planId);
        await store.SaveResultAsync(r1);
        await store.SaveResultAsync(r2);
        var results = await store.GetPlanResultsAsync(planId);
        Assert.Equal(2, results.Count);
    }

    [Fact] public async Task GetActivePlans_OnlyReturnsRunningAndPaused()
    {
        var store = Fresh();
        var running = MakePlan();
        var completed = MakePlan() with { Status = OrchestratorPlanStatus.Completed };
        await store.SavePlanAsync(running);
        await store.SavePlanAsync(completed);
        var active = await store.GetActivePlansAsync();
        Assert.Contains(active, p => p.PlanId == running.PlanId);
        Assert.DoesNotContain(active, p => p.PlanId == completed.PlanId);
    }
}
