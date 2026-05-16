// RT-06..RT-14 — REST API Controllers
// Each controller maps to a domain area. All responses are JSON.
// Auth + RBAC is enforced upstream in middleware.

using Microsoft.AspNetCore.Mvc;
using AGXIOME.Security.Audit;
using AGXIOME.Security.Sanitization;
using AGXIOME.Security.ApiKey;
using AGXIOME.Security.Rbac;
using AGXIOME.Shared.Errors;

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
public sealed class AgentController(AuditLogger audit, IInputSanitizer sanitizer) : ControllerBase
{
    private readonly AuditLogger _audit = audit;          // injected for future audit usage
    private readonly IInputSanitizer _sanitizer = sanitizer;
    // In-memory stub storage — production uses IAgentRepository
    private static readonly Dictionary<string, AgentDto> _agents = [];

    [HttpGet]
    public IActionResult List() => Ok(_agents.Values.ToList());

    [HttpGet("{id}")]
    public IActionResult Get(string id) =>
        _agents.TryGetValue(id, out var a) ? Ok(a) : ApiErrors.NotFound($"Agent '{id}' was not found.");

    [HttpPost]
    public IActionResult Create([FromBody] CreateAgentRequest req)
    {
        var check = _sanitizer.Sanitize(req.Name, SanitizationRules.AgentNameR);
        if (!check.IsValid) return ApiErrors.InvalidArguments(
            $"Agent name rejected by sanitizer: {check.ViolationDetail ?? "Invalid name"}.",
            check.ViolationDetail ?? "Nome agente non valido.");

        var id    = Guid.NewGuid().ToString("N")[..12];
        var agent = new AgentDto(id, check.SanitizedValue, req.Description, req.Model, DateTimeOffset.UtcNow);
        _agents[id] = agent;
        return Created($"/api/agents/{id}", agent);
    }

    [HttpPut("{id}")]
    public IActionResult Update(string id, [FromBody] CreateAgentRequest req)
    {
        if (!_agents.TryGetValue(id, out var existing)) return NotFound();
        _agents[id] = existing with { Name = req.Name, Description = req.Description, Model = req.Model };
        return Ok(_agents[id]);
    }

    [HttpDelete("{id}")]
    public IActionResult Delete(string id)
    {
        if (!_agents.Remove(id)) return NotFound();
        return NoContent();
    }

    [HttpPost("{id}/clone")]
    public IActionResult Clone(string id)
    {
        if (!_agents.TryGetValue(id, out var src)) return NotFound();
        var newId = Guid.NewGuid().ToString("N")[..12];
        _agents[newId] = src with { Id = newId, Name = $"{src.Name}-copy", CreatedAt = DateTimeOffset.UtcNow };
        return Created($"/api/agents/{newId}", _agents[newId]);
    }
}

// ═══════════════════════════════════════════════════════════════
// RT-07 — Run Controller
// ═══════════════════════════════════════════════════════════════

[ApiController, Route("api/runs")]
public sealed class RunController : ControllerBase
{
    private static readonly Dictionary<string, RunDto> _runs = [];

    [HttpPost]
    public IActionResult Start([FromBody] StartRunRequest req)
    {
        var runId = Guid.NewGuid().ToString("N")[..12];
        var run   = new RunDto(runId, req.AgentId, "running", DateTimeOffset.UtcNow, null, null);
        _runs[runId] = run;
        return Accepted(run);
    }

    [HttpGet("{runId}")]
    public IActionResult Status(string runId) =>
        _runs.TryGetValue(runId, out var r) ? Ok(r) : NotFound();

    [HttpDelete("{runId}")]
    public IActionResult Cancel(string runId)
    {
        if (!_runs.TryGetValue(runId, out var r)) return NotFound();
        _runs[runId] = r with { Status = "cancelled", EndedAt = DateTimeOffset.UtcNow };
        return Ok(_runs[runId]);
    }

    [HttpPost("{runId}/resume")]
    public IActionResult Resume(string runId)
    {
        if (!_runs.TryGetValue(runId, out var r)) return NotFound();
        if (r.Status != "paused") return ApiErrors.Conflict($"Run '{runId}' cannot resume because status is '{r.Status}'.");
        _runs[runId] = r with { Status = "running" };
        return Ok(_runs[runId]);
    }

    [HttpGet]
    public IActionResult List([FromQuery] string? agentId) =>
        Ok(_runs.Values.Where(r => agentId == null || r.AgentId == agentId).ToList());

    [HttpGet("{runId}/stream")]
    public IActionResult Stream(string runId)
    {
        // SSE endpoint stub — real impl uses IAsyncEnumerable + Server-Sent Events
        if (!_runs.ContainsKey(runId)) return NotFound();
        return Ok(new { runId, message = "stream not yet connected" });
    }

    [HttpGet("{runId}/result")]
    public IActionResult Result(string runId) =>
        _runs.TryGetValue(runId, out var r) ? Ok(r) : NotFound();

    [HttpPost("{runId}/pause")]
    public IActionResult Pause(string runId)
    {
        if (!_runs.TryGetValue(runId, out var r)) return NotFound();
        _runs[runId] = r with { Status = "paused" };
        return Ok(_runs[runId]);
    }
}

// ═══════════════════════════════════════════════════════════════
// RT-08 — Session Controller
// ═══════════════════════════════════════════════════════════════

[ApiController, Route("api/sessions")]
public sealed class SessionController : ControllerBase
{
    private static readonly Dictionary<string, SessionDto> _sessions = [];

    [HttpGet]
    public IActionResult List([FromQuery] string? agentId) =>
        Ok(_sessions.Values.Where(s => agentId == null || s.AgentId == agentId).ToList());

    [HttpGet("{id}")]
    public IActionResult Get(string id) =>
        _sessions.TryGetValue(id, out var s) ? Ok(s) : NotFound();

    [HttpPost]
    public IActionResult Create([FromBody] SessionDto req)
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        var s  = req with { SessionId = id, CreatedAt = DateTimeOffset.UtcNow };
        _sessions[id] = s;
        return Created($"/api/sessions/{id}", s);
    }

    [HttpDelete("{id}")]
    public IActionResult Delete(string id)
    {
        if (!_sessions.Remove(id)) return NotFound();
        return NoContent();
    }

    [HttpPatch("{id}/close")]
    public IActionResult Close(string id)
    {
        if (!_sessions.TryGetValue(id, out var s)) return NotFound();
        _sessions[id] = s with { IsActive = false };
        return Ok(_sessions[id]);
    }
}

// ═══════════════════════════════════════════════════════════════
// RT-09 — Team Controller
// ═══════════════════════════════════════════════════════════════

[ApiController, Route("api/teams")]
public sealed class TeamController : ControllerBase
{
    private static readonly Dictionary<string, TeamDto> _teams = [];

    [HttpGet]                    public IActionResult List()                      => Ok(_teams.Values);
    [HttpGet("{id}")]            public IActionResult Get(string id)              => _teams.TryGetValue(id, out var t) ? Ok(t) : NotFound();
    [HttpDelete("{id}")]         public IActionResult Delete(string id)           { _teams.Remove(id); return NoContent(); }

    [HttpPost]
    public IActionResult Create([FromBody] CreateTeamRequest req)
    {
        var id   = Guid.NewGuid().ToString("N")[..12];
        var team = new TeamDto(id, req.Name, req.AgentIds);
        _teams[id] = team;
        return Created($"/api/teams/{id}", team);
    }

    [HttpPut("{id}")]
    public IActionResult Update(string id, [FromBody] CreateTeamRequest req)
    {
        if (!_teams.ContainsKey(id)) return NotFound();
        _teams[id] = new TeamDto(id, req.Name, req.AgentIds);
        return Ok(_teams[id]);
    }

    [HttpPost("{id}/coordinate")]
    public IActionResult Coordinate(string id, [FromBody] string task)
    {
        if (!_teams.ContainsKey(id)) return NotFound();
        // Production: route to TeamCoordinator service
        return Accepted(new { teamId = id, task, status = "coordinating" });
    }

    [HttpPost("{id}/broadcast")]
    public IActionResult Broadcast(string id, [FromBody] string message)
    {
        if (!_teams.ContainsKey(id)) return NotFound();
        return Ok(new { teamId = id, delivered = _teams[id].AgentIds.Count });
    }
}

// ═══════════════════════════════════════════════════════════════
// RT-10 — Workflow Controller
// ═══════════════════════════════════════════════════════════════

[ApiController, Route("api/workflows")]
public sealed class WorkflowController : ControllerBase
{
    private static readonly Dictionary<string, WorkflowDto> _wf = [];
    private static readonly List<object> _history = [];

    [HttpGet]           public IActionResult List()              => Ok(_wf.Values);
    [HttpGet("{id}")]   public IActionResult Get(string id)      => _wf.TryGetValue(id, out var w) ? Ok(w) : NotFound();
    [HttpDelete("{id}")] public IActionResult Delete(string id)  { _wf.Remove(id); return NoContent(); }

    [HttpPost]
    public IActionResult Create([FromBody] CreateWorkflowRequest req)
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        _wf[id] = new WorkflowDto(id, req.Name, req.StepIds, "idle");
        return Created($"/api/workflows/{id}", _wf[id]);
    }

    [HttpPost("{id}/execute")]
    public IActionResult Execute(string id)
    {
        if (!_wf.TryGetValue(id, out var w)) return NotFound();
        _wf[id] = w with { Status = "running" };
        _history.Add(new { workflowId = id, startedAt = DateTimeOffset.UtcNow });
        return Accepted(_wf[id]);
    }

    [HttpGet("{id}/history")]
    public IActionResult History(string id) =>
        Ok(_history.Where(h => h.ToString()!.Contains(id)).ToList());
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
public sealed class ToolController : ControllerBase
{
    private static readonly List<ToolDto> _tools = [];

    [HttpGet]              public IActionResult List()                              => Ok(_tools);
    [HttpGet("{id}/schema")] public IActionResult Schema(string id)                => Ok(new { toolId = id, schema = "{}" });
    [HttpPost("register")] public IActionResult Register([FromBody] ToolDto tool)  { _tools.Add(tool); return Created($"/api/tools/{tool.ToolId}", tool); }
    [HttpGet("toolkits")]  public IActionResult Toolkits()                          => Ok(Array.Empty<string>());
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
