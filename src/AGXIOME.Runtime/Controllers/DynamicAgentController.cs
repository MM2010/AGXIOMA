using Microsoft.AspNetCore.Mvc;
using AGXIOME.DynamicRuntime.Generation;
using AGXIOME.DynamicRuntime.HotFolder;
using AGXIOME.DynamicRuntime.Registry;
using Microsoft.Extensions.DependencyInjection;

namespace AGXIOME.Runtime.Controllers;

/// <summary>
/// REST controller per la gestione degli agenti dinamici utente.
/// Base path: /api/agents
/// </summary>
[ApiController]
[Route("api/agents")]
public sealed class DynamicAgentController : ControllerBase
{
    /// <summary>GET /api/agents/user/{userId} — lista agenti dell'utente.</summary>
    [HttpGet("user/{userId}")]
    public IActionResult GetUserAgents(string userId)
    {
        var registry = HttpContext.RequestServices.GetRequiredService<DynamicAgentRegistry>();
        var entries = registry.GetAll(userId);
        var result = entries.Select(e => new
        {
            e.AgentId,
            e.Description,
            Status = e.Status.ToString(),
            e.DiscoveredAt,
            Error = e.LastError
        });
        return Ok(result);
    }

    /// <summary>POST /api/agents/generate — genera un agente tramite LLM.</summary>
    [HttpPost("generate")]
    public async Task<IActionResult> GenerateAgent([FromBody] GenerateAgentRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.UserId) || string.IsNullOrWhiteSpace(req.Description))
            return ApiErrors.InvalidArguments(
                "Dynamic agent generation requires both UserId and Description.",
                "UserId e Description sono obbligatori.");

        var generator = HttpContext.RequestServices.GetRequiredService<AgentCodeGenerator>();
        var request = new AgentGenerationRequest(req.UserId, req.Description, req.ExampleInput, req.ExampleOutput);
        var result = await generator.GenerateAsync(request, ct);

        if (!result.Success)
            return ApiErrors.Unprocessable(
                "Dynamic agent generation failed.",
                "Generazione agente fallita.");

        return Ok(new
        {
            result.AgentId,
            result.TotalTime,
            result.SavedFilePath
        });
    }

    /// <summary>POST /api/agents/{agentId}/compile — forza ricompilazione.</summary>
    [HttpPost("{agentId}/compile")]
    public IActionResult TriggerCompile(string agentId, [FromQuery] string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return ApiErrors.InvalidArguments("Agent compile trigger requires 'userId' query parameter.", "Parametro 'userId' obbligatorio.");

        var registry = HttpContext.RequestServices.GetRequiredService<DynamicAgentRegistry>();
        var entry = registry.Get(userId, agentId);
        if (entry is null)
            return ApiErrors.NotFound($"Agent '{agentId}' was not found for user '{userId}'.", "Agente non trovato.");

        registry.UpdateStatus(userId, agentId, AgentEntryStatus.Discovered);
        return Accepted();
    }

    /// <summary>DELETE /api/agents/{agentId}/remove — rimuove agente dal registry.</summary>
    [HttpDelete("{agentId}/remove")]
    public IActionResult DeleteAgent(string agentId, [FromQuery] string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return ApiErrors.InvalidArguments("Agent deletion requires 'userId' query parameter.", "Parametro 'userId' obbligatorio.");

        var registry = HttpContext.RequestServices.GetRequiredService<DynamicAgentRegistry>();
        var removed = registry.Remove(userId, agentId);
        return removed ? NoContent() : NotFound();
    }
}

/// <summary>DTO per la richiesta di generazione agente.</summary>
public sealed record GenerateAgentRequest(
    string UserId,
    string Description,
    string? ExampleInput = null,
    string? ExampleOutput = null
);
