using AGXIOME.Core.Agent;
using AGXIOME.Core.Skills;
using AGXIOME.Shared.Interfaces;
using Xunit;

namespace AGXIOME.Tests.F19;

//══════════════════════════════════════════════════════════════════════════════
// TS01  SkillManager — happy path
// ══════════════════════════════════════════════════════════════════════════════

[Trait("Phase", "F19")]
public class F19_TS01_SkillManager_HappyPath
{
    private static SkillManager Build() => new();

    private static IAgxiomeSkill MakeSkill(string id, string name, string category = "general")
        => new AgxiomeSkillBuilder(id).WithName(name).WithCategory(category).Build();

    [Fact]
    public void RegisterFromPackage_StoresPackageAndSkill()
    {
        var mgr = Build();
        var pkg = new SkillPackage(
            "pkg-1", "Package Skill", "desc", "1.0.0",
            "utils", null, "/skills/pkg-1/SKILL.md", null, null,
            new List<string>(), new List<string>());
        mgr.RegisterFromPackage(pkg);
        var skill = mgr.GetSkill("pkg-1");
        Assert.NotNull(skill);
        Assert.Equal("pkg-1", skill!.Id);
        Assert.Equal("Package Skill", skill.Name);
    }

    [Fact]
    public void RegisterFromPackage_ThenUnregister_RemovesBoth()
    {
        var mgr = Build();
        var pkg = new SkillPackage("pkg-x", "PX", "d", "1.0.0",
            null, null, "/skills/pkg-x/SKILL.md", null, null,
            new List<string>(), new List<string>());
        mgr.RegisterFromPackage(pkg);
        mgr.UnregisterSkill("pkg-x");
        Assert.Null(mgr.GetSkill("pkg-x"));
    }

    [Fact]
    public void ListSkills_NoFilter_ReturnsAll()
    {
        var mgr = Build();
        mgr.RegisterSkill(MakeSkill("s1", "S1", "research"));
        mgr.RegisterSkill(MakeSkill("s2", "S2", "research"));
        mgr.RegisterSkill(MakeSkill("s3", "S3", "coding"));
        Assert.Equal(3, mgr.ListSkills().Count);
    }

    [Fact]
    public void ListSkills_CategoryFilter_ReturnsSubset()
    {
        var mgr = Build();
        mgr.RegisterSkill(MakeSkill("r1", "R1", "research"));
        mgr.RegisterSkill(MakeSkill("r2", "R2", "research"));
        mgr.RegisterSkill(MakeSkill("c1", "C1", "coding"));
        Assert.Equal(2, mgr.ListSkills("research").Count);
        Assert.Single(mgr.ListSkills("coding"));
    }

    [Fact]
    public void RemoveSkillFromAgent_RemovesFromTracking()
    {
        var agent = AgentFactory.CreateTestAgent("agent-x");
        var mgr   = Build();
        var skill = new AgxiomeSkillBuilder("s1").WithName("S1").Build();
        mgr.RegisterSkill(skill);
        mgr.ApplySkillToAgent(agent, "s1");
        Assert.Contains("s1", mgr.GetAgentSkillIds(agent.Id));
        mgr.RemoveSkillFromAgent(agent, "s1");
        Assert.DoesNotContain("s1", mgr.GetAgentSkillIds(agent.Id));
    }

    [Fact]
    public void GetAgentSkillIds_UnknownAgent_ReturnsEmpty()
    {
        var mgr = Build();
        Assert.Empty(mgr.GetAgentSkillIds("ghost-agent"));
    }

    [Fact]
    public void ApplySkill_MultipleAgents_TrackedSeparately()
    {
        var a1  = AgentFactory.CreateTestAgent("a1");
        var a2  = AgentFactory.CreateTestAgent("a2");
        var mgr = Build();
        mgr.RegisterSkill(MakeSkill("shared-skill", "Shared"));
        mgr.ApplySkillToAgent(a1, "shared-skill");
        mgr.ApplySkillToAgent(a2, "shared-skill");
        Assert.Contains("shared-skill", mgr.GetAgentSkillIds("a1"));
        Assert.Contains("shared-skill", mgr.GetAgentSkillIds("a2"));
    }

    [Fact]
    public void ApplySkill_WithDependency_DependencyAutoApplied()
    {
        var agent = AgentFactory.CreateTestAgent("dep-agent");
        var mgr   = Build();
        mgr.RegisterSkill(MakeSkill("base", "Base"));
        var ext = new AgxiomeSkillBuilder("ext").WithName("Ext").Requires("base").Build();
        mgr.RegisterSkill(ext);
        mgr.ApplySkillToAgent(agent, "ext");
        var ids = mgr.GetAgentSkillIds(agent.Id);
        Assert.Contains("ext",  ids);
        Assert.Contains("base", ids);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// TS02  SkillBuilder — advanced
// ══════════════════════════════════════════════════════════════════════════════

[Trait("Phase", "F19")]
public class F19_TS02_SkillBuilder_Advanced
{
    [Fact]
    public void SkillBuilder_WithPrompt_StoredAsSystemPromptExtension()
    {
        var s = new AgxiomeSkillBuilder("kb")
            .WithName("KB")
            .WithPrompt("Search knowledge base.")
            .Build();
        Assert.Equal("Search knowledge base.", s.SystemPromptExtension);
    }

    [Fact]
    public void SkillBuilder_WithVersion_Stored()
    {
        var s = new AgxiomeSkillBuilder("v-skill")
            .WithName("Versioned")
            .WithVersion("2.3.1")
            .Build();
        Assert.Equal("2.3.1", s.Version);
    }

    [Fact]
    public void SkillBuilder_MultipleDependencies_AllRecorded()
    {
        var s = new AgxiomeSkillBuilder("composite")
            .WithName("Composite")
            .Requires("dep-a")
            .Requires("dep-b")
            .Requires("dep-c")
            .Build();
        Assert.Equal(3, s.RequiredSkills.Count);
        Assert.Contains("dep-a", s.RequiredSkills);
        Assert.Contains("dep-c", s.RequiredSkills);
    }

    [Fact]
    public void SkillBuilder_Extends_SetsBaseSkillId()
    {
        var s = new AgxiomeSkillBuilder("child")
            .WithName("Child")
            .Extends("parent-skill")
            .Build();
        Assert.Equal("parent-skill", s.BaseSkillId);
    }

    [Fact]
    public void SkillBuilder_WithKnowledgeBase_Stored()
    {
        var s = new AgxiomeSkillBuilder("kb-skill")
            .WithName("KB Skill")
            .WithKnowledgeBase("kb-42")
            .Build();
        Assert.Equal("kb-42", s.KnowledgeBaseId);
    }

    [Fact]
    public void SkillBuilder_AddScript_AppearsInScripts()
    {
        var s = new AgxiomeSkillBuilder("script-skill")
            .WithName("Scripted")
            .AddScript("run", "/scripts/run.py", "python")
            .Build();
        Assert.Single(s.Scripts);
        Assert.Equal("run",    s.Scripts[0].Name);
        Assert.Equal("python", s.Scripts[0].Language);
    }

    [Fact]
    public void SkillBuilder_AddReference_AppearsInReferences()
    {
        var s = new AgxiomeSkillBuilder("ref-skill")
            .WithName("Referenced")
            .AddReference("guide", "/references/guide.md")
            .Build();
        Assert.Single(s.References);
        Assert.Equal("guide", s.References[0].Name);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// TS03  SkillManager — offensive
// ══════════════════════════════════════════════════════════════════════════════

[Trait("Phase", "F19")]
public class F19_TS03_SkillManager_Offensive
{
    private static SkillManager Build() => new();

    [Fact]
    public void GetSkill_Unknown_ReturnsNull()
    {
        Assert.Null(Build().GetSkill("no-such-skill"));
    }

    [Fact]
    public void UnregisterSkill_Unknown_NoThrow()
    {
        Build().UnregisterSkill("ghost");
    }

    [Fact]
    public void ApplySkill_UnknownSkill_ThrowsKeyNotFound()
    {
        var agent = AgentFactory.CreateTestAgent("throw-agent");
        Assert.Throws<KeyNotFoundException>(() => Build().ApplySkillToAgent(agent, "missing-skill"));
    }

    [Fact]
    public void RegisterSkill_Overwrite_ReplacesExisting()
    {
        var mgr = Build();
        mgr.RegisterSkill(new AgxiomeSkillBuilder("dup").WithName("First").Build());
        mgr.RegisterSkill(new AgxiomeSkillBuilder("dup").WithName("Second").Build());
        Assert.Equal("Second", mgr.GetSkill("dup")!.Name);
        Assert.Single(mgr.ListSkills());
    }

    [Fact]
    public void ListSkills_UnknownCategory_ReturnsEmpty()
    {
        var mgr = Build();
        mgr.RegisterSkill(new AgxiomeSkillBuilder("s").WithName("S").WithCategory("coding").Build());
        Assert.Empty(mgr.ListSkills("nonexistent-category"));
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// TS04  SkillManager — aggressive (concurrency + many skills)
// ══════════════════════════════════════════════════════════════════════════════

[Trait("Phase", "F19")]
public class F19_TS04_SkillManager_Aggressive
{
    [Fact]
    public async Task Concurrent_RegisterAndRead_NoException()
    {
        var mgr   = new SkillManager();
        var tasks = Enumerable.Range(0, 200).Select(i => Task.Run(() =>
        {
            var s = new AgxiomeSkillBuilder($"skill-{i}").WithName($"S{i}").Build();
            mgr.RegisterSkill(s);
            _ = mgr.GetSkill($"skill-{i}");
        }));
        await Task.WhenAll(tasks);
    }

    [Fact]
    public void Many_Skills_CanBeFilteredByCategory()
    {
        var mgr = new SkillManager();
        for (int i = 0; i < 50; i++)
            mgr.RegisterSkill(new AgxiomeSkillBuilder($"r{i}").WithName($"R{i}").WithCategory("research").Build());
        for (int i = 0; i < 30; i++)
            mgr.RegisterSkill(new AgxiomeSkillBuilder($"c{i}").WithName($"C{i}").WithCategory("coding").Build());
        Assert.Equal(50, mgr.ListSkills("research").Count);
        Assert.Equal(30, mgr.ListSkills("coding").Count);
        Assert.Equal(80, mgr.ListSkills().Count);
    }
}
