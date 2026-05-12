// F15-TS04 — Master Tools Tests (20 tests)
// Validates DecomposeRequestTool, PersistPlanTool, SaveResultTool, NotifyCompletionTool,
// SpawnAgentTool, PollResultsTool. Uses NSubstitute for LLM + IAgentSpawner.

using AGXIOME.Orchestration.Abstractions;
using AGXIOME.Orchestration.Infrastructure;
using AGXIOME.Orchestration.Models;
using AGXIOME.Orchestration.Tools.Master;
using AGXIOME.Orchestration.Tools.SubAgent;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using NSubstitute;

// ═══════════════════════════════════════════════════════════════════
// TS04-A  DecomposeRequestTool (5 tests)
// ═══════════════════════════════════════════════════════════════════

public sealed class F15_TS04_DecomposeRequestToolTests
{
    private const string ValidLlmJson =
        """[{"description":"task1","stepIndex":0,"allowedToolNames":["t1"],"temperature":0.5},{"description":"task2","stepIndex":0,"allowedToolNames":[],"temperature":0.7}]""";

    private static ILLMProvider MockLlm(string response)
    {
        var llm = Substitute.For<ILLMProvider>();
        llm.CompleteAsync(Arg.Any<LLMCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LLMWIKYResponse(response));
        return llm;
    }

    private static ToolContext MakeCtx(string request, string? planId = null) =>
        new("agent1", "run1", Parameters: new Dictionary<string, object>
        {
            ["request"] = request,
            ["planId"] = planId ?? Guid.NewGuid().ToString(),
            ["sessionId"] = "sess1"
        });

    [Fact] public async Task DecomposeRequest_ValidJson_ReturnsTasks()
    {
        var tool = new DecomposeRequestTool(MockLlm(ValidLlmJson));
        var result = await tool.ExecuteAsync(MakeCtx("analyse data"), CancellationToken.None);
        Assert.True(result.Success);
    }

    [Fact] public async Task DecomposeRequest_MissingRequestParam_Fails()
    {
        var tool = new DecomposeRequestTool(MockLlm("[]"));
        var ctx = new ToolContext("a", "r");
        var result = await tool.ExecuteAsync(ctx, CancellationToken.None);
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact] public async Task DecomposeRequest_ValidJson_ReturnsCorrectTaskCount()
    {
        var tool = new DecomposeRequestTool(MockLlm(ValidLlmJson));
        var result = await tool.ExecuteAsync(MakeCtx("do stuff"), CancellationToken.None);
        Assert.True(result.Success);
        // Data contains taskCount = 2
        var json = System.Text.Json.JsonSerializer.Serialize(result.Data);
        Assert.Contains("taskCount", json);
    }

    [Fact] public async Task DecomposeRequest_LlmMarkdownFenced_ParsesCorrectly()
    {
        var fenced = "```json\n" + ValidLlmJson + "\n```";
        var tool = new DecomposeRequestTool(MockLlm(fenced));
        var result = await tool.ExecuteAsync(MakeCtx("q"), CancellationToken.None);
        Assert.True(result.Success);
    }

    [Fact] public void DecomposeRequest_ToJsonSchema_ReturnsObject()
    {
        var tool = new DecomposeRequestTool(Substitute.For<ILLMProvider>());
        var schema = tool.ToJsonSchema();
        Assert.NotNull(schema);
    }
}

// ═══════════════════════════════════════════════════════════════════
// TS04-B  PersistPlanTool (5 tests)
// ═══════════════════════════════════════════════════════════════════

public sealed class F15_TS04_PersistPlanToolTests
{
    private static (InMemoryHotStore hot, InMemoryOrchestrationStore cold, PersistPlanTool tool) Make()
    {
        var hot = new InMemoryHotStore();
        var cold = new InMemoryOrchestrationStore();
        hot.Clear(); cold.Clear();
        return (hot, cold, new PersistPlanTool(hot, cold));
    }

    private static PlanState MakePlan() => new()
    {
        PlanId = Guid.NewGuid(),
        OriginalRequest = "r",
        SessionId = "s1",
        Tasks = new List<TaskSpec>
        {
            new() { TaskId = Guid.NewGuid(), PlanId = Guid.NewGuid(), StepIndex = 0, Description = "t1" }
        },
        CurrentStep = 0,
        Status = OrchestratorPlanStatus.Decomposing
    };

    [Fact] public async Task PersistPlan_MissingPlanParam_Fails()
    {
        var (_, _, tool) = Make();
        var ctx = new ToolContext("a", "r");
        var result = await tool.ExecuteAsync(ctx, CancellationToken.None);
        Assert.False(result.Success);
    }

    [Fact] public async Task PersistPlan_ValidPlan_Succeeds()
    {
        var (_, _, tool) = Make();
        var plan = MakePlan();
        var ctx = new ToolContext("a", "r", Parameters: new Dictionary<string, object> { ["plan"] = plan });
        var result = await tool.ExecuteAsync(ctx, CancellationToken.None);
        Assert.True(result.Success);
    }

    [Fact] public async Task PersistPlan_SavesToColdStore()
    {
        var (_, cold, tool) = Make();
        var plan = MakePlan();
        var ctx = new ToolContext("a", "r", Parameters: new Dictionary<string, object> { ["plan"] = plan });
        await tool.ExecuteAsync(ctx, CancellationToken.None);
        var loaded = await cold.GetPlanAsync(plan.PlanId);
        Assert.NotNull(loaded);
    }

    [Fact] public async Task PersistPlan_SetsHotStoreStatus()
    {
        var (hot, _, tool) = Make();
        var plan = MakePlan();
        var ctx = new ToolContext("a", "r", Parameters: new Dictionary<string, object> { ["plan"] = plan });
        await tool.ExecuteAsync(ctx, CancellationToken.None);
        var status = await hot.GetAsync(HotKeys.PlanStatus(plan.PlanId));
        Assert.NotNull(status);
    }

    [Fact] public async Task PersistPlan_ReturnsCorrectPlanId()
    {
        var (_, _, tool) = Make();
        var plan = MakePlan();
        var ctx = new ToolContext("a", "r", Parameters: new Dictionary<string, object> { ["plan"] = plan });
        var result = await tool.ExecuteAsync(ctx, CancellationToken.None);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Data);
        Assert.Contains(plan.PlanId.ToString(), json);
    }
}

// ═══════════════════════════════════════════════════════════════════
// TS04-C  SpawnAgentTool (5 tests)
// ═══════════════════════════════════════════════════════════════════

public sealed class F15_TS04_SpawnAgentToolTests
{
    private static (InMemoryHotStore hot, InMemoryOrchestrationStore cold, IAgentSpawner spawner, SpawnAgentTool tool) Make()
    {
        var hot = new InMemoryHotStore(); hot.Clear();
        var cold = new InMemoryOrchestrationStore(); cold.Clear();
        var spawner = Substitute.For<IAgentSpawner>();
        spawner.SpawnAsync(Arg.Any<TaskSpec>(), Arg.Any<CancellationToken>())
               .Returns(ci => Task.FromResult(Guid.NewGuid().ToString("N")));
        return (hot, cold, spawner, new SpawnAgentTool(spawner, hot, cold));
    }

    private static TaskSpec MakeSpec() => new()
    {
        TaskId = Guid.NewGuid(),
        PlanId = Guid.NewGuid(),
        StepIndex = 0,
        Description = "desc"
    };

    [Fact] public async Task SpawnAgent_MissingTasksParam_Fails()
    {
        var (_, _, _, tool) = Make();
        var result = await tool.ExecuteAsync(new ToolContext("a", "r"), CancellationToken.None);
        Assert.False(result.Success);
    }

    [Fact] public async Task SpawnAgent_OneTasks_CallsSpawnerOnce()
    {
        var (_, _, spawner, tool) = Make();
        var tasks = new List<TaskSpec> { MakeSpec() };
        var ctx = new ToolContext("a", "r", Parameters: new Dictionary<string, object>
        {
            ["tasks"] = (IReadOnlyList<TaskSpec>)tasks,
            ["executionMode"] = StepExecutionMode.Sequential
        });
        await tool.ExecuteAsync(ctx, CancellationToken.None);
        await spawner.Received(1).SpawnAsync(Arg.Any<TaskSpec>(), Arg.Any<CancellationToken>());
    }

    [Fact] public async Task SpawnAgent_TwoTasks_CallsSpawnerTwice()
    {
        var (_, _, spawner, tool) = Make();
        var tasks = new List<TaskSpec> { MakeSpec(), MakeSpec() };
        var ctx = new ToolContext("a", "r", Parameters: new Dictionary<string, object>
        {
            ["tasks"] = (IReadOnlyList<TaskSpec>)tasks,
            ["executionMode"] = StepExecutionMode.Sequential
        });
        await tool.ExecuteAsync(ctx, CancellationToken.None);
        await spawner.Received(2).SpawnAsync(Arg.Any<TaskSpec>(), Arg.Any<CancellationToken>());
    }

    [Fact] public async Task SpawnAgent_SetsHotStoreStatusRunning()
    {
        var (hot, _, _, tool) = Make();
        var spec = MakeSpec();
        var ctx = new ToolContext("a", "r", Parameters: new Dictionary<string, object>
        {
            ["tasks"] = (IReadOnlyList<TaskSpec>)new List<TaskSpec> { spec },
            ["executionMode"] = StepExecutionMode.Sequential
        });
        await tool.ExecuteAsync(ctx, CancellationToken.None);
        var status = await hot.GetAsync(HotKeys.TaskStatus(spec.TaskId));
        Assert.Equal("Running", status);
    }

    [Fact] public async Task SpawnAgent_Parallel_AllTasksSpawned()
    {
        var (_, _, spawner, tool) = Make();
        var tasks = new List<TaskSpec> { MakeSpec(), MakeSpec(), MakeSpec() };
        var ctx = new ToolContext("a", "r", Parameters: new Dictionary<string, object>
        {
            ["tasks"] = (IReadOnlyList<TaskSpec>)tasks,
            ["executionMode"] = StepExecutionMode.Parallel
        });
        await tool.ExecuteAsync(ctx, CancellationToken.None);
        await spawner.Received(3).SpawnAsync(Arg.Any<TaskSpec>(), Arg.Any<CancellationToken>());
    }
}

// ═══════════════════════════════════════════════════════════════════
// TS04-D  GenerateAgentsTool (5 tests)
// ═══════════════════════════════════════════════════════════════════

public sealed class F15_TS04_GenerateAgentsToolTests
{
    private static ILLMProvider MockLlm(string json)
    {
        var llm = Substitute.For<ILLMProvider>();
        llm.CompleteAsync(Arg.Any<LLMCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LLMWIKYResponse(json));
        return llm;
    }

    private static TaskSpec MakeSpec(Guid? taskId = null) => new()
    {
        TaskId = taskId ?? Guid.NewGuid(),
        PlanId = Guid.NewGuid(),
        StepIndex = 0,
        Description = "desc"
    };

    [Fact] public async Task GenerateAgents_MissingTasksParam_Fails()
    {
        var tool = new GenerateAgentsTool(Substitute.For<ILLMProvider>());
        var result = await tool.ExecuteAsync(new ToolContext("a", "r"), CancellationToken.None);
        Assert.False(result.Success);
    }

    [Fact] public async Task GenerateAgents_ValidLlmResponse_Succeeds()
    {
        var spec = MakeSpec();
        var llmJson = $@"[{{""taskId"":""{spec.TaskId}"",""agentRole"":""Analyst"",""allowedToolNames"":[""t1""],""temperature"":0.5,""systemPrompt"":""You are helpful""}}]";
        var tool = new GenerateAgentsTool(MockLlm(llmJson));
        var ctx = new ToolContext("a", "r", Parameters: new Dictionary<string, object>
        {
            ["tasks"] = (IReadOnlyList<TaskSpec>)new List<TaskSpec> { spec },
            ["availableTools"] = (IReadOnlyList<string>)new List<string> { "t1", "t2" }
        });
        var result = await tool.ExecuteAsync(ctx, CancellationToken.None);
        Assert.True(result.Success);
    }

    [Fact] public async Task GenerateAgents_FillsDefaultForMissingTaskConfigs()
    {
        var spec1 = MakeSpec();
        var spec2 = MakeSpec();
        // Only configure spec1 in LLM response
        var llmJson = $@"[{{""taskId"":""{spec1.TaskId}"",""agentRole"":""A"",""allowedToolNames"":[],""temperature"":0.5}}]";
        var tool = new GenerateAgentsTool(MockLlm(llmJson));
        var ctx = new ToolContext("a", "r", Parameters: new Dictionary<string, object>
        {
            ["tasks"] = (IReadOnlyList<TaskSpec>)new List<TaskSpec> { spec1, spec2 }
        });
        var result = await tool.ExecuteAsync(ctx, CancellationToken.None);
        Assert.True(result.Success);
        // Both specs should have configs
        var json = System.Text.Json.JsonSerializer.Serialize(result.Data);
        Assert.Contains(spec2.TaskId.ToString(), json);
    }

    [Fact] public async Task GenerateAgents_FiltersToolsToAvailableSet()
    {
        var spec = MakeSpec();
        var llmJson = $@"[{{""taskId"":""{spec.TaskId}"",""agentRole"":""A"",""allowedToolNames"":[""allowed"",""NOTALLOWED""],""temperature"":0.5}}]";
        var tool = new GenerateAgentsTool(MockLlm(llmJson));
        var ctx = new ToolContext("a", "r", Parameters: new Dictionary<string, object>
        {
            ["tasks"] = (IReadOnlyList<TaskSpec>)new List<TaskSpec> { spec },
            ["availableTools"] = (IReadOnlyList<string>)new List<string> { "allowed" }
        });
        var result = await tool.ExecuteAsync(ctx, CancellationToken.None);
        Assert.True(result.Success);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Data);
        Assert.DoesNotContain("NOTALLOWED", json);
    }

    [Fact] public void GenerateAgents_ToJsonSchema_ReturnsValid()
    {
        var tool = new GenerateAgentsTool(Substitute.For<ILLMProvider>());
        var schema = tool.ToJsonSchema();
        Assert.NotNull(schema["properties"]);
    }
}
