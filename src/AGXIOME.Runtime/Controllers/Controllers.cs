// RT-06..RT-14 — REST API Controllers
// Each controller maps to a domain area. All responses are JSON.
// Auth + RBAC is enforced upstream in middleware.

using Microsoft.AspNetCore.Mvc;
using AGXIOME.Core.Execution;
using AGXIOME.Core.Tools;
using AGXIOME.Runtime.Repositories;
using AGXIOME.Security.Audit;
using AGXIOME.Security.Sanitization;
using AGXIOME.Security.ApiKey;
using AGXIOME.Security.Rbac;
using AGXIOME.Shared.Errors;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Runtime.Controllers;

// ═══════════════════════════════════════════════════════════════
// Shared DTOs
// ═══════════════════════════════════════════════════════════════

public sealed record AgentDto(string Id, string Name, string? Description, string? Model, DateTimeOffset CreatedAt);
public sealed record CreateAgentRequest(string Name, string? Description, string? Model);
public sealed record RunDto(string RunId, string AgentId, string Status, DateTimeOffset StartedAt, DateTimeOffset? EndedAt, int? Score);
public sealed record StartRunRequest(string AgentId, string Input, bool Stream = false);
public sealed record SessionDto(string SessionId, string AgentId, DateTimeOffset CreatedAt, bool IsActive);
public sealed record TeamDto(string TeamId, string Name, IReadOnlyList<string> AgentIds);
public sealed record CreateTeamRequest(string Name, IReadOnlyList<string> AgentIds);
public sealed record WorkflowDto(string WorkflowId, string Name, IReadOnlyList<string> StepIds, string Status);
public sealed record CreateWorkflowRequest(string Name, IReadOnlyList<string> StepIds);
public sealed record KnowledgeIngestRequest(string CollectionId, string Content, string? Source);
public sealed record KnowledgeSearchRequest(string CollectionId, string Query, int TopK = 5);
public sealed record ToolDto(string ToolId, string Name, string Category, string? Schema);
public sealed record ApiKeyCreateRequest(IReadOnlyList<string> Roles, IReadOnlyList<string> Scopes, int? ExpiresInDays);
public sealed record ErrorResponse(string Code, int NumericCode, string Message, string Category);

public static class ApiErrors
{
    public static ErrorResponse From(AgxiomeException ex) =>
        new(
            ErrorCatalog.GetCode(ex.ErrorCode),
            ex.NumericCode,
            ex.UserMessage,
            ex.Category);

    public static ObjectResult InvalidArguments(string technicalMessage, string? userMessage = null)
        => ToActionResult(
            new AgxiomeException(
                AgxiomeErrorCode.TL_InvalidArguments,
                technicalMessage,
                userMessage ?? ErrorCatalog.GetUserMessage(AgxiomeErrorCode.TL_InvalidArguments)),
            StatusCodes.Status400BadRequest);

    public static ObjectResult NotFound(string technicalMessage, string? userMessage = null)
        => ToActionResult(
            new AgxiomeException(
                AgxiomeErrorCode.TL_NotFound,
                technicalMessage,
                userMessage ?? "Risorsa richiesta non trovata."),
            StatusCodes.Status404NotFound);

    public static ObjectResult Conflict(string technicalMessage, string? userMessage = null)
        => ToActionResult(
            new AgxiomeException(
                AgxiomeErrorCode.TL_InvalidArguments,
                technicalMessage,
                userMessage ?? "Stato della richiesta non valido."),
            StatusCodes.Status409Conflict);

    public static ObjectResult Unprocessable(string technicalMessage, string? userMessage = null)
        => ToActionResult(
            new AgxiomeException(
                AgxiomeErrorCode.TL_ExecutionFailed,
                technicalMessage,
                userMessage ?? ErrorCatalog.GetUserMessage(AgxiomeErrorCode.TL_ExecutionFailed)),
            StatusCodes.Status422UnprocessableEntity);

    public static ObjectResult InvalidModelState(ActionContext _)
        => InvalidArguments("HTTP request model validation failed.", "Richiesta non valida.");

    public static IResult ToResult(AgxiomeException ex, int? statusCode = null)
        => Results.Json(From(ex), statusCode: statusCode ?? MapStatusCode(ex.ErrorCode));

    public static IResult FromStatusCode(HttpContext context)
    {
        var ex = context.Response.StatusCode switch
        {
            StatusCodes.Status400BadRequest => new AgxiomeException(AgxiomeErrorCode.TL_InvalidArguments, userMessage: "Richiesta non valida."),
            StatusCodes.Status401Unauthorized => new AgxiomeException(AgxiomeErrorCode.SE_AuthenticationFailed),
            StatusCodes.Status403Forbidden => new AgxiomeException(AgxiomeErrorCode.SE_AuthorizationDenied),
            StatusCodes.Status404NotFound => new AgxiomeException(AgxiomeErrorCode.TL_NotFound, userMessage: "Risorsa richiesta non trovata."),
            StatusCodes.Status405MethodNotAllowed => new AgxiomeException(AgxiomeErrorCode.TL_InvalidArguments, userMessage: "Metodo HTTP non consentito."),
            _ => new AgxiomeException(AgxiomeErrorCode.InternalError),
        };

        return ToResult(ex, context.Response.StatusCode);
    }

    private static ObjectResult ToActionResult(AgxiomeException ex, int statusCode)
        => new(From(ex)) { StatusCode = statusCode };

    private static int MapStatusCode(AgxiomeErrorCode code) => code switch
    {
        AgxiomeErrorCode.TL_InvalidArguments => StatusCodes.Status400BadRequest,
        AgxiomeErrorCode.TL_NotFound => StatusCodes.Status404NotFound,
        AgxiomeErrorCode.SE_AuthenticationFailed => StatusCodes.Status401Unauthorized,
        AgxiomeErrorCode.SE_AuthorizationDenied => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status500InternalServerError,
    };
}

// ═══════════════════════════════════════════════════════════════
// RT-06 — Agent Controller
// ═══════════════════════════════════════════════════════════════

[ApiController, Route("api/agents")]
public sealed class AgentController(AuditLogger audit, IInputSanitizer sanitizer, IAgentRepository agents) : ControllerBase
{
    private readonly AuditLogger _audit = audit;
    private readonly IInputSanitizer _sanitizer = sanitizer;
    private readonly IAgentRepository _agents = agents;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await _agents.ListAsync(ct));

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
    {
        var a = await _agents.GetByIdAsync(id, ct);
        return a is not null ? Ok(a) : ApiErrors.NotFound($"Agent '{id}' was not found.");
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAgentRequest req, CancellationToken ct)
    {
        var check = _sanitizer.Sanitize(req.Name, SanitizationRules.AgentNameR);
        if (!check.IsValid) return ApiErrors.InvalidArguments(
            $"Agent name rejected by sanitizer: {check.ViolationDetail ?? "Invalid name"}.",
            check.ViolationDetail ?? "Nome agente non valido.");

        var id    = Guid.NewGuid().ToString("N")[..12];
        var agent = new AgentDto(id, check.SanitizedValue, req.Description, req.Model, DateTimeOffset.UtcNow);
        await _agents.SaveAsync(agent, ct);
        return Created($"/api/agents/{id}", agent);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] CreateAgentRequest req, CancellationToken ct)
    {
        var existing = await _agents.GetByIdAsync(id, ct);
        if (existing is null) return NotFound();
        var updated = existing with { Name = req.Name, Description = req.Description, Model = req.Model };
        await _agents.UpdateAsync(id, updated, ct);
        return Ok(updated);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        if (!await _agents.DeleteAsync(id, ct)) return NotFound();
        return NoContent();
    }

    [HttpPost("{id}/clone")]
    public async Task<IActionResult> Clone(string id, CancellationToken ct)
    {
        var src = await _agents.GetByIdAsync(id, ct);
        if (src is null) return NotFound();
        var newId  = Guid.NewGuid().ToString("N")[..12];
        var cloned = src with { Id = newId, Name = $"{src.Name}-copy", CreatedAt = DateTimeOffset.UtcNow };
        await _agents.SaveAsync(cloned, ct);
        return Created($"/api/agents/{newId}", cloned);
    }
}

// ═══════════════════════════════════════════════════════════════
// RT-07 — Run Controller
// ═══════════════════════════════════════════════════════════════

[ApiController, Route("api/runs")]
public sealed class RunController(IRunRepository runs, IAgentRunPipeline? pipeline = null) : ControllerBase
{
    private readonly IRunRepository _runs = runs;
    private readonly IAgentRunPipeline? _pipeline = pipeline;

    [HttpPost]
    public async Task<IActionResult> Start([FromBody] StartRunRequest req, CancellationToken ct)
    {
        var runId = Guid.NewGuid().ToString("N")[..12];
        var run   = new RunDto(runId, req.AgentId, "running", DateTimeOffset.UtcNow, null, null);
        await _runs.SaveAsync(run, ct);

        if (_pipeline is not null)
        {
            var response = await _pipeline.RunAsync(new AgentRequest(req.Input), runId: runId, ct: ct);
            var status   = response.Status == AgentStatus.Completed ? "completed" : "error";
            var completed = run with { Status = status, EndedAt = DateTimeOffset.UtcNow };
            await _runs.UpdateAsync(runId, completed, ct);
            return Ok(new { runId, status, content = response.Content });
        }

        return Accepted(run);
    }

    [HttpPost("async")]
    public async Task<IActionResult> StartAsync([FromBody] StartRunRequest req, CancellationToken ct)
    {
        if (_pipeline is null)
            return Accepted(new { status = "queued", message = "No pipeline registered — stub mode" });

        var jobId = _pipeline.RunBackground(new AgentRequest(req.Input));
        await _runs.SaveAsync(new RunDto(jobId, req.AgentId, "queued", DateTimeOffset.UtcNow, null, null), ct);
        return Accepted(new { runId = jobId, status = "queued" });
    }

    [HttpGet("{runId}")]
    public async Task<IActionResult> Status(string runId, CancellationToken ct)
    {
        var r = await _runs.GetByIdAsync(runId, ct);
        return r is not null ? Ok(r) : NotFound();
    }

    [HttpGet("{runId}/result")]
    public async Task<IActionResult> Result(string runId, CancellationToken ct)
    {
        var r = await _runs.GetByIdAsync(runId, ct);
        return r is not null ? Ok(r) : NotFound();
    }

    [HttpGet("{runId}/stream")]
    public async Task Stream(string runId, [FromQuery] string input = "hello", CancellationToken ct = default)
    {
        Response.ContentType  = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        if (_pipeline is null)
        {
            await Response.WriteAsync($"data: {{\"chunk\":\"Pipeline not configured\"}}\n\n", ct);
            return;
        }

        await foreach (var chunk in _pipeline.RunStreamAsync(new AgentRequest(input), runId: runId, ct: ct))
        {
            if (ct.IsCancellationRequested) break;
            var payload = System.Text.Json.JsonSerializer.Serialize(new { chunk = chunk.Content, isFinal = chunk.IsFinal });
            await Response.WriteAsync($"data: {payload}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }
    }

    [HttpDelete("{runId}")]
    public async Task<IActionResult> Cancel(string runId, CancellationToken ct)
    {
        var r = await _runs.GetByIdAsync(runId, ct);
        if (r is null) return NotFound();
        _pipeline?.CancelRun(runId);
        var cancelled = r with { Status = "cancelled", EndedAt = DateTimeOffset.UtcNow };
        await _runs.UpdateAsync(runId, cancelled, ct);
        return Ok(cancelled);
    }

    [HttpPost("{runId}/resume")]
    public async Task<IActionResult> Resume(string runId, CancellationToken ct)
    {
        var r = await _runs.GetByIdAsync(runId, ct);
        if (r is null) return NotFound();
        if (r.Status != "paused") return ApiErrors.Conflict($"Run '{runId}' cannot resume because status is '{r.Status}'.");
        var resumed = r with { Status = "running" };
        await _runs.UpdateAsync(runId, resumed, ct);
        return Ok(resumed);
    }

    [HttpPost("{runId}/pause")]
    public async Task<IActionResult> Pause(string runId, CancellationToken ct)
    {
        var r = await _runs.GetByIdAsync(runId, ct);
        if (r is null) return NotFound();
        var paused = r with { Status = "paused" };
        await _runs.UpdateAsync(runId, paused, ct);
        return Ok(paused);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? agentId, CancellationToken ct) =>
        Ok(await _runs.ListAsync(agentId, ct));
}

// ═══════════════════════════════════════════════════════════════
// RT-08 — Session Controller
// ═══════════════════════════════════════════════════════════════

[ApiController, Route("api/sessions")]
public sealed class SessionController(ISessionRepository sessions) : ControllerBase
{
    private readonly ISessionRepository _sessions = sessions;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? agentId, CancellationToken ct) =>
        Ok(await _sessions.ListAsync(agentId, ct));

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
    {
        var s = await _sessions.GetByIdAsync(id, ct);
        return s is not null ? Ok(s) : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SessionDto req, CancellationToken ct)
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        var s  = req with { SessionId = id, CreatedAt = DateTimeOffset.UtcNow };
        await _sessions.SaveAsync(s, ct);
        return Created($"/api/sessions/{id}", s);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        if (!await _sessions.DeleteAsync(id, ct)) return NotFound();
        return NoContent();
    }

    [HttpPatch("{id}/close")]
    public async Task<IActionResult> Close(string id, CancellationToken ct)
    {
        var s = await _sessions.CloseAsync(id, ct);
        return s is not null ? Ok(s) : NotFound();
    }
}

// ═══════════════════════════════════════════════════════════════
// RT-09 — Team Controller
// ═══════════════════════════════════════════════════════════════

[ApiController, Route("api/teams")]
public sealed class TeamController(ITeamRepository teams) : ControllerBase
{
    private readonly ITeamRepository _teams = teams;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await _teams.ListAsync(ct));

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
    {
        var t = await _teams.GetByIdAsync(id, ct);
        return t is not null ? Ok(t) : NotFound();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await _teams.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTeamRequest req, CancellationToken ct)
    {
        var id   = Guid.NewGuid().ToString("N")[..12];
        var team = new TeamDto(id, req.Name, req.AgentIds);
        await _teams.SaveAsync(team, ct);
        return Created($"/api/teams/{id}", team);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] CreateTeamRequest req, CancellationToken ct)
    {
        if (!await _teams.ExistsAsync(id, ct)) return NotFound();
        var updated = new TeamDto(id, req.Name, req.AgentIds);
        await _teams.UpdateAsync(id, updated, ct);
        return Ok(updated);
    }

    [HttpPost("{id}/coordinate")]
    public async Task<IActionResult> Coordinate(string id, [FromBody] string task, CancellationToken ct)
    {
        if (!await _teams.ExistsAsync(id, ct)) return NotFound();
        return Accepted(new { teamId = id, task, status = "coordinating" });
    }

    [HttpPost("{id}/broadcast")]
    public async Task<IActionResult> Broadcast(string id, [FromBody] string message, CancellationToken ct)
    {
        var t = await _teams.GetByIdAsync(id, ct);
        if (t is null) return NotFound();
        return Ok(new { teamId = id, delivered = t.AgentIds.Count });
    }
}

// ═══════════════════════════════════════════════════════════════
// RT-10 — Workflow Controller
// ═══════════════════════════════════════════════════════════════

[ApiController, Route("api/workflows")]
public sealed class WorkflowController(IWorkflowRepository workflows) : ControllerBase
{
    private readonly IWorkflowRepository _workflows = workflows;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await _workflows.ListAsync(ct));

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
    {
        var w = await _workflows.GetByIdAsync(id, ct);
        return w is not null ? Ok(w) : NotFound();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await _workflows.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWorkflowRequest req, CancellationToken ct)
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        var wf = new WorkflowDto(id, req.Name, req.StepIds, "idle");
        await _workflows.SaveAsync(wf, ct);
        return Created($"/api/workflows/{id}", wf);
    }

    [HttpPost("{id}/execute")]
    public async Task<IActionResult> Execute(string id, CancellationToken ct)
    {
        var w = await _workflows.GetByIdAsync(id, ct);
        if (w is null) return NotFound();
        var running = w with { Status = "running" };
        await _workflows.UpdateAsync(id, running, ct);
        await _workflows.RecordHistoryAsync(id, new { workflowId = id, startedAt = DateTimeOffset.UtcNow }, ct);
        return Accepted(running);
    }

    [HttpGet("{id}/history")]
    public async Task<IActionResult> History(string id, CancellationToken ct) =>
        Ok(await _workflows.GetHistoryAsync(id, ct));
}

// ═══════════════════════════════════════════════════════════════
// RT-11 — Knowledge Controller
// ═══════════════════════════════════════════════════════════════

[ApiController, Route("api/knowledge")]
public sealed class KnowledgeController : ControllerBase
{
    [HttpPost("ingest")]  public IActionResult Ingest([FromBody] KnowledgeIngestRequest req)  => Accepted(new { req.CollectionId, status = "queued" });
    [HttpPost("search")]  public IActionResult Search([FromBody] KnowledgeSearchRequest req)  => Ok(new { req.CollectionId, req.Query, results = Array.Empty<object>() });
    [HttpPost("query")]   public IActionResult Query([FromBody] KnowledgeSearchRequest req)   => Ok(new { req.CollectionId, answer = "stub answer" });
    [HttpPost("rerank")]  public IActionResult Rerank([FromBody] KnowledgeSearchRequest req)  => Ok(new { reranked = Array.Empty<object>() });
    [HttpGet("collections")]  public IActionResult Collections()  => Ok(Array.Empty<string>());
    [HttpDelete("{collectionId}")] public IActionResult Delete(string collectionId) => NoContent();
}

// ═══════════════════════════════════════════════════════════════
// RT-12 — Tool Controller
// ═══════════════════════════════════════════════════════════════

[ApiController, Route("api/tools")]
public sealed class ToolController(IToolExecutor? executor = null) : ControllerBase
{
    private readonly IToolExecutor? _executor = executor;
    private static readonly List<ToolDto> _tools = [];

    [HttpGet]              public IActionResult List()                              => Ok(_tools);
    [HttpGet("{id}/schema")] public IActionResult Schema(string id)                => Ok(new { toolId = id, schema = "{}" });
    [HttpPost("register")] public IActionResult Register([FromBody] ToolDto tool)  { _tools.Add(tool); return Created($"/api/tools/{tool.ToolId}", tool); }
    [HttpGet("toolkits")]  public IActionResult Toolkits()                          => Ok(Array.Empty<string>());

    /// <summary>Execute a registered tool by name. Uses IToolExecutor pipeline if available.</summary>
    [HttpPost("{toolId}/execute")]
    public async Task<IActionResult> Execute(
        string toolId,
        [FromBody] Dictionary<string, object>? parameters = null,
        [FromQuery] string? runId = null,
        [FromQuery] string? agentId = null,
        CancellationToken ct = default)
    {
        if (_executor is null)
            return Ok(new { toolId, status = "stub", message = "No ToolExecutor registered." });

        var ctx    = new ToolContext(agentId ?? "api", runId ?? Guid.NewGuid().ToString("N")[..8], null, parameters, null, null, null);
        var result = await _executor.ExecuteAsync(toolId, ctx, ct);

        if (!result.Success)
            return ApiErrors.Unprocessable(
                $"Tool '{toolId}' execution failed: {result.ErrorMessage}",
                result.ErrorMessage ?? "Esecuzione tool fallita.");

        return Ok(new { toolId, success = result.Success, data = result.Data, executionTimeMs = result.ExecutionTime?.TotalMilliseconds });
    }
}

// ═══════════════════════════════════════════════════════════════
// RT-13 — Auth Controller
// ═══════════════════════════════════════════════════════════════

[ApiController, Route("api/auth")]
public sealed class AuthController(IApiKeyStore keyStore) : ControllerBase
{
    [HttpPost("login")]
    public IActionResult Login([FromBody] Dictionary<string, string> creds)
    {
        // Stub: real impl validates username/password, issues JWT
        if (!creds.ContainsKey("apiKey")) return Unauthorized();
        return Ok(new { token = "stub-jwt", expiresIn = 3600 });
    }

    [HttpPost("refresh")]  public IActionResult Refresh()  => Ok(new { token = "stub-jwt-refreshed", expiresIn = 3600 });
    [HttpPost("logout")]   public IActionResult Logout()   => NoContent();
    [HttpGet("me")]        public IActionResult Me()       => Ok(new { subject = "stub-user", roles = new[] { "Viewer" } });

    [HttpPost("apikey")]
    public async Task<IActionResult> CreateApiKey([FromBody] ApiKeyCreateRequest req)
    {
        var roles  = req.Roles.Select(r => Enum.Parse<AGXIOME.Security.Rbac.AgxiomeRole>(r)).ToList();
        var expiry = req.ExpiresInDays.HasValue ? DateTimeOffset.UtcNow.AddDays(req.ExpiresInDays.Value) : (DateTimeOffset?)null;
        var (raw, record) = await keyStore.CreateAsync(roles, req.Scopes, "api-auth-stub", expiry);
        return Ok(new { keyId = record.KeyId, rawKey = raw });
    }
}

// ═══════════════════════════════════════════════════════════════
// RT-14 — Admin Controller
// ═══════════════════════════════════════════════════════════════

[ApiController, Route("api/admin")]
public sealed class AdminController : ControllerBase
{
    [HttpGet("health")]
    public IActionResult Health() => Ok(new { Status = "healthy", Ts = DateTimeOffset.UtcNow, Uptime = Environment.TickCount64 });

    [HttpGet("metrics")]
    public IActionResult Metrics() => Ok(new { agents = 0, activeSessions = 0, runsToday = 0, p95LatencyMs = 0 });

    [HttpGet("logs")]
    public IActionResult Logs([FromQuery] int lines = 50) => Ok(new { lines, entries = Array.Empty<string>() });

    [HttpGet("errors")]
    public IActionResult Errors() => Ok(Array.Empty<object>());

    [HttpGet("config")]
    public IActionResult Config() => Ok(new { version = "1.0.0", environment = "dev" });

    [HttpPost("config")]
    public IActionResult UpdateConfig([FromBody] Dictionary<string, string> patch) =>
        Ok(new { applied = patch.Count });
}
