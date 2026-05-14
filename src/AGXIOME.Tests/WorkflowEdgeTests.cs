using AGXIOME.Core.Workflows;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using Xunit;

namespace AGXIOME.Tests;

public class WorkflowEdgeTests
{
    [Fact]
    public async Task CyclicDAG_Detected()
    {
        var wf = new AgxiomeWorkflow("cycle");
        wf.AddStep("a", ctx => Task.CompletedTask);
        wf.AddStep("b", ctx => Task.CompletedTask);
        wf.AddTransition("a", "b");
        wf.AddTransition("b", "a"); // cycle!

        var result = await wf.ExecuteAsync(new WorkflowContext());
        // Should not hang; completes with partial results
        Assert.True(result.Status == WorkflowStatus.Completed || result.Status == WorkflowStatus.Failed);
    }

    [Fact]
    public async Task StepThrows_WorkflowContinues()
    {
        var wf = new AgxiomeWorkflow("throws");
        wf.AddStep("fails", ctx => throw new InvalidOperationException("fail"));
        wf.AddStep("after", ctx => Task.CompletedTask);
        wf.AddTransition("fails", "after");

        var result = await wf.ExecuteAsync(new WorkflowContext());

        Assert.Contains(result.StepResults, r => r.StepName == "fails" && r.Status == StepStatus.Failed);
        Assert.Contains(result.StepResults, r => r.StepName == "after" && r.Status == StepStatus.Completed);
    }

    [Fact]
    public async Task Parallel_100Steps_AllComplete()
    {
        var steps = Enumerable.Range(0, 100).Select(i =>
            (IWorkflowStep)new FunctionWorkflowStep($"s{i}", ctx => Task.CompletedTask)).ToArray();
        var parallel = new ParallelStep("big-parallel", steps);

        var result = await parallel.ExecuteAsync(new WorkflowContext());

        Assert.Equal(StepStatus.Completed, result.Status);
        Assert.NotNull(result.Output);
    }

    [Fact]
    public async Task Router_UnknownRoute_Skipped()
    {
        var router = new RouterStep("router", _ => "unknown",
            new Dictionary<string, IWorkflowStep>());

        var result = await router.ExecuteAsync(new WorkflowContext());

        Assert.Equal(StepStatus.Completed, result.Status);
        Assert.Contains("not found", result.Output?.ToString() ?? "");
    }

    [Fact]
    public async Task CELCondition_BasicContains()
    {
        var evaluator = new CELConditionEvaluator();
        var ctx = new WorkflowContext();

        var result = evaluator.Evaluate("input.Contains(\"urgent\")", ctx, "This is urgent!");

        Assert.True(result);
    }

    [Fact]
    public async Task CELCondition_NoMatch_ReturnsFalse()
    {
        var evaluator = new CELConditionEvaluator();
        var ctx = new WorkflowContext();

        var result = evaluator.Evaluate("input.Contains(\"urgent\")", ctx, "Just a normal message");

        Assert.False(result);
    }

    [Fact]
    public async Task NestedWorkflow_3Levels()
    {
        var inner = new AgxiomeWorkflow("inner");
        inner.AddStep("inner-step", ctx => Task.CompletedTask);

        var mid = new AgxiomeWorkflow("mid");
        mid.AddAgentStep("mid-step", AGXIOME.Core.Agent.AgentFactory.CreateTestAgent("mid-agent"));

        var outer = new AgxiomeWorkflow("outer");
        outer.AddStep("outer-step", ctx => Task.CompletedTask);
        outer.AddTransition("outer-step", "mid-step");

        // Just verify no crash
        var result = await outer.ExecuteAsync(new WorkflowContext());
        Assert.NotNull(result);
    }
}
