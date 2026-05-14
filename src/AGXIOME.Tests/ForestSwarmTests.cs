using AGXIOME.Core.Patterns;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using NSubstitute;

namespace AGXIOME.Tests;

public class ForestSwarmTests
{
    // ── Config / records ──────────────────────────────────────────────────────

    [Fact]
    public void ForestSwarmConfig_DefaultMinMatchScore_IsZero()
    {
        var config = new ForestSwarmConfig();
        Assert.Equal(0, config.MinMatchScore);
    }

    [Fact]
    public void ForestSwarmResult_StoresAllFields()
    {
        var result = new ForestSwarmResult(
            "task", "tree1", "agent1", "output",
            new[] { "keyword" }, TimeSpan.FromMilliseconds(100));

        Assert.Equal("task", result.Task);
        Assert.Equal("tree1", result.SelectedTree);
        Assert.Equal("agent1", result.SelectedAgent);
        Assert.Equal("output", result.Output);
        Assert.Single(result.MatchedKeywords);
    }

    // ── ForestAgentEntry ──────────────────────────────────────────────────────

    [Fact]
    public void ForestAgentEntry_NullAgent_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ForestAgentEntry("id", "desc", Array.Empty<string>(), null!));
    }

    [Fact]
    public void ForestAgentEntry_EmptyAgentId_Throws()
    {
        var mock = Substitute.For<IAgxiomeAgent>();
        Assert.Throws<ArgumentException>(() =>
            new ForestAgentEntry("", "desc", Array.Empty<string>(), mock));
    }

    [Fact]
    public void ForestAgentEntry_ValidConstruction_Succeeds()
    {
        var mock  = Substitute.For<IAgxiomeAgent>();
        var entry = new ForestAgentEntry("agent1", "Finance expert", new[] { "budget", "revenue" }, mock);

        Assert.Equal("agent1", entry.AgentId);
        Assert.Equal("Finance expert", entry.Description);
        Assert.Equal(2, entry.Keywords.Count);
        Assert.Same(mock, entry.Agent);
    }

    // ── ForestTree ────────────────────────────────────────────────────────────

    [Fact]
    public void ForestTree_EmptyAgentList_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new ForestTree("tree1", Array.Empty<ForestAgentEntry>()));
    }

    [Fact]
    public void ForestTree_EmptyName_Throws()
    {
        var mock  = Substitute.For<IAgxiomeAgent>();
        var entry = new ForestAgentEntry("a1", "desc", new[] { "kw" }, mock);
        Assert.Throws<ArgumentException>(() =>
            new ForestTree("", new[] { entry }));
    }

    [Fact]
    public void ForestTree_ValidConstruction_Succeeds()
    {
        var mock  = Substitute.For<IAgxiomeAgent>();
        var entry = new ForestAgentEntry("a1", "desc", new[] { "kw" }, mock);
        var tree  = new ForestTree("Finance", new[] { entry });

        Assert.Equal("Finance", tree.TreeName);
        Assert.Single(tree.Agents);
    }

    // ── ForestSwarm routing ───────────────────────────────────────────────────

    [Fact]
    public void ForestSwarm_EmptyTrees_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            SwarmFactory.ForestSwarm(Array.Empty<ForestTree>()));
    }

    [Fact]
    public void FindBestMatch_KeywordOverlap_SelectsCorrectAgent()
    {
        var financeAgent  = Substitute.For<IAgxiomeAgent>();
        var codingAgent   = Substitute.For<IAgxiomeAgent>();

        var financeEntry = new ForestAgentEntry("finance", "Finance", new[] { "budget", "revenue", "investment", "finance" }, financeAgent);
        var codingEntry  = new ForestAgentEntry("coding",  "Coding",  new[] { "code", "programming", "algorithm", "software" },  codingAgent);

        var financeTree = new ForestTree("Finance", new[] { financeEntry });
        var codingTree  = new ForestTree("Coding",  new[] { codingEntry });

        var swarm = SwarmFactory.ForestSwarm(new[] { financeTree, codingTree });

        var (tree, agent, matched) = swarm.FindBestMatch("What is the best investment strategy for revenue growth?");

        Assert.Equal("Finance", tree.TreeName);
        Assert.Equal("finance", agent.AgentId);
        Assert.NotEmpty(matched);
    }

    [Fact]
    public void FindBestMatch_CodingTask_SelectsCodingAgent()
    {
        var financeAgent = Substitute.For<IAgxiomeAgent>();
        var codingAgent  = Substitute.For<IAgxiomeAgent>();

        var financeEntry = new ForestAgentEntry("finance", "Finance", new[] { "budget", "revenue", "investment" }, financeAgent);
        var codingEntry  = new ForestAgentEntry("coding",  "Coding",  new[] { "code", "programming", "software", "algorithm" }, codingAgent);

        var tree1 = new ForestTree("Finance", new[] { financeEntry });
        var tree2 = new ForestTree("Coding",  new[] { codingEntry });

        var swarm = SwarmFactory.ForestSwarm(new[] { tree1, tree2 });

        var (selectedTree, selectedAgent, _) = swarm.FindBestMatch("Write a sorting algorithm in Python programming language");

        Assert.Equal("Coding", selectedTree.TreeName);
        Assert.Equal("coding", selectedAgent.AgentId);
    }

    [Fact]
    public void FindBestMatch_NoKeywordOverlap_FallsBackToFirstAgent()
    {
        var agent1 = Substitute.For<IAgxiomeAgent>();
        var agent2 = Substitute.For<IAgxiomeAgent>();

        var entry1 = new ForestAgentEntry("a1", "Science", new[] { "quantum", "particle", "physics" }, agent1);
        var entry2 = new ForestAgentEntry("a2", "Art",     new[] { "painting", "sculpture", "color" },  agent2);

        var tree = new ForestTree("Domain", new[] { entry1, entry2 });
        var swarm = SwarmFactory.ForestSwarm(new[] { tree });

        var (selectedTree, selectedAgent, matched) = swarm.FindBestMatch("What is the best pizza recipe?");

        // No overlap → fallback to first tree, first agent
        Assert.Equal("Domain", selectedTree.TreeName);
        Assert.Equal("a1", selectedAgent.AgentId);
        Assert.Empty(matched);
    }

    [Fact]
    public void FindBestMatch_MultipleAgentsSameTree_SelectsHighestScore()
    {
        var lowAgent  = Substitute.For<IAgxiomeAgent>();
        var highAgent = Substitute.For<IAgxiomeAgent>();

        var lowEntry  = new ForestAgentEntry("low",  "Low",  new[] { "budget" },                              lowAgent);
        var highEntry = new ForestAgentEntry("high", "High", new[] { "budget", "revenue", "investment", "finance" }, highAgent);

        var tree  = new ForestTree("Finance", new[] { lowEntry, highEntry });
        var swarm = SwarmFactory.ForestSwarm(new[] { tree });

        var (_, selectedAgent, _) = swarm.FindBestMatch("Analyze the budget revenue and investment finance plan");

        Assert.Equal("high", selectedAgent.AgentId);
    }

    [Fact]
    public void SwarmFactory_ForestSwarm_Name_IsPreserved()
    {
        var mock  = Substitute.For<IAgxiomeAgent>();
        var entry = new ForestAgentEntry("a1", "desc", new[] { "kw" }, mock);
        var tree  = new ForestTree("T1", new[] { entry });

        var swarm = SwarmFactory.ForestSwarm(new[] { tree }, name: "my-forest", description: "test desc");

        Assert.Equal("my-forest", swarm.Name);
        Assert.Equal("test desc", swarm.Description);
    }
}
