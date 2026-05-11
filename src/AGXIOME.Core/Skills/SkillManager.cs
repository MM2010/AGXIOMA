using System.Collections.Concurrent;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Core.Skills;

public sealed class SkillManager : ISkillManager
{
    private readonly ConcurrentDictionary<string, IAgxiomeSkill> _skills = new();

    public void RegisterSkill(IAgxiomeSkill skill) => _skills[skill.Id] = skill;
    public void UnregisterSkill(string skillId) => _skills.TryRemove(skillId, out _);
    public IAgxiomeSkill? GetSkill(string skillId) => _skills.GetValueOrDefault(skillId);

    public IReadOnlyList<IAgxiomeSkill> ListSkills(string? category = null)
        => _skills.Values.Where(s => category == null || s.Category == category).ToList();

    public async Task<IAgxiomeSkill> DownloadSkillAsync(string skillId, string source, CancellationToken ct = default)
    {
        // HTTP GET from remote registry → load assembly
        await Task.CompletedTask;
        throw new NotImplementedException("Remote skill download requires HTTP client");
    }

    public void ApplySkillToAgent(IAgxiomeAgent agent, string skillId)
    {
        var skill = GetSkill(skillId) ?? throw new KeyNotFoundException($"Skill '{skillId}' not found");
        // Register tools, extend prompt
    }

    public void RemoveSkillFromAgent(IAgxiomeAgent agent, string skillId) { /* unregister tools */ }
}
