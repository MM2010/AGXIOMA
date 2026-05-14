using System.Text.Json;
using AGXIOME.DynamicRuntime.Compilation;
using AGXIOME.DynamicRuntime.Skills;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Core.Tools;

/// <summary>
/// Tool orchestratore per importare una skill dal marketplace Skills.sh.
/// Richiede: userId, skillId.
/// </summary>
public sealed class ImportSkillTool(
    SkillsShClient skillsClient,
    SkillsShRegistry skillsRegistry,
    SkillAdapter skillAdapter) : AgxiomeFunction
{
    public override string Name => "import_skill";
    public override string Description => "Imports a skill from the Skills.sh marketplace for the specified user. The skill is saved as Pending until trusted.";
    public override IReadOnlyList<string> Categories => ["dynamic", "skills", "import"];

    public override async Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct)
    {
        var parameters = context.Parameters;
        if (parameters is null)
            return new ToolResult(false, null, "Parameters required: userId, skillId");

        var userId = GetParam(parameters, "userId");
        var skillId = GetParam(parameters, "skillId");

        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(skillId))
            return new ToolResult(false, null, "userId and skillId are required");

        var skill = await skillsClient.GetSkillAsync(skillId, ct);
        if (skill is null)
            return new ToolResult(false, null, $"Skill '{skillId}' not found in marketplace");

        // Validate + adapt (non bloccante: gli errori di compilazione sono non fatali)
        string? adaptError = null;
        try { await skillAdapter.AdaptAsync(skill, ct); }
        catch (Exception ex) { adaptError = ex.Message; }

        await skillsRegistry.ImportAsync(userId, skill, ct);

        var output = new
        {
            skill.Id,
            skill.Name,
            Type = skill.Type.ToString(),
            Status = "Pending",
            AdaptWarning = adaptError
        };
        return new ToolResult(true, JsonSerializer.Serialize(output));
    }

    private static string? GetParam(Dictionary<string, object> parameters, string key) =>
        parameters.TryGetValue(key, out var v) ? v?.ToString() : null;
}
