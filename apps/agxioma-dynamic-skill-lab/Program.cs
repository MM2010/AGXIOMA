using System.Diagnostics;
using AGXIOME.Core;
using AGXIOME.Core.DependencyInjection;
using AGXIOME.DynamicRuntime.Compilation;
using AGXIOME.DynamicRuntime.Generation;
using AGXIOME.DynamicRuntime.Generation.Prompts;
using AGXIOME.DynamicRuntime.Interfaces;
using AGXIOME.DynamicRuntime.Registry;
using AGXIOME.LlmWiky;
using AGXIOME.Runtime;
using AGXIOME.Security.Rbac;
using AGXIOME.Shared.Config;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using AgxiomaDynamicSkillLab;
using AgxiomaDynamicSkillLab.Engine;
using AgxiomaDynamicSkillLab.Storage;
using Microsoft.Data.Sqlite;

// ═══════════════════════════════════════════════════════════════════════════════
//  T8 — agxioma-dynamic-skill-lab
//  Sistema di runtime self-extension: detect → generate → validate → compile → run
// ═══════════════════════════════════════════════════════════════════════════════

var builder = WebApplication.CreateBuilder(args);
builder.AddAgxiomeSecurityDefaults();

var cfg = builder.Configuration.GetSection("Agent").Get<AgentSettings>() ?? new AgentSettings();
var runtimeMutationEnabled = IsRuntimeMutationEnabled(builder.Configuration, builder.Environment);

// ── Storage ───────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<SkillLabStore>(_ => new SkillLabStore(cfg.DatabasePath));

// ── Engine ────────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<DynamicToolBuilder>();
builder.Services.AddSingleton<StaticCodeAnalyzer>();
builder.Services.AddSingleton(_ => new RoslynCompiler(runtimeMutationEnabled, cfg.MaxDynamicSourceChars));
builder.Services.AddSingleton<RuntimeToolRegistry>();
builder.Services.AddSingleton(_ => new SandboxExecutor(
    cfg.ExecutionTimeoutMs,
    runtimeMutationEnabled,
    cfg.MaxExecutionInputChars,
    cfg.MaxExecutionOutputChars,
    cfg.MaxConcurrentExecutions));

// ── LLM Provider via AGXIOME ──────────────────────────────────────────────────
builder.Services.AddSingleton<ILLMProvider>(sp =>
{
    var (provider, defaultModel, _) = AgxiomeProviderResolver.Resolve(builder.Configuration["Agent:Provider"] ?? "openai", builder.Configuration);
    var model  = cfg.Model ?? defaultModel;
    var apiKey = AgxiomeProviderResolver.ResolveApiKey(provider, builder.Configuration);

    return LLMProviderFactory.Create(new LLMWIKYConfig
    {
        Provider = provider,
        ApiKey   = apiKey,
        Model    = model
    });
});

// ── Orchestrator ──────────────────────────────────────────────────────────────
builder.Services.AddSingleton<SkillLabOrchestrator>(sp =>
{
    var (provider, defaultModel2, _) = AgxiomeProviderResolver.Resolve(builder.Configuration["Agent:Provider"] ?? "openai", builder.Configuration);
    var model = cfg.Model ?? defaultModel2;
    return new SkillLabOrchestrator(
        sp.GetRequiredService<SkillLabStore>(),
        sp.GetRequiredService<DynamicToolBuilder>(),
        sp.GetRequiredService<StaticCodeAnalyzer>(),
        sp.GetRequiredService<RoslynCompiler>(),
        sp.GetRequiredService<RuntimeToolRegistry>(),
        sp.GetRequiredService<SandboxExecutor>(),
        sp.GetRequiredService<ILLMProvider>(),
        model,
        provider
    );
});

var app = builder.Build();

// Detailed exception pages are only safe in local development.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

await app.UseAgxiomeSecurityDefaultsAsync();

var runtimeMutationGroup = app.MapGroup(string.Empty)
    .AddEndpointFilter(async (context, next) =>
    {
        if (runtimeMutationEnabled)
            return await next(context);

        return Results.Json(
            new
            {
                code = "runtime_mutation_disabled",
                message = "Runtime compile, run, and promote operations are disabled outside approved development workflows."
            },
            statusCode: StatusCodes.Status403Forbidden);
    });

// Init SQLite
await app.Services.GetRequiredService<SkillLabStore>().InitAsync();

// ═══════════════════════════════════════════════════════════════════════════════
//  ENDPOINTS
// ═══════════════════════════════════════════════════════════════════════════════

app.MapGet("/health", () => Results.Ok(new
{
    status       = "ok",
    app          = "agxioma-dynamic-skill-lab",
    pattern      = "Self-Extension Runtime",
    lifecycle    = new[] { "detect", "generate", "analyze", "compile", "test", "sandbox", "promote" },
    utc          = DateTime.UtcNow
}))
    .AllowAnonymousAgxiome();

// ─── POST /pipeline — Full lifecycle: detect → generate → analyze → compile → execute ──
app.MapPost("/pipeline", async (
    CapabilityRequest req,
    SkillLabOrchestrator orchestrator,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Task))
        return Results.BadRequest(new { error = "Task description is required." });

    var result = await orchestrator.RunFullPipelineAsync(req.Task, req.SessionId, ct);
    return result.Errors is { Count: > 0 }
        ? Results.Problem(detail: null, statusCode: 422, extensions: new Dictionary<string, object?> { ["result"] = result })
        : Results.Ok(result);
})
    .RequireAgxiomePermission(AgxiomePermission.WorkflowExecute)
    .WithAgxiomeRateLimit(10, TimeSpan.FromMinutes(1));

// ─── POST /pipeline/detect — Step 1: Detect capability gap ─────────────────────
app.MapPost("/pipeline/detect", async (
    CapabilityRequest req,
    SkillLabOrchestrator orchestrator,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Task))
        return Results.BadRequest(new { error = "Task description is required." });

    var proposal = await orchestrator.DetectCapabilityAsync(req.Task, req.SessionId, ct);
    return Results.Ok(new PipelineResponse(req.SessionId, proposal.ToolId, proposal.ToolName,
        ToolStatus.Proposed, "detected", proposal));
})
    .RequireAgxiomePermission(AgxiomePermission.WorkflowExecute)
    .WithAgxiomeRateLimit(15, TimeSpan.FromMinutes(1));

// ─── POST /pipeline/generate — Step 2: Generate tool code from proposal ────────
app.MapPost("/pipeline/generate", async (
    ToolGenerateRequest req,
    SkillLabOrchestrator orchestrator,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.ToolId) || string.IsNullOrWhiteSpace(req.Description))
        return Results.BadRequest(new { error = "ToolId and Description are required." });

    var proposal = new ToolProposal(req.ToolId, req.ToolName, req.Description,
        "User-requested tool", new List<string> { "System", "System.Text.Json", "System.Linq" });

    var tool = await orchestrator.GenerateToolAsync(proposal, req.TaskContext, req.SessionId, ct);
    return Results.Ok(new PipelineResponse(req.SessionId, tool.Id, tool.Name,
        tool.Status, "generated", new { sourceCode = tool.SourceCode, manifest = tool.ManifestJson }));
})
    .RequireAgxiomePermission(AgxiomePermission.WorkflowExecute)
    .WithAgxiomeRateLimit(10, TimeSpan.FromMinutes(1));

// ─── POST /pipeline/analyze — Step 3: Static analysis ──────────────────────────
app.MapPost("/pipeline/{toolId}/analyze", async (
    string toolId,
    SkillLabOrchestrator orchestrator,
    SkillLabStore store,
    CancellationToken ct) =>
{
    var tool = store.GetTool(toolId);
    if (tool == null) return Results.NotFound(new { error = $"Tool '{toolId}' not found." });

    var analyzed = await orchestrator.AnalyzeToolAsync(tool, "direct", ct);
    return Results.Ok(new PipelineResponse("direct", analyzed.Id, analyzed.Name,
        analyzed.Status, "analyzed",
        new { validationPassed = analyzed.Status == ToolStatus.Validated, violations = analyzed.ValidationErrors },
        analyzed.ValidationErrors));
})
    .RequireAgxiomePermission(AgxiomePermission.WorkflowExecute)
    .WithAgxiomeRateLimit(20, TimeSpan.FromMinutes(1));

// ─── POST /pipeline/{toolId}/compile — Step 4: Roslyn compilation ──────────────
runtimeMutationGroup.MapPost("/pipeline/{toolId}/compile", async (
    string toolId,
    SkillLabOrchestrator orchestrator,
    SkillLabStore store,
    CancellationToken ct) =>
{
    var tool = store.GetTool(toolId);
    if (tool == null) return Results.NotFound(new { error = $"Tool '{toolId}' not found." });

    var compiled = await orchestrator.CompileToolAsync(tool, "direct", ct);
    return compiled.Status == ToolStatus.Compiled
        ? Results.Ok(new PipelineResponse("direct", compiled.Id, compiled.Name,
            compiled.Status, "compiled", new { assembly = "loaded in isolated ALC" }))
        : Results.Problem(detail: null, statusCode: 422, extensions: new Dictionary<string, object?> { ["result"] = new PipelineResponse("direct", compiled.Id, compiled.Name,
            compiled.Status, "compilation-failed", null, compiled.CompilationErrors) });
})
    .RequireAgxiomePermission(AgxiomePermission.WorkflowExecute)
    .WithAgxiomeRateLimit(10, TimeSpan.FromMinutes(1));

// ─── POST /pipeline/{toolId}/run — Step 5: Sandbox execution ───────────────────
runtimeMutationGroup.MapPost("/pipeline/{toolId}/run", async (
    string toolId,
    ToolRunRequest req,
    SkillLabOrchestrator orchestrator,
    SkillLabStore store,
    CancellationToken ct) =>
{
    var tool = store.GetTool(toolId);
    if (tool == null) return Results.NotFound(new { error = $"Tool '{toolId}' not found." });

    var execResult = await orchestrator.RunToolAsync(tool, req.Input, req.SessionId, ct);
    return Results.Ok(new PipelineResponse(req.SessionId, toolId, tool.Name,
        execResult.Success ? ToolStatus.Executed : ToolStatus.Failed,
        execResult.Success ? "completed" : "execution-failed",
        execResult, execResult.Errors));
})
    .RequireAgxiomePermission(AgxiomePermission.WorkflowExecute)
    .WithAgxiomeRateLimit(10, TimeSpan.FromMinutes(1));

// ─── POST /pipeline/{toolId}/promote — Step 6: Promote to persistent skill ─────
runtimeMutationGroup.MapPost("/pipeline/{toolId}/promote", async (
    string toolId,
    ToolPromoteRequest req,
    SkillLabOrchestrator orchestrator,
    SkillLabStore store,
    CancellationToken ct) =>
{
    var tool = store.GetTool(toolId);
    if (tool == null) return Results.NotFound(new { error = $"Tool '{toolId}' not found." });

    ToolDescriptor promoted;
    try
    {
        promoted = await orchestrator.PromoteSkillAsync(tool, req.SessionId, ct);
    }
    catch (PromotionPolicyViolationException ex)
    {
        return Results.Problem(
            detail: null,
            statusCode: 422,
            extensions: new Dictionary<string, object?>
            {
                ["errors"] = ex.Errors,
                ["toolId"] = toolId,
                ["code"] = "promotion_policy_rejected"
            });
    }

    return Results.Ok(new PipelineResponse(req.SessionId, tool.Id, tool.Name,
        ToolStatus.Promoted, "promoted",
        new { message = $"Tool '{tool.Name}' promoted to persistent skill" }));
})
    .RequireAgxiomePermission(AgxiomePermission.WorkflowExecute)
    .WithAgxiomeRateLimit(5, TimeSpan.FromMinutes(1));

// ─── GET /tools — List all generated tools ─────────────────────────────────────
app.MapGet("/tools", (SkillLabStore store) =>
{
    var tools = store.GetAllTools();
    return Results.Ok(new
    {
        count = tools.Count,
        tools = tools.Select(t => new
        {
            t.Id, t.Name, t.Description, status = t.Status.ToString(),
            t.CreatedAt, hasCode = !string.IsNullOrEmpty(t.SourceCode)
        })
    });
})
    .RequireAgxiomePermission(AgxiomePermission.AgentRead)
    .WithAgxiomeRateLimit(30, TimeSpan.FromMinutes(1));

// ─── GET /tools/{toolId} — Get full tool details ──────────────────────────────
app.MapGet("/tools/{toolId}", (string toolId, SkillLabStore store) =>
{
    var tool = store.GetTool(toolId);
    return tool == null
        ? Results.NotFound(new { error = $"Tool '{toolId}' not found." })
        : Results.Ok(tool);
})
    .RequireAgxiomePermission(AgxiomePermission.AgentRead)
    .WithAgxiomeRateLimit(30, TimeSpan.FromMinutes(1));

// ─── GET /tools/active — List active (loaded) tools ───────────────────────────
app.MapGet("/tools/active", (RuntimeToolRegistry registry) =>
{
    var ids = registry.GetActiveToolIds();
    return Results.Ok(new { count = ids.Count, toolIds = ids });
})
    .RequireAgxiomePermission(AgxiomePermission.AgentRead)
    .WithAgxiomeRateLimit(30, TimeSpan.FromMinutes(1));

// ─── DELETE /tools/{toolId} — Unload and remove a tool ────────────────────────
app.MapDelete("/tools/{toolId}", (string toolId, RuntimeToolRegistry registry, SkillLabStore store) =>
{
    registry.Unload(toolId);
    return Results.Ok(new { toolId, unloaded = true });
})
    .RequireAgxiomePermission(AgxiomePermission.AgentDelete)
    .WithAgxiomeRateLimit(10, TimeSpan.FromMinutes(1));

// ─── GET /skills — List promoted persistent skills ────────────────────────────
app.MapGet("/skills", async (SkillLabStore store, CancellationToken ct) =>
{
    var skills = await store.GetPersistentSkillsAsync(ct);
    return Results.Ok(new { count = skills.Count, skills });
})
    .RequireAgxiomePermission(AgxiomePermission.AgentRead)
    .WithAgxiomeRateLimit(30, TimeSpan.FromMinutes(1));

// ─── GET /audit/{sessionId} — Get audit trail for a session ───────────────────
app.MapGet("/audit/{sessionId}", async (
    string sessionId,
    CancellationToken ct) =>
{
    // Read directly from SQLite for the audit log
    await using var conn = new SqliteConnection(
        new SqliteConnectionStringBuilder(
            $"Data Source={cfg.DatabasePath}").ToString());
    await conn.OpenAsync(ct);

    var cmd = conn.CreateCommand();
    cmd.CommandText = """
        SELECT id, session_id, tool_id, step, status, details, recorded_at
        FROM audit_log
        WHERE session_id = $sid
        ORDER BY recorded_at ASC
        """;
    cmd.Parameters.AddWithValue("$sid", sessionId);

    var entries = new List<object>();
    await using var reader = await cmd.ExecuteReaderAsync(ct);
    while (await reader.ReadAsync(ct))
    {
        entries.Add(new
        {
            id = reader.GetInt32(0),
            sessionId = reader.GetString(1),
            toolId = reader.IsDBNull(2) ? null : reader.GetString(2),
            step = reader.GetString(3),
            status = reader.GetString(4),
            details = reader.IsDBNull(5) ? null : reader.GetString(5),
            recordedAt = reader.GetString(6)
        });
    }
    return Results.Ok(new { sessionId, count = entries.Count, entries });
})
    .RequireAgxiomePermission(AgxiomePermission.AdminLogs)
    .WithAgxiomeRateLimit(20, TimeSpan.FromMinutes(1));

// ─── DELETE /cache — Evict expired tools ──────────────────────────────────────
app.MapDelete("/cache", (RuntimeToolRegistry registry) =>
{
    var before = registry.Count;
    registry.EvictExpired();
    return Results.Ok(new { evicted = before - registry.Count, remaining = registry.Count });
})
    .RequireAgxiomePermission(AgxiomePermission.AdminConfig)
    .WithAgxiomeRateLimit(5, TimeSpan.FromMinutes(1));

static bool IsRuntimeMutationEnabled(IConfiguration configuration, IWebHostEnvironment environment)
{
    var flag = configuration["DynamicRuntime:EnableMutationEndpoints"]
               ?? Environment.GetEnvironmentVariable("AGXIOME_ENABLE_MUTATION_ENDPOINTS");

    return environment.IsDevelopment()
        && bool.TryParse(flag, out var enabled)
        && enabled;
}

    app.Run();

    public partial class Program { }
