using System.Text.Json;
using AGXIOME.DynamicRuntime.Registry;
using AGXIOME.DynamicRuntime.Skills;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Core.Tools;

/// <summary>
/// Tool orchestratore che restituisce tutti gli agenti dinamici e skills disponibili per un utente.
/// Richiede: userId.
/// </summary>
public sealed class ListAvailableAgentsTool(
    DynamicAgentRegistry agentRegistry,
    SkillsShRegistry skillsRegistry) : AgxiomeFunction
{
    public override string Name => "list_available_agents";
    public override string Description => "Lists all dynamic agents and imported skills available for a user, including their status and descriptions.";
    public override IReadOnlyList<string> Categories => ["dynamic", "registry", "discovery"];

    public override async Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct)
    {
        var parameters = context.Parameters;
        if (parameters is null || !parameters.TryGetValue("userId", out var uidObj))
            return new ToolResult(false, null, "userId parameter is required");

        var userId = uidObj?.ToString();
        if (string.IsNullOrWhiteSpace(userId))
            return new ToolResult(false, null, "userId must not be empty");

        var agents = agentRegistry.GetAll(userId)
            .Select(e => new
            {
                Kind = "agent",
                e.AgentId,
                e.Description,
                Status = e.Status.ToString(),
                e.DiscoveredAt
            })
            .ToList<object>();

        var skills = (await skillsRegistry.GetAllAsync(userId, ct))
            .Select(e => new
            {
                Kind = "skill",
                Id = e.SkillId,
                e.Definition.Name,
                e.Definition.Description,
                Type = e.Definition.Type.ToString(),
                Status = e.Status.ToString(),
                e.ImportedAt
            })
            .ToList<object>();

        var result = new
        {
            UserId = userId,
            Agents = agents,
            Skills = skills,
            TotalAgents = agents.Count,
            TotalSkills = skills.Count
        };

        return new ToolResult(true, JsonSerializer.Serialize(result));
    }
}
