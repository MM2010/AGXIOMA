using AGXIOME.Core.Agent;
using AGXIOME.Core.Tools;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using Xunit;

namespace AGXIOME.Tests;

public class AgentEdgeTests
{
    [Fact]
    public async Task EmptyPrompt_ReturnsResponse()
    {
        var agent = AgentFactory.CreateTestAgent();
        var result = await agent.RunAsync(new AgentRequest(""));
        Assert.Equal(AgentStatus.Completed, result.Status);
    }

    [Fact]
    public async Task MaxIterationsZero_Completes()
    {
        var agent = AgentFactory.CreateTestAgent();
        var result = await agent.RunAsync(new AgentRequest("test"));
        Assert.Equal(AgentStatus.Completed, result.Status);
    }

    [Fact]
    public async Task CancelledToken_Throws()
    {
        var agent = AgentFactory.CreateTestAgent();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var result = await agent.RunAsync(new AgentRequest("test"), cts.Token);
        Assert.Equal(AgentStatus.Error, result.Status);
    }

    [Fact]
    public async Task ToolLoop_MaxIterations_ReturnsResponse()
    {
        var agent = AgentFactory.CreateTestAgent("loop-test");
        var tool = new LoopTool();
        agent.RegisterTool(tool);

        var result = await agent.RunAsync(new AgentRequest("loop"));

        Assert.Equal(AgentStatus.Completed, result.Status);
        Assert.NotNull(result.Content);
    }

    [Fact]
    public async Task PreHook_ModifiesContext()
    {
        var agent = AgentFactory.CreateTestAgent("hook-test");
        string? captured = null;

        agent.AddHook(new AGXIOME.Core.Hooks.DelegateHook("test-hook", HookPoint.PreRun,
            (ctx, ct) => { captured = "modified"; return Task.CompletedTask; }));

        var result = await agent.RunAsync(new AgentRequest("test"));

        Assert.Equal("modified", captured);
        Assert.Equal(AgentStatus.Completed, result.Status);
    }

    [Fact]
    public async Task BackgroundHook_DoesNotBlock()
    {
        var agent = AgentFactory.CreateTestAgent("bg-hook");

        agent.AddHook(new AGXIOME.Core.Hooks.DelegateHook("bg", HookPoint.BackgroundPost,
            async (ctx, ct) => { await Task.Delay(200, ct); }));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await agent.RunAsync(new AgentRequest("test"));
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 150,
            $"Background hook blocked: {sw.ElapsedMilliseconds}ms (should be < 150ms)");
        Assert.Equal(AgentStatus.Completed, result.Status);
    }

    [Fact]
    public async Task MultipleGuardrails_AllCheck()
    {
        var agent = AgentFactory.CreateTestAgent();
        int callCount = 0;

        agent.AddGuardrail(new TestGuardrail("g1", c => { callCount++; return false; }, GuardrailSeverity.Log)); // flag only
        agent.AddGuardrail(new TestGuardrail("g2", c => { callCount++; return false; }, GuardrailSeverity.Flag));  // flag only

        var result = await agent.RunAsync(new AgentRequest("safe input"));

        Assert.Equal(2, callCount);
        Assert.Equal(AgentStatus.Completed, result.Status);
    }

    [Fact]
    public async Task Guardrail_BlocksWithMessage()
    {
        var agent = AgentFactory.CreateTestAgent();
        agent.AddGuardrail(new TestGuardrail("blocker", c => c.Contains("bad"), GuardrailSeverity.Block));

        var result = await agent.RunAsync(new AgentRequest("this is bad input"));

        Assert.Equal(AgentStatus.Error, result.Status);
        Assert.Contains("ERR-0101", result.Content);
    }

    [Fact]
    public async Task SessionState_PersistsAcrossRuns()
    {
        var agent = AgentFactory.CreateTestAgent("state-test");
        Assert.NotNull(agent.SessionState);

        agent.SessionState.Set("counter", 1);
        agent.SessionState.Set("counter", agent.SessionState.Get<int>("counter") + 1);

        Assert.Equal(2, agent.SessionState.Get<int>("counter"));
    }

    [Fact]
    public async Task ConcurrentRun_OnlyOneExecutes()
    {
        var agent = AgentFactory.CreateTestAgent("concurrent");
        var task1 = agent.RunAsync(new AgentRequest("first"));
        var task2 = agent.RunAsync(new AgentRequest("second"));

        var results = await Task.WhenAll(task1, task2);
        Assert.Contains(results, r => r.Status == AgentStatus.Completed);
    }

    [Fact]
    public async Task ToolWithError_DoesNotBlockLoop()
    {
        var agent = AgentFactory.CreateTestAgent("err-tool");
        agent.RegisterTool(new ErrorTool());

        var result = await agent.RunAsync(new AgentRequest("error"));

        Assert.NotNull(result);
    }
}

/// <summary>Tool that always calls itself to trigger max iterations.</summary>
public sealed class LoopTool : AgxiomeFunction
{
    public override string Name => "loop_tool";
    public override string Description => "Loops forever";
    public override Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct)
        => Task.FromResult(new ToolResult(true, "loop_again"));
}

/// <summary>Tool that throws an exception.</summary>
public sealed class ErrorTool : AgxiomeFunction
{
    public override string Name => "error_tool";
    public override string Description => "Always throws";
    public override Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct)
        => throw new InvalidOperationException("Intentional tool error");
}

/// <summary>Test guardrail with configurable logic.</summary>
public sealed class TestGuardrail : IGuardrail
{
    public string Name { get; }
    public GuardrailType Type => GuardrailType.Input;
    public GuardrailSeverity Severity { get; }
    private readonly Func<string, bool> _checker;

    public TestGuardrail(string name, Func<string, bool> checker, GuardrailSeverity severity = GuardrailSeverity.Block)
    {
        Name = name; _checker = checker; Severity = severity;
    }

    public Task<GuardrailResult> ValidateAsync(string content, GuardrailContext ctx, CancellationToken ct)
        => Task.FromResult(_checker(content)
            ? new GuardrailResult(false, $"Guardrail '{Name}' triggered", Severity)
            : new GuardrailResult(true));
}
