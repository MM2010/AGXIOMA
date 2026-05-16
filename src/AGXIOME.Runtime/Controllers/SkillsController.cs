using Microsoft.AspNetCore.Mvc;
using AGXIOME.DynamicRuntime.Skills;
using Microsoft.Extensions.DependencyInjection;

namespace AGXIOME.Runtime.Controllers;

/// <summary>
/// REST controller per la gestione delle skills importate da Skills.sh.
/// Base path: /api/skills
/// </summary>
[ApiController]
[Route("api/skills")]
public sealed class SkillsController : ControllerBase
{
    /// <summary>GET /api/skills/search?q= — cerca nel marketplace.</summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int limit = 10, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q))
            return ApiErrors.InvalidArguments("Skills search requires query parameter 'q'.", "Parametro 'q' obbligatorio.");

        var skillsClient = HttpContext.RequestServices.GetRequiredService<SkillsShClient>();
        var results = await skillsClient.SearchAsync(q, limit, ct);
        return Ok(results);
    }

    /// <summary>POST /api/skills/{skillId}/import/{userId} — importa una skill per un utente.</summary>
    [HttpPost("{skillId}/import/{userId}")]
    public async Task<IActionResult> Import(string skillId, string userId, CancellationToken ct)
    {
        var skillsClient = HttpContext.RequestServices.GetRequiredService<SkillsShClient>();
        var skillsRegistry = HttpContext.RequestServices.GetRequiredService<SkillsShRegistry>();
        var skill = await skillsClient.GetSkillAsync(skillId, ct);
        if (skill is null)
            return ApiErrors.NotFound($"Skill '{skillId}' was not found in marketplace.", "Skill non trovata.");

        await skillsRegistry.ImportAsync(userId, skill, ct);
        return Ok(new { Message = $"Skill '{skillId}' imported (Pending trust)", skill.Id, skill.Name });
    }

    /// <summary>GET /api/skills/user/{userId} — lista skills dell'utente.</summary>
    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetUserSkills(string userId, CancellationToken ct)
    {
        var skillsRegistry = HttpContext.RequestServices.GetRequiredService<SkillsShRegistry>();
        var entries = await skillsRegistry.GetAllAsync(userId, ct);
        return Ok(entries.Select(e => new
        {
            e.SkillId,
            e.Definition.Name,
            e.Definition.Description,
            Type = e.Definition.Type.ToString(),
            Status = e.Status.ToString(),
            e.ImportedAt
        }));
    }

    /// <summary>PATCH /api/skills/{skillId}/trust — aggiorna status trust.</summary>
    [HttpPatch("{skillId}/trust")]
    public async Task<IActionResult> SetTrust(string skillId, [FromQuery] string userId, [FromBody] SetSkillTrustRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return ApiErrors.InvalidArguments("Skill trust update requires 'userId' query parameter.", "Parametro 'userId' obbligatorio.");

        if (!Enum.TryParse<SkillStatus>(req.Status, ignoreCase: true, out var status))
            return ApiErrors.InvalidArguments(
                $"Skill trust status '{req.Status}' is invalid. Allowed: Trusted, Pending, Blocked.",
                "Stato trust della skill non valido.");

        var skillsRegistry = HttpContext.RequestServices.GetRequiredService<SkillsShRegistry>();
        await skillsRegistry.SetStatusAsync(userId, skillId, status, ct);
        return NoContent();
    }
}

/// <summary>DTO per aggiornare il trust status di una skill.</summary>
public sealed record SetSkillTrustRequest(string Status);
