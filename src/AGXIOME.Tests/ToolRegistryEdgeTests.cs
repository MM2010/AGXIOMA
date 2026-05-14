using AGXIOME.Core.Tools;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using Xunit;

namespace AGXIOME.Tests;

public class ToolRegistryEdgeTests
{
    [Fact]
    public void RegisterDuplicateName_LastWins()
    {
        var reg = new ToolRegistry();
        reg.Register<TestToolA>();
        reg.Register<TestToolB>(); // same name "test_tool"

        var tool = reg.GetTool("test_tool");
        Assert.IsType<TestToolB>(tool);
    }

    [Fact]
    public void GetTool_UnknownName_ReturnsNull()
    {
        var reg = new ToolRegistry();
        Assert.Null(reg.GetTool("nonexistent"));
    }

    [Fact]
    public void FindByCategory_NoMatch_EmptyList()
    {
        var reg = new ToolRegistry();
        reg.Register<TestToolA>();

        var tools = reg.FindByCategory("unknown");
        Assert.Empty(tools);
    }

    [Fact]
    public void ToJsonSchema_NoParams_ValidSchema()
    {
        var schema = new NoParamTool().ToJsonSchema();
        Assert.NotNull(schema["name"]);
        Assert.Equal("no_param_tool", schema["name"]?.GetValue<string>());
    }

    [Fact]
    public void ToJsonSchema_WithParams_HasRequired()
    {
        var schema = new TestToolA().ToJsonSchema();
        var parameters = schema["parameters"]?.AsObject();
        Assert.NotNull(parameters);
        Assert.Contains("required", parameters);
    }

    [Fact]
    public void UnregisterAgentTool_DoesNotAffectOtherAgents()
    {
        var reg = new ToolRegistry();
        reg.Register<TestToolA>();
        reg.RegisterForAgent("agent-a", "test_tool");
        reg.RegisterForAgent("agent-b", "test_tool");

        reg.UnregisterFromAgent("agent-a", "test_tool");

        Assert.Empty(reg.GetAgentTools("agent-a"));
        Assert.Single(reg.GetAgentTools("agent-b"));
    }
}

public sealed class TestToolA : AgxiomeFunction
{
    public override string Name => "test_tool";
    public override string Description => "Tool A";
    [ToolParameter(Required = true, Description = "Input")]
    public string Input { get; set; } = "";
    public override Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct)
        => Task.FromResult(new ToolResult(true, "A"));
}

public sealed class TestToolB : AgxiomeFunction
{
    public override string Name => "test_tool";
    public override string Description => "Tool B";
    public override Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct)
        => Task.FromResult(new ToolResult(true, "B"));
}

public sealed class NoParamTool : AgxiomeFunction
{
    public override string Name => "no_param_tool";
    public override string Description => "No params";
    public override Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct)
        => Task.FromResult(new ToolResult(true, "done"));
}
