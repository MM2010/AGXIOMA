using AGXIOME.Core.Workflows;
using AGXIOME.Shared.Interfaces;

namespace AGXIOME.Tests;

public class AgentRearrangeTests
{
    [Fact]
    public void FlowParser_SingleAgent_ReturnsOneStage()
    {
        var stages = FlowParser.Parse("A");
        Assert.Single(stages);
        Assert.Equal("A", stages[0][0]);
    }

    [Fact]
    public void FlowParser_Sequential_ReturnsOrderedStages()
    {
        var stages = FlowParser.Parse("A -> B -> C");
        Assert.Equal(3, stages.Count);
        Assert.Equal("A", stages[0][0]);
        Assert.Equal("B", stages[1][0]);
        Assert.Equal("C", stages[2][0]);
    }

    [Fact]
    public void FlowParser_Parallel_ReturnsParallelStage()
    {
        var stages = FlowParser.Parse("A -> B, C");
        Assert.Equal(2, stages.Count);
        Assert.Equal("A", stages[0][0]);
        Assert.Equal(2, stages[1].Count);
        Assert.Equal("B", stages[1][0]);
        Assert.Equal("C", stages[1][1]);
    }

    [Fact]
    public void FlowParser_MultipleParallel_ReturnsCorrectStages()
    {
        var stages = FlowParser.Parse("A, B -> C, D");
        Assert.Equal(2, stages.Count);
        Assert.Equal(2, stages[0].Count);
        Assert.Equal("A", stages[0][0]);
        Assert.Equal("B", stages[0][1]);
        Assert.Equal(2, stages[1].Count);
        Assert.Equal("C", stages[1][0]);
        Assert.Equal("D", stages[1][1]);
    }

    [Fact]
    public void FlowParser_Empty_Throws()
    {
        Assert.Throws<ArgumentException>(() => FlowParser.Parse(""));
        Assert.Throws<ArgumentException>(() => FlowParser.Parse("   "));
    }

    [Fact]
    public void FlowParser_ComplexFlow_ParsesCorrectly()
    {
        var stages = FlowParser.Parse("researcher -> writer, editor -> publisher");
        Assert.Equal(3, stages.Count);
        Assert.Equal("researcher", stages[0][0]);
        Assert.Equal(2, stages[1].Count);
        Assert.Equal("writer", stages[1][0]);
        Assert.Equal("editor", stages[1][1]);
        Assert.Equal("publisher", stages[2][0]);
    }
}
