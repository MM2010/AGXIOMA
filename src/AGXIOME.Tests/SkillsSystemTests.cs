using AGXIOME.Core.Skills;
using AGXIOME.Core.Agent;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using Xunit;

namespace AGXIOME.Tests;

public class SkillsSystemTests
{
    // ── Programmatic skills via builder ──

    [Fact]
    public void SkillBuilder_CreatesValidSkill()
    {
        var skill = new AgxiomeSkillBuilder("research.web")
            .WithName("Web Research")
            .WithDescription("Search and scrape web content")
            .WithCategory("research")
            .WithPrompt("Use web_search to find information.")
            .Build();

        Assert.Equal("research.web", skill.Id);
        Assert.Equal("Web Research", skill.Name);
        Assert.Equal("research", skill.Category);
    }

    [Fact]
    public void SkillBuilder_WithTools_RegistersTypeNames()
    {
        var skill = new AgxiomeSkillBuilder("code-review")
            .WithName("Code Review")
            .AddTool<MockCodeReviewTool>()
            .Build();

        Assert.Contains(skill.ToolIds, t => t.Contains("MockCodeReviewTool"));
    }

    [Fact]
    public void SkillBuilder_WithDependencies()
    {
        var skill = new AgxiomeSkillBuilder("data.analyst")
            .Requires("research.base")
            .Requires("math.tools")
            .Build();

        Assert.Equal(2, skill.RequiredSkills.Count);
    }

    // ── Skill Manager ──

    [Fact]
    public void SkillManager_RegisterAndGet()
    {
        var mgr = new SkillManager();
        var skill = BuildTestSkill("test-1", "Test Skill");

        mgr.RegisterSkill(skill);

        Assert.Same(skill, mgr.GetSkill("test-1"));
    }

    [Fact]
    public void SkillManager_ListByCategory()
    {
        var mgr = new SkillManager();
        mgr.RegisterSkill(BuildTestSkill("s1", "S1", "research"));
        mgr.RegisterSkill(BuildTestSkill("s2", "S2", "coding"));

        Assert.Single(mgr.ListSkills("research"));
        Assert.Single(mgr.ListSkills("coding"));
        Assert.Equal(2, mgr.ListSkills().Count);
    }

    [Fact]
    public void SkillManager_Unregister()
    {
        var mgr = new SkillManager();
        mgr.RegisterSkill(BuildTestSkill("to-go", "Going"));
        mgr.UnregisterSkill("to-go");

        Assert.Null(mgr.GetSkill("to-go"));
    }

    // ── Agent binding ──

    [Fact]
    public void ApplySkillToAgent_RegistersTools()
    {
        var agent = AgentFactory.CreateTestAgent("skilled-agent");
        var mgr = new SkillManager();

        var skill = new AgxiomeSkillBuilder("demo-skill")
            .WithName("Demo")
            .AddTool<MockCodeReviewTool>()
            .Build();

        mgr.RegisterSkill(skill);
        mgr.ApplySkillToAgent(agent, "demo-skill");

        Assert.Contains("demo-skill", mgr.GetAgentSkillIds(agent.Id));
    }

    [Fact]
    public void ApplySkill_UnknownSkill_Throws()
    {
        var agent = AgentFactory.CreateTestAgent("no-skill");
        var mgr = new SkillManager();

        Assert.Throws<KeyNotFoundException>(() =>
            mgr.ApplySkillToAgent(agent, "nonexistent"));
    }

    // ── Skill composition ──

    [Fact]
    public void SkillComposition_AggregatesTools()
    {
        var baseSkill = new AgxiomeSkillBuilder("base")
            .WithName("Base").AddTool<MockCodeReviewTool>().Build();

        var extSkill = new AgxiomeSkillBuilder("ext")
            .WithName("Extended").Requires("base").Build();

        var mgr = new SkillManager();
        mgr.RegisterSkill(baseSkill);
        mgr.RegisterSkill(extSkill);

        Assert.Single(baseSkill.ToolIds);
    }

    // ── Frontmatter parsing ──

    [Fact]
    public void ParseFrontmatter_Valid_Success()
    {
        var content = @"---
id: code-review
name: Code Review
category: coding
version: 2.0.0
tags: code, review, quality
---

Instructions for code review skill.

Use the review_code tool to check for style issues.";

        var (fm, desc) = LocalSkillsLoader.ParseFrontmatter(content);

        Assert.Equal("code-review", fm["id"]);
        Assert.Equal("Code Review", fm["name"]);
        Assert.Equal("coding", fm["category"]);
        Assert.Contains("Instructions for code review", desc);
    }

    [Fact]
    public void ParseFrontmatter_NoFrontmatter_ReturnsFullContent()
    {
        var content = "Just plain text content without frontmatter.";
        var (fm, desc) = LocalSkillsLoader.ParseFrontmatter(content);

        Assert.Empty(fm);
        Assert.Equal(content, desc);
    }

    [Fact]
    public void ParseFrontmatter_EmptyContent_ReturnsEmpty()
    {
        var (fm, desc) = LocalSkillsLoader.ParseFrontmatter("");

        Assert.Empty(fm);
        Assert.Equal("", desc);
    }

    // ── Helpers ──

    private static IAgxiomeSkill BuildTestSkill(string id, string name, string? category = null)
        => new AgxiomeSkillBuilder(id).WithName(name).WithCategory(category ?? "general").Build();
}

/// <summary>A dummy tool for skills tests.</summary>
public sealed class MockCodeReviewTool : AgxiomeFunction
{
    public override string Name => "review_code";
    public override string Description => "Review code quality";
    [ToolParameter(Required = true)] public string Code { get; set; } = "";
    public override Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct)
        => Task.FromResult(new ToolResult(true, "Code looks good"));
}
