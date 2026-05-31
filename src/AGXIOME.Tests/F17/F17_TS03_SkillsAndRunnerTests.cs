// F17-TS03 — OpenClaw.Skills Tests (18 tests)
// Verifies ClawSkill construction, tool registration, system-prompt content,
// and ClawRunner integration with a stub agent.
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using OpenClaw.Skills;
using OpenClaw.Tools;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using OpenClaw;

namespace AGXIOME.Tests.F17;

// ═══════════════════════════════════════════════════════════════════
// TS03-A  ClawSkill construction
// ═══════════════════════════════════════════════════════════════════
public sealed class F17_TS03_SkillConstruction
{
    private static readonly string TempRoot =
        Path.Combine(Path.GetTempPath(), $"claw-skill-{Guid.NewGuid():N}");

    public F17_TS03_SkillConstruction() => Directory.CreateDirectory(TempRoot);

    [Fact] public void CodeReviewSkill_Create_HasExpectedId()
    {
        var skill = CodeReviewSkill.Create(TempRoot);
        Assert.Equal("code-review", skill.Id);
    }

    [Fact] public void CodeReviewSkill_Create_HasTwoTools()
    {
        var skill = CodeReviewSkill.Create(TempRoot);
        Assert.Equal(2, skill.Tools.Count);
    }

    [Fact] public void CodeReviewSkill_Tools_ContainFileSystemTool()
    {
        var skill = CodeReviewSkill.Create(TempRoot);
        Assert.Contains(skill.Tools, t => t is FileSystemTool);
    }

    [Fact] public void CodeReviewSkill_Tools_ContainCodeSearchTool()
    {
        var skill = CodeReviewSkill.Create(TempRoot);
        Assert.Contains(skill.Tools, t => t is CodeSearchTool);
    }

    [Fact] public void CodeReviewSkill_SystemPromptExtension_ContainsSeverityGuidance()
    {
        var skill = CodeReviewSkill.Create(TempRoot);
        Assert.Contains("SEVERITY", skill.SystemPromptExtension, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public void ProjectAnalysisSkill_Create_HasExpectedId()
    {
        var skill = ProjectAnalysisSkill.Create(TempRoot);
        Assert.Equal("project-analysis", skill.Id);
    }

    [Fact] public void ProjectAnalysisSkill_Create_HasTwoTools()
    {
        var skill = ProjectAnalysisSkill.Create(TempRoot);
        Assert.Equal(2, skill.Tools.Count);
    }

    [Fact] public void ProjectAnalysisSkill_SystemPromptExtension_ContainsOverviewSection()
    {
        var skill = ProjectAnalysisSkill.Create(TempRoot);
        Assert.Contains("Overview", skill.SystemPromptExtension, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public void Skill_IsImmutableRecord()
    {
        var s1 = CodeReviewSkill.Create(TempRoot);
        var s2 = s1 with { Id = "modified" };
        Assert.Equal("code-review", s1.Id);  // original unchanged
        Assert.Equal("modified",    s2.Id);
    }
}

// ═══════════════════════════════════════════════════════════════════
// TS03-B  ClawRunner — unit tests with stub IAgxiomeAgent
// ═══════════════════════════════════════════════════════════════════
public sealed class F17_TS03_ClawRunnerUnit
{
    private static readonly string TempRoot =
        Path.Combine(Path.GetTempPath(), $"claw-runner-{Guid.NewGuid():N}");

    private readonly IAgxiomeAgent _agent;
    private readonly ClawRunner    _runner;

    public F17_TS03_ClawRunnerUnit()
    {
        Directory.CreateDirectory(TempRoot);
        _agent  = Substitute.For<IAgxiomeAgent>();
        _runner = new ClawRunner(_agent);
    }

    [Fact] public async Task RunAsync_RegistersSkillTools()
    {
        // Arrange
        _agent.RunAsync(Arg.Any<AgentRequest>(), Arg.Any<CancellationToken>())
              .Returns(new AgentResponse(Content: "done", Status: AgentStatus.Completed));

        var skill = CodeReviewSkill.Create(TempRoot);

        // Act
        await _runner.RunAsync(skill, "Review my code.", default);

        // Assert: RegisterTool called once per tool in skill.
        _agent.Received(skill.Tools.Count).RegisterTool(Arg.Any<IAgxiomeTool>());
    }

    [Fact] public async Task RunAsync_PassesSystemPromptInMessage()
    {
        AgentRequest? captured = null;
        _agent.RunAsync(Arg.Do<AgentRequest>(r => captured = r), Arg.Any<CancellationToken>())
              .Returns(new AgentResponse(Content: "ok", Status: AgentStatus.Completed));

        var skill = CodeReviewSkill.Create(TempRoot);
        await _runner.RunAsync(skill, "Prompt", default);

        Assert.NotNull(captured);
        Assert.Contains("SEVERITY", captured!.Prompt); // from skill system-prompt extension
        Assert.Contains("Prompt",   captured!.Prompt);
    }

    [Fact] public async Task RunAsync_ReturnsAgentResponseContent()
    {
        _agent.RunAsync(Arg.Any<AgentRequest>(), Arg.Any<CancellationToken>())
              .Returns(new AgentResponse(Content: "review result", Status: AgentStatus.Completed));

        var result = await _runner.RunAsync(CodeReviewSkill.Create(TempRoot), "Go", default);
        Assert.Equal("review result", result);
    }

    [Fact] public async Task ChatAsync_CallsRunAsync_WithPrompt()
    {
        AgentRequest? captured = null;
        _agent.RunAsync(Arg.Do<AgentRequest>(r => captured = r), Arg.Any<CancellationToken>())
              .Returns(new AgentResponse(Content: "chat reply", Status: AgentStatus.Completed));

        var result = await _runner.ChatAsync("Hello agent", default);

        Assert.Equal("chat reply", result);
        Assert.NotNull(captured);
        Assert.Equal("Hello agent", captured!.Prompt);
    }

    [Fact] public async Task RunAsync_NullSkill_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _runner.RunAsync(null!, "prompt", default));
    }

    [Fact] public async Task RunAsync_EmptyPrompt_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _runner.RunAsync(CodeReviewSkill.Create(TempRoot), "", default));
    }

    [Fact] public async Task ChatAsync_EmptyPrompt_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _runner.ChatAsync("   ", default));
    }

    [Fact] public async Task RunAsync_AgentReturnsNullContent_ReturnsEmpty()
    {
        _agent.RunAsync(Arg.Any<AgentRequest>(), Arg.Any<CancellationToken>())
              .Returns(new AgentResponse(Content: null!, Status: AgentStatus.Completed));

        var result = await _runner.RunAsync(CodeReviewSkill.Create(TempRoot), "Go", default);
        Assert.Equal(string.Empty, result);
    }
}
