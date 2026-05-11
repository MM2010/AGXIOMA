using AGXIOME.Core.Agent;
using AGXIOME.Core.Tools;
using AGXIOME.Core.Guardrails;
using AGXIOME.Core.HITL;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using Xunit;

namespace AGXIOME.Tests;

public class AgentCoreTests
{
    [Fact]
    public void AgentFactory_Create_ReturnsAgentWithCorrectId()
    {
        var agent = AgentFactory.CreateTestAgent("test-agent");
        Assert.Equal("test-agent", agent.Id);
        Assert.Equal(AgentStatus.Idle, agent.Status);
    }

    [Fact]
    public void ToolRegistry_RegisterAndGet_ReturnsTool()
    {
        var registry = new ToolRegistry();
        registry.Register<TestTool>();
        var tool = registry.GetTool("test_tool");
        Assert.NotNull(tool);
        Assert.Equal("test_tool", tool!.Name);
    }

    [Fact]
    public void ToolRegistry_PerAgentTools_Isolated()
    {
        var registry = new ToolRegistry();
        registry.Register<TestTool>();
        registry.RegisterForAgent("agent-a", "test_tool");

        var agentATools = registry.GetAgentTools("agent-a");
        var agentBTools = registry.GetAgentTools("agent-b");

        Assert.Single(agentATools);
        Assert.Empty(agentBTools);
    }

    [Fact]
    public async Task PIIDetectionGuardrail_BlocksSSN()
    {
        var guardrail = new PIIDetectionGuardrail();
        var ctx = new GuardrailContext("agent", null, null);

        var result = await guardrail.ValidateAsync("My SSN is 123-45-6789", ctx);

        Assert.False(result.IsValid);
        Assert.Contains("PII", result.Reason);
    }

    [Fact]
    public async Task PIIDetectionGuardrail_AllowsCleanInput()
    {
        var guardrail = new PIIDetectionGuardrail();
        var ctx = new GuardrailContext("agent", null, null);

        var result = await guardrail.ValidateAsync("Hello, how are you?", ctx);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void HITLManager_CreateRequirement_Pending()
    {
        var hitl = new HITLManager();
        var req = hitl.CreateRequirement("run-1", "agent-1", "send_email", new { to = "test@test.com" }, HITLRequirementType.Confirmation);

        Assert.Equal(HITLStatus.Pending, req.Status);

        // NeedsConfirmation is derived from HITLRequirementType comparison
        Assert.Equal(HITLRequirementType.Confirmation, req.Type);
    }

    [Fact]
    public void HITLManager_Resolve_UpdatesStatus()
    {
        var hitl = new HITLManager();
        var req = hitl.CreateRequirement("run-1", "agent-1", "send_email", new { }, HITLRequirementType.Confirmation);

        hitl.Resolve(req.Id, true);

        Assert.Equal(HITLStatus.Approved, hitl.GetRequirement(req.Id)?.Status);
    }

    [Fact]
    public void SessionState_SetGet_ReturnsValue()
    {
        var state = new SessionState();
        state.Set("key1", "value1");

        Assert.Equal("value1", state.Get<string>("key1"));
    }

    [Fact]
    public void SessionState_ToDictionary_ReturnsCopy()
    {
        var state = new SessionState();
        state.Set("count", 42);

        var dict = state.ToDictionary();
        Assert.Equal(42, dict["count"]);
    }

    [Fact]
    public void ToolSchema_GeneratesValidJson()
    {
        var schema = new TestTool().ToJsonSchema();
        Assert.NotNull(schema["name"]);
        Assert.Equal("test_tool", schema["name"]?.GetValue<string>());
        Assert.NotNull(schema["parameters"]);
    }
}

public sealed class TestTool : AgxiomeFunction
{
    public override string Name => "test_tool";
    public override string Description => "A test tool";

    [ToolParameter(Required = true, Description = "Test parameter")]
    public string Input { get; set; } = "";

    public override Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct = default)
        => Task.FromResult(new ToolResult(true, $"Processed: {Input}"));
}
