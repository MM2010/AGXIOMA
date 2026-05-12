// F15-TS01 — Domain Model Tests (15 tests)
// Validates PlanState, TaskSpec, TaskResult computed properties, enums, and record semantics.

using AGXIOME.Orchestration.Models;
using TaskResult = AGXIOME.Orchestration.Models.TaskResult;

namespace AGXIOME.Tests.F15;

// ═══════════════════════════════════════════════════════════════════
// TS01-A  Enum values (5 tests)
// ═══════════════════════════════════════════════════════════════════

public sealed class F15_TS01_EnumTests
{
    [Fact] public void OrchestratorTaskStatus_AllValuesDefined()
    {
        var expected = new[] { "Pending", "Running", "Done", "Failed", "Skipped", "Cancelled" };
        var defined = Enum.GetNames<OrchestratorTaskStatus>();
        foreach (var e in expected)
            Assert.Contains(e, defined);
    }

    [Fact] public void OrchestratorPlanStatus_AllValuesDefined()
    {
        var expected = new[] { "Decomposing", "Running", "Paused", "Completed", "Failed", "Cancelled" };
        var defined = Enum.GetNames<OrchestratorPlanStatus>();
        foreach (var e in expected)
            Assert.Contains(e, defined);
    }

    [Fact] public void StepExecutionMode_HasParallelAndSequential()
    {
        Assert.True(Enum.IsDefined(typeof(StepExecutionMode), StepExecutionMode.Parallel));
        Assert.True(Enum.IsDefined(typeof(StepExecutionMode), StepExecutionMode.Sequential));
    }

    [Fact] public void AdvanceDecision_HasAllFourValues()
    {
        var names = Enum.GetNames<AdvanceDecision>();
        Assert.Contains("Advance", names);
        Assert.Contains("Complete", names);
        Assert.Contains("Fail", names);
        Assert.Contains("Retry", names);
    }

    [Fact] public void AllEnumValues_AreUnique()
    {
        var taskVals = Enum.GetValues<OrchestratorTaskStatus>().Cast<int>().ToList();
        Assert.Equal(taskVals.Count, taskVals.Distinct().Count());
        var planVals = Enum.GetValues<OrchestratorPlanStatus>().Cast<int>().ToList();
        Assert.Equal(planVals.Count, planVals.Distinct().Count());
    }
}

// ═══════════════════════════════════════════════════════════════════
// TS01-B  TaskSpec record (4 tests)
// ═══════════════════════════════════════════════════════════════════

public sealed class F15_TS01_TaskSpecTests
{
    private static TaskSpec Make(int step = 0) => new()
    {
        TaskId = Guid.NewGuid(),
        PlanId = Guid.NewGuid(),
        StepIndex = step,
        Description = "Test task"
    };

    [Fact] public void TaskSpec_DefaultStatus_IsPending()
        => Assert.Equal(OrchestratorTaskStatus.Pending, Make().Status);

    [Fact] public void TaskSpec_DefaultTemperature_Is07()
        => Assert.Equal(0.7f, Make().Temperature);

    [Fact] public void TaskSpec_WithExpression_IsNonDestructive()
    {
        var original = Make(step: 1);
        var updated = original with { Status = OrchestratorTaskStatus.Running, StartedAt = DateTimeOffset.UtcNow };
        Assert.Equal(OrchestratorTaskStatus.Pending, original.Status);
        Assert.Equal(OrchestratorTaskStatus.Running, updated.Status);
        Assert.Equal(original.TaskId, updated.TaskId);
    }

    [Fact] public void TaskSpec_DefaultMaxTokens_Is4096()
        => Assert.Equal(4096, Make().MaxTokens);
}

// ═══════════════════════════════════════════════════════════════════
// TS01-C  TaskResult record (3 tests)
// ═══════════════════════════════════════════════════════════════════

public sealed class F15_TS01_TaskResultTests
{
    private static TaskResult MakeResult(bool success = true) => new()
    {
        TaskId = Guid.NewGuid(),
        PlanId = Guid.NewGuid(),
        Success = success,
        Output = "done",
        CompletedAt = DateTimeOffset.UtcNow,
        ElapsedTime = TimeSpan.FromSeconds(1)
    };

    [Fact] public void TaskResult_Success_HasNoErrorMessage()
        => Assert.Null(MakeResult(true).ErrorMessage);

    [Fact] public void TaskResult_Failed_CanHaveErrorMessage()
    {
        var r = MakeResult(false) with { ErrorMessage = "oops" };
        Assert.Equal("oops", r.ErrorMessage);
    }

    [Fact] public void TaskResult_RetryCount_DefaultsToZero()
        => Assert.Equal(0, MakeResult().RetryCount);
}

// ═══════════════════════════════════════════════════════════════════
// TS01-D  PlanState computed properties (3 tests)
// ═══════════════════════════════════════════════════════════════════

public sealed class F15_TS01_PlanStateTests
{
    private static TaskSpec MakeSpec(int step) => new()
    {
        TaskId = Guid.NewGuid(),
        PlanId = Guid.NewGuid(),
        StepIndex = step,
        Description = $"Step {step} task"
    };

    private static PlanState MakePlan(int steps = 2)
    {
        var tasks = Enumerable.Range(0, steps).Select(MakeSpec).ToList();
        return new PlanState
        {
            PlanId = Guid.NewGuid(),
            OriginalRequest = "test",
            SessionId = "s1",
            Tasks = tasks,
            CurrentStep = 0,
            Status = OrchestratorPlanStatus.Running
        };
    }

    [Fact] public void PlanState_CurrentStepTasks_ReturnsCorrectSubset()
    {
        var plan = MakePlan(2);
        var step0 = plan.CurrentStepTasks;
        Assert.All(step0, t => Assert.Equal(0, t.StepIndex));
    }

    [Fact] public void PlanState_MaxStepIndex_IsCorrect()
        => Assert.Equal(1, MakePlan(2).MaxStepIndex);

    [Fact] public void PlanState_IsLastStep_TrueWhenOnMaxStep()
    {
        var plan = MakePlan(1) with { CurrentStep = 0 };
        Assert.True(plan.IsLastStep);
    }
}
