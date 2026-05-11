using AGXIOME.Core.Agent;
using AGXIOME.Core.Teams;
using AGXIOME.Shared.Config;
using AGXIOME.Shared.Models;
using Xunit;

namespace AGXIOME.Tests;

public class TeamTests
{
    private static AgxiomeTeam CreateTeam(string name, TeamMode mode = TeamMode.Coordinate)
    {
        var leader = AgentFactory.CreateTestAgent($"{name}-leader");
        var config = new TeamConfig
        {
            Name = name,
            Mode = mode
        };
        return new AgxiomeTeam(name, config, leader);
    }

    [Fact]
    public async Task Team_NoMembers_Executes()
    {
        var team = CreateTeam("empty");
        var result = await team.ExecuteAsync("test");
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Team_WithMember_Executes()
    {
        var team = CreateTeam("basic");
        team.AddAgent("m1", "worker");
        var result = await team.ExecuteAsync("hello");
        Assert.NotNull(result.Content);
    }

    [Fact]
    public async Task RouteMode_ReturnsSingleResponse()
    {
        var team = CreateTeam("route", TeamMode.Route);
        team.AddAgent("spec", "specialist");
        var result = await team.ExecuteAsync("q");
        Assert.Equal(TeamMode.Route, result.ModeUsed);
    }

    [Fact]
    public async Task BroadcastMode_AllMembersCalled()
    {
        var team = CreateTeam("broadcast", TeamMode.Broadcast);
        team.AddAgent("m1", "w"); team.AddAgent("m2", "w");
        var result = await team.ExecuteAsync("task");
        Assert.Equal(2, result.AgentResults.Count);
    }
}
