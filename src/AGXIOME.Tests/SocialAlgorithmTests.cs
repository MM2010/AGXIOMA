using AGXIOME.Core.Agent;
using AGXIOME.Core.Teams;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Tests;

public class SocialAlgorithmTests
{
    [Fact]
    public void SocialAlgorithmExecutor_ThrowsOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => new SocialAlgorithmExecutor(null!));
    }

    [Fact]
    public async Task SocialAlgorithm_RoundRobin_ExecutesInOrder()
    {
        var callable = SocialAlgorithms.RoundRobin;
        var agents = new List<IAgxiomeAgent>
        {
            AgentFactory.CreateTestAgent("agent1"),
            AgentFactory.CreateTestAgent("agent2")
        };

        var result = await callable("test objective", agents, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(TeamMode.SocialAlgorithm, result.ModeUsed);
        Assert.Equal(2, result.AgentResults.Count);
    }

    [Fact]
    public async Task SocialAlgorithm_Debate_ExecutesAllRounds()
    {
        var callable = SocialAlgorithms.Debate;
        var agents = new List<IAgxiomeAgent>
        {
            AgentFactory.CreateTestAgent("agent1"),
            AgentFactory.CreateTestAgent("agent2")
        };

        var result = await callable("test debate topic", agents, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.AgentResults.Count >= 2);
    }

    [Fact]
    public async Task SocialAlgorithm_CustomCallable_ReturnsResult()
    {
        var callable = new SocialAlgorithmExecutor(async (objective, agents, ct) =>
        {
            await Task.CompletedTask;
            return new TeamResponse(
                "custom result",
                TeamMode.SocialAlgorithm,
                new List<AgentResult>(),
                new TeamMetadata(0, TimeSpan.Zero, 0)
            );
        });

        var agents = new List<IAgxiomeAgent>
        {
            AgentFactory.CreateTestAgent("agent1")
        };

        var result = await callable.ExecuteAsync("objective", agents);

        Assert.Equal("custom result", result.Content);
        Assert.Equal(TeamMode.SocialAlgorithm, result.ModeUsed);
    }
}
