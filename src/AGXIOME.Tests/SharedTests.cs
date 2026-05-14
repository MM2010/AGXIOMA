using AGXIOME.Shared.Models;
using AGXIOME.Shared.Config;
using Xunit;

namespace AGXIOME.Tests;

public class SharedRecordTests
{
    [Fact] public async Task Record_Immutability_With_ReturnsNewInstance()
    {
        var original = new AgentRequest("hello");
        var modified = original with { Prompt = "world" };
        Assert.Equal("hello", original.Prompt);
        Assert.Equal("world", modified.Prompt);
    }

    [Fact] public async Task Record_Equality_SameValuesAreEqual()
    {
        var a = new AgentRequest("test");
        var b = new AgentRequest("test");
        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact] public async Task Record_Equality_DifferentValuesNotEqual()
    {
        var a = new AgentRequest("hello");
        var b = new AgentRequest("world");
        Assert.NotEqual(a, b);
    }

    [Fact] public async Task Record_NullString_DoesNotThrow()
    {
        var msg = new AgentMessage("user", null!);
        Assert.Null(msg.Content);
    }

    [Fact] public async Task Enum_ParseRoundtrip()
    {
        var status = AgentStatus.Completed;
        var parsed = Enum.Parse<AgentStatus>(status.ToString());
        Assert.Equal(AgentStatus.Completed, parsed);
    }

    [Fact] public async Task Config_HasSensibleDefaults()
    {
        var cfg = new AgentConfig();
        Assert.Equal(20, cfg.MaxIterations);
        Assert.Equal(TimeSpan.FromSeconds(30), cfg.Timeout);
        Assert.Equal(LLMProviderType.OpenAI, cfg.ReasoningEngine.Provider);
    }

    [Fact] public async Task LLMWIKYConfig_HasDefaults()
    {
        var cfg = new LLMWIKYConfig();
        Assert.Equal("gpt-4o", cfg.Model);
        Assert.Equal(0.7, cfg.DefaultTemperature);
        Assert.True(cfg.EnableCostTracking);
    }
}
