using AGXIOME.Core.Patterns;
using AGXIOME.Shared.Models;

namespace AGXIOME.Tests;

public class HeavySwarmTests
{
    [Fact]
    public void HeavySwarmConfig_Defaults_AreSet()
    {
        var config = new HeavySwarmConfig();
        Assert.Equal("gpt-4o", config.QuestionModel);
        Assert.Equal("gpt-4o-mini", config.WorkerModel);
        Assert.Equal("gpt-4o", config.SynthesisModel);
        Assert.True(config.EnableParallelResearch);
    }

    [Fact]
    public void HeavySwarmPhaseLog_CapturesPhaseData()
    {
        var log = new HeavySwarmPhaseLog("QuestionGen", "test output", TimeSpan.FromSeconds(1.5));
        Assert.Equal("QuestionGen", log.Phase);
        Assert.Equal("test output", log.Output);
        Assert.Equal(TimeSpan.FromSeconds(1.5), log.Duration);
    }

    [Fact]
    public void HeavySwarmResult_StoresAllPhases()
    {
        var result = new HeavySwarmResult(
            "What is AI?",
            new List<string> { "Finding 1", "Finding 2" },
            "Analysis text",
            new List<string> { "Alt 1", "Alt 2" },
            "Synthesis text",
            new Dictionary<string, TimeSpan> { ["Research"] = TimeSpan.FromSeconds(2) }
        );

        Assert.Equal("What is AI?", result.Question);
        Assert.Equal(2, result.ResearchFindings.Count);
        Assert.Equal("Analysis text", result.Analysis);
        Assert.Equal(2, result.Alternatives.Count);
        Assert.Equal("Synthesis text", result.Synthesis);
        Assert.Single(result.PhaseTiming);
    }
}

public class GrokSwarmTests
{
    [Fact]
    public void GrokSwarmConfig_DefaultMaxLoops_IsOne()
    {
        var config = new GrokSwarmConfig();
        Assert.Equal(1, config.MaxLoops);
    }

    [Fact]
    public void GrokSwarmResult_StoresAllFields()
    {
        var timing = new Dictionary<string, TimeSpan> { ["Decompose"] = TimeSpan.FromSeconds(1) };
        var result = new GrokSwarmResult("task", "harper-out", "benjamin-out", "lucas-out", "synthesis", timing);

        Assert.Equal("task", result.Task);
        Assert.Equal("harper-out", result.HarperOutput);
        Assert.Equal("benjamin-out", result.BenjaminOutput);
        Assert.Equal("lucas-out", result.LucasOutput);
        Assert.Equal("synthesis", result.CaptainSynthesis);
        Assert.Single(result.PhaseTiming);
    }

    [Fact]
    public void GrokSwarm_Constructor_NullCaptain_Throws()
    {
        var mock = NSubstitute.Substitute.For<AGXIOME.Shared.Interfaces.IAgxiomeAgent>();
        Assert.Throws<ArgumentNullException>(() =>
            new GrokSwarm(new GrokSwarmConfig(), null!, mock, mock, mock));
    }

    [Fact]
    public void GrokSwarm_Constructor_ValidAgents_Succeeds()
    {
        var mock = NSubstitute.Substitute.For<AGXIOME.Shared.Interfaces.IAgxiomeAgent>();
        var swarm = new GrokSwarm(new GrokSwarmConfig(), mock, mock, mock, mock);
        Assert.NotNull(swarm);
    }

    [Fact]
    public void SwarmFactory_GrokSwarm_ReturnsInstance()
    {
        var mock = NSubstitute.Substitute.For<AGXIOME.Shared.Interfaces.IAgxiomeAgent>();
        var swarm = SwarmFactory.GrokSwarm(mock, mock, mock, mock, maxLoops: 2);
        Assert.NotNull(swarm);
    }
}

public class GrokHeavySwarmTests
{
    [Fact]
    public void GrokHeavySwarmConfig_DefaultMaxLoops_IsOne()
    {
        var config = new GrokHeavySwarmConfig();
        Assert.Equal(1, config.MaxLoops);
    }

    [Fact]
    public void GrokHeavySwarm_SpecialistNames_Has15Entries()
    {
        Assert.Equal(15, GrokHeavySwarm.SpecialistNames.Count);
    }

    [Fact]
    public void GrokHeavySwarm_SpecialistDescriptions_CoverAllNames()
    {
        foreach (var name in GrokHeavySwarm.SpecialistNames)
            Assert.True(GrokHeavySwarm.SpecialistDescriptions.ContainsKey(name),
                $"Missing description for specialist: {name}");
    }

    [Fact]
    public void GrokHeavySwarm_Constructor_MissingSpecialists_Throws()
    {
        var mock = NSubstitute.Substitute.For<AGXIOME.Shared.Interfaces.IAgxiomeAgent>();
        // Provide only 2 of 15 specialists
        var partial = new Dictionary<string, AGXIOME.Shared.Interfaces.IAgxiomeAgent>
        {
            ["Harper"]   = mock,
            ["Benjamin"] = mock,
        };
        Assert.Throws<ArgumentException>(() =>
            new GrokHeavySwarm(new GrokHeavySwarmConfig(), mock, partial));
    }

    [Fact]
    public void GrokHeavySwarm_Constructor_AllSpecialists_Succeeds()
    {
        var mock = NSubstitute.Substitute.For<AGXIOME.Shared.Interfaces.IAgxiomeAgent>();
        var specialists = GrokHeavySwarm.SpecialistNames
            .ToDictionary(n => n, _ => mock);
        var swarm = new GrokHeavySwarm(new GrokHeavySwarmConfig(), mock, specialists);
        Assert.NotNull(swarm);
    }

    [Fact]
    public void GrokHeavySwarmResult_StoresFields()
    {
        var outputs = new Dictionary<string, string> { ["Harper"] = "h-out" };
        var timing  = new Dictionary<string, TimeSpan> { ["Specialists"] = TimeSpan.FromSeconds(5) };
        var result  = new GrokHeavySwarmResult("task", outputs, "synthesis", timing);

        Assert.Equal("task", result.Task);
        Assert.Equal("synthesis", result.CaptainSynthesis);
        Assert.Equal("h-out", result.SpecialistOutputs["Harper"]);
    }
}
