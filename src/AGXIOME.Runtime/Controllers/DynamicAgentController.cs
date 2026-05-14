using Microsoft.AspNetCore.Mvc;
using AGXIOME.DynamicRuntime.Generation;
using AGXIOME.DynamicRuntime.HotFolder;
using AGXIOME.DynamicRuntime.Registry;

namespace AGXIOME.Runtime.Controllers;

/// <summary>
/// REST controller per la gestione degli agenti dinamici utente.
/// Base path: /api/agents
/// </summary>
[ApiController]
[Route("api/agents")]
public sealed class DynamicAgentController(
    DynamicAgentRegistry registry,
    AgentCodeGenerator generator,
    AgentDropFolderService dropFolder) : ControllerBase
{
    private readonly AgentDropFolderService _dropFolder = dropFolder;  // injected for future use
    /// <summary>GET /api/agents/user/{userId} — lista agenti dell'utente.</summary>
    [HttpGet("user/{userId}")]
    public IActionResult GetUserAgents(string userId)
    {
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
            return BadRequest("UserId and Description are required");

        var request = new AgentGenerationRequest(req.UserId, req.Description, req.ExampleInput, req.ExampleOutput);
        var result = await generator.GenerateAsync(request, ct);

        if (!result.Success)
            return UnprocessableEntity(new { Errors = result.Errors });

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
            return BadRequest("userId query param is required");

        var entry = registry.Get(userId, agentId);
        if (entry is null)
            return NotFound($"Agent '{agentId}' not found for user '{userId}'");

        registry.UpdateStatus(userId, agentId, AgentEntryStatus.Discovered);
        return Accepted();
    }

    /// <summary>DELETE /api/agents/{agentId}/remove — rimuove agente dal registry.</summary>
    [HttpDelete("{agentId}/remove")]
    public IActionResult DeleteAgent(string agentId, [FromQuery] string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest("userId query param is required");

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
