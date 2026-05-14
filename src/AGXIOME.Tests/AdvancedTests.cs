using AGXIOME.Core.Workflows;
using AGXIOME.Core.Context;
using AGXIOME.Core.Agent;
using AGXIOME.Core.Teams;
using AGXIOME.Core.Skills;
using AGXIOME.Core.Scheduling;
using AGXIOME.LLMWIKY;
using AGXIOME.Storage.Chunkers;
using AGXIOME.Core.Hooks;
using AGXIOME.Shared.Config;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using Xunit;

namespace AGXIOME.Tests;

public class AdvancedTests
{
    // ── Workflow Tests ──

    [Fact]
    public async Task Workflow_SequentialSteps_ExecutesInOrder()
    {
        var wf = new AgxiomeWorkflow("test-wf");
        var order = new List<string>();

        wf.AddStep("step1", ctx => { order.Add("step1"); return Task.CompletedTask; });
        wf.AddStep("step2", ctx => { order.Add("step2"); return Task.CompletedTask; });
        wf.AddTransition("step1", "step2");

        var result = await wf.ExecuteAsync(new WorkflowContext());

        Assert.Equal(2, result.StepResults.Count);
        Assert.Equal(["step1", "step2"], order);
        Assert.Equal(WorkflowStatus.Completed, result.Status);
    }

    [Fact]
    public async Task Workflow_ConditionalBranch_SkipsWhenFalse()
    {
        var wf = new AgxiomeWorkflow("cond-wf");
        var executed = false;

        wf.AddStep("gate", ctx => Task.CompletedTask);
        wf.AddStep("skipped", ctx => { executed = true; return Task.CompletedTask; });
        wf.AddTransition("gate", "skipped", ctx => false); // condition false

        var result = await wf.ExecuteAsync(new WorkflowContext());

        Assert.False(executed);
        Assert.Equal(WorkflowStatus.Completed, result.Status);
    }

    [Fact]
    public async Task Workflow_Loop_MaxIterations()
    {
        var body = new FunctionWorkflowStep("loop-body", ctx => Task.CompletedTask);
        var loop = new LoopStep("loop", body, maxIterations: 5);

        var result = await loop.ExecuteAsync(new WorkflowContext());

        Assert.Equal(StepStatus.Completed, result.Status);
        Assert.Contains("Iteration 5", result.Output?.ToString() ?? "");
    }

    [Fact]
    public async Task Workflow_Parallel_AllComplete()
    {
        var step1 = new FunctionWorkflowStep("p1", ctx => Task.Delay(10));
        var step2 = new FunctionWorkflowStep("p2", ctx => Task.Delay(5));
        var parallel = new ParallelStep("parallel", step1, step2);

        var result = await parallel.ExecuteAsync(new WorkflowContext());

        Assert.Equal(StepStatus.Completed, result.Status);
    }

    // ── Context Engineering Tests ──

    [Fact]
    public void DynamicInstructionResolver_ResolvesPlaceholders()
    {
        var resolver = new DynamicInstructionResolver();
        var state = new SessionState();
        state.Set("user_name", "Alice");

        var result = resolver.Resolve("Hello {user_name}", state);

        Assert.Equal("Hello Alice", result);
    }

    [Fact]
    public void DefaultDependencyProvider_ResolvesRegisteredDependency()
    {
        var provider = new DefaultDependencyProvider();
        provider.RegisterDependency<string>("test-value");

        Assert.Equal("test-value", provider.GetDependency<string>());
    }

    [Fact]
    public void DefaultDependencyProvider_ThrowsOnMissing()
    {
        var provider = new DefaultDependencyProvider();
        Assert.Throws<InvalidOperationException>(() => provider.GetDependency<string>());
    }

    // ── Chunker Tests ──

    [Fact]
    public void FixedSizeChunker_SplitsText()
    {
        var chunker = new FixedSizeChunker();
        var text = "one two three four five six seven eight nine ten";

        var chunks = chunker.Chunk(text, 10, 2);

        Assert.True(chunks.Count >= 2);
        Assert.Contains("one", chunks[0].Content);
    }

    [Fact]
    public void HeadingChunker_SplitsAtHeadings()
    {
        var chunker = new HeadingChunker();
        var text = "# Title\ncontent1\n## Subtitle\ncontent2";

        var chunks = chunker.Chunk(text);

        Assert.True(chunks.Count >= 1);
    }

    // ── Cost Tracker Tests ──

    [Fact]
    public void ProviderCostTracker_EstimatesOpenAI()
    {
        var tracker = new ProviderCostTracker();
        var cost = tracker.Estimate("OpenAI", "gpt-4o", 1000, 500);
        Assert.True(cost > 0);
    }

    [Fact]
    public void ProviderCostTracker_LocalProviderIsFree()
    {
        var tracker = new ProviderCostTracker();
        var cost = tracker.Estimate("Ollama", "llama3", 10000, 5000);
        Assert.Equal(0, cost);
    }

    [Fact]
    public void ProviderCostTracker_UnknownProvider()
    {
        var tracker = new ProviderCostTracker();
        var cost = tracker.Estimate("Unknown", "model", 100, 100);
        Assert.Equal(0, cost);
    }

    // ── Skill Manager Tests ──

    [Fact]
    public void SkillManager_RegisterAndGet()
    {
        var mgr = new SkillManager();
        var skill = new TestSkill("test-skill", "Test", "A test skill");

        mgr.RegisterSkill(skill);

        Assert.Same(skill, mgr.GetSkill("test-skill"));
    }

    [Fact]
    public void SkillManager_ListByCategory()
    {
        var mgr = new SkillManager();
        mgr.RegisterSkill(new TestSkill("s1", "S1", "D1", "research"));
        mgr.RegisterSkill(new TestSkill("s2", "S2", "D2", "coding"));

        var research = mgr.ListSkills("research");

        Assert.Single(research);
    }

    // ── Schedule Manager Tests ──

    [Fact]
    public async Task ScheduleManager_CreateAndList()
    {
        var mgr = new ScheduleManager();
        await mgr.CreateScheduleAsync("test-schedule", "wf-1", "*/5 * * * *");

        var list = await mgr.ListSchedulesAsync();

        Assert.Single(list);
        Assert.Equal("test-schedule", list[0].Name);
    }

    // ── Agent Factory TestAgent Tests ──

    [Fact]
    public async Task TestAgent_WithHooks_ExecutesPreHook()
    {
        var agent = AgentFactory.CreateTestAgent("hook-test");
        bool hookRan = false;

        agent.AddHook(new DelegateHook("test-hook", HookPoint.PreRun,
            (ctx, ct) => { hookRan = true; return Task.CompletedTask; }));

        var result = await agent.RunAsync(new AgentRequest("test"));

        Assert.True(hookRan);
        Assert.Equal(AgentStatus.Completed, result.Status);
    }

    [Fact]
    public async Task TestAgent_WithGuardrail_BlocksPII()
    {
        var agent = AgentFactory.CreateTestAgent("guard-test");
        agent.AddGuardrail(new AGXIOME.Core.Guardrails.PIIDetectionGuardrail());

        var result = await agent.RunAsync(new AgentRequest("My SSN is 123-45-6789"));

        Assert.Equal(AgentStatus.Error, result.Status);
        Assert.Contains("ERR-0101", result.Content);
    }
}

/// <summary>Test skill implementation for unit tests.</summary>
public sealed class TestSkill : IAgxiomeSkill
{
    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public string? Version => "1.0.0";
    public string? Category { get; }
    public string? BaseSkillId => null;
    public IReadOnlyList<string> ToolIds => Array.Empty<string>();
    public IReadOnlyList<string> RequiredSkills => Array.Empty<string>();
    public string? SystemPromptExtension => null;
    public string? KnowledgeBaseId => null;
    public Dictionary<string, object> DefaultConfig => new();
    public IReadOnlyList<SkillScript> Scripts => Array.Empty<SkillScript>();
    public IReadOnlyList<SkillDocRef> References => Array.Empty<SkillDocRef>();

    public TestSkill(string id, string name, string description, string? category = null)
    {
        Id = id; Name = name; Description = description; Category = category;
    }
}

/// <summary>Test hook implementation.</summary>
public sealed class DelegateHook : IAgentHook
{
    public string Name { get; }
    public HookPoint Point { get; }
    public int Order { get; }
    public bool RunInBackground { get; }
    private readonly Func<HookContext, CancellationToken, Task> _handler;

    public DelegateHook(string name, HookPoint point, Func<HookContext, CancellationToken, Task> handler, int order = 0, bool background = false)
    {
        Name = name; Point = point; _handler = handler; Order = order; RunInBackground = background;
    }

    public Task ExecuteAsync(HookContext context, CancellationToken ct = default) => _handler(context, ct);
}
