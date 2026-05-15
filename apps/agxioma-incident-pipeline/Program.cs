using AGXIOME.Core;
using AGXIOME.Core.DependencyInjection;
using AGXIOME.Core.Patterns;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using AGXIOME.Storage;
using AGXIOME.Storage.Chunkers;
using AGXIOME.Storage.Embedders;
using AGXIOME.Storage.VectorStores;
using AgxiomaIncidentPipeline;

// ── Bootstrap ─────────────────────────────────────────────────────────────────
var builder = WebApplication.CreateBuilder(args);

var providerName = builder.Configuration["Agent:Provider"] ?? "deepseek";
var model        = builder.Configuration["Agent:Model"];
var workers      = builder.Configuration.GetValue("Agent:ResearchWorkers", 5);

// ── Resolve LLM provider ──────────────────────────────────────────────────────
var resolved = AgxiomeProviderResolver.Resolve(providerName, builder.Configuration);
var provider = resolved.Provider;
var resolvedModel = !string.IsNullOrEmpty(model) ? model : resolved.DefaultModel;
var apiKey   = AgxiomeProviderResolver.ResolveApiKey(provider, builder.Configuration);

// ── RAG memory: embedder + chunker + vector store + knowledge base ────────────
builder.Services.AddHttpClient("embedder");
builder.Services.AddSingleton<IEmbedder>(sp =>
    new OllamaMxbaiEmbedder(sp.GetRequiredService<IHttpClientFactory>().CreateClient("embedder")));
builder.Services.AddSingleton<IChunker, RecursiveChunker>();
builder.Services.AddSingleton<IVectorStore, HNSWLibStore>();
builder.Services.AddSingleton<KnowledgeBase>(sp => new KnowledgeBase(
    sp.GetRequiredService<IChunker>(),
    sp.GetRequiredService<IEmbedder>(),
    sp.GetRequiredService<IVectorStore>(),
    new KnowledgeBaseOptions { ChunkMaxTokens = 512, ChunkOverlap = 64,
        DefaultSearchOptions = new KnowledgeSearchOptions(TopK: 3, MinScore: 0.4f) }));

// ── IncidentStore ─────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IncidentStore>();

// ── Swarms via fluent API ─────────────────────────────────────────────────────
builder.Services.AddAgxiome()
    .UseProvider(providerName, resolvedModel)

    // ForestSwarm — incident classification router
    .AddForestSwarm("router", forest =>
    {
        forest.AddTree("auth-incident")
            .AddAgent("auth-classifier",
                "You are an incident classification expert specialising in authentication systems. " +
                "When given an incident description, determine: severity (Sev1/Sev2/Sev3), " +
                "whether this is an auth/JWT/session/token issue, and the immediate escalation path. " +
                "Be concise: output severity, incident type, affected services, and escalation path.")
            .WithKeywords("401", "403", "auth", "jwt", "token", "session", "login", "access", "unauthorized", "forbidden");

        forest.AddTree("infra-incident")
            .AddAgent("infra-classifier",
                "You are an incident classification expert specialising in infrastructure and Kubernetes. " +
                "Classify: severity (Sev1/Sev2/Sev3), whether this is a deployment/pod/OOM/network issue, " +
                "and the immediate escalation path. Be concise.")
            .WithKeywords("pod", "crash", "oom", "deploy", "kubernetes", "k8s", "node", "container", "restart", "eviction");

        forest.AddTree("performance-incident")
            .AddAgent("perf-classifier",
                "You are an incident classification expert specialising in performance and latency. " +
                "Classify: severity (Sev1/Sev2/Sev3), whether this is a latency/throughput/cache/DB issue, " +
                "and the immediate escalation path. Be concise.")
            .WithKeywords("slow", "latency", "timeout", "throughput", "cache", "redis", "queue", "backpressure", "saturation");

        forest.AddTree("general-incident")
            .AddAgent("general-classifier",
                "You are an incident classification expert. " +
                "Classify any incident into severity (Sev1/Sev2/Sev3), incident type, " +
                "affected services, and escalation path. Be concise.")
            .WithKeywords("incident", "alert", "error", "outage", "spike", "anomaly", "issue", "failure", "down");
    })

    // HeavySwarm — parallel root cause investigation (5 workers)
    .AddHeavySwarm("investigator", heavy =>
    {
        heavy
            .WithWorkers(workers)
            .WithParallelResearch()
            .WithQuestionPrompt(
                "You are a senior SRE and incident investigator. " +
                "Given the incident description and evidence (logs, metrics, K8s state, deploys, commits), " +
                "generate 5 precise investigation questions covering:\n" +
                "1. Infrastructure correlation (K8s, pods, restarts)\n" +
                "2. Deploy/commit correlation (what changed recently?)\n" +
                "3. Dependency analysis (Redis, DB, downstream services)\n" +
                "4. Error propagation path (how did this fail?)\n" +
                "5. Timeline reconstruction (when did each signal appear?)\n" +
                "Output each question on a new line.")
            .WithWorkerPrompt(
                "You are Research Worker #{n} in a parallel incident investigation team. " +
                "Answer the assigned question thoroughly using the evidence provided. " +
                "Cite specific log entries, metric values, and commit SHAs where relevant. " +
                "Flag data gaps and assumptions explicitly.")
            .WithSynthesisPrompt(
                "You are the Lead Incident Commander synthesising findings from {n} parallel investigators.\n\n" +
                "Produce a structured Incident Analysis Report:\n\n" +
                "ROOT CAUSE: (one sentence, precise)\n" +
                "BLAST RADIUS: (scope of impact, number of users/requests affected)\n" +
                "TIMELINE: (chronological sequence of events)\n" +
                "CONFIDENCE: (0-100%)\n" +
                "MITIGATION: (immediate action to stop the bleeding)\n" +
                "RECOMMENDED ACTIONS:\n" +
                "- action 1\n" +
                "- action 2\n" +
                "- action 3\n\n" +
                "Be precise, actionable, and evidence-based. No speculation without evidence.");
    });

// ── Postmortem agent (standalone WikiAgent-style) ─────────────────────────────
builder.Services.AddSingleton<IAgxiomeAgent>(_ =>
    AgxiomeBuilder.Agent("postmortem-writer")
        .UseModel(provider, resolvedModel, apiKey)
        .WithSystemPrompt(
            "You are a senior technical writer and SRE. " +
            "You write precise, actionable incident postmortems in Markdown. " +
            "Your postmortems are blameless, evidence-based, and focus on systemic improvements. " +
            "Always include: timeline table, root cause tree, action items with owners, and prevention measures.")
        .Build()
);

// ── Pipeline orchestrator ─────────────────────────────────────────────────────
builder.Services.AddSingleton<IncidentPipelineOrchestrator>(sp =>
    new IncidentPipelineOrchestrator(
        sp.GetRequiredService<ForestSwarm>(),
        sp.GetRequiredService<HeavySwarm>(),
        sp.GetRequiredService<IAgxiomeAgent>(),
        sp.GetRequiredService<IncidentStore>()
    )
);

var app = builder.Build();

// ═════════════════════════════════════════════════════════════════════════════
// ENDPOINTS
// ═════════════════════════════════════════════════════════════════════════════

// ── Health ────────────────────────────────────────────────────────────────────
app.MapGet("/health", (IConfiguration cfg, IncidentStore store) => Results.Ok(new
{
    status   = "ok",
    provider = cfg["Agent:Provider"],
    model    = cfg["Agent:Model"],
    pipeline = new[] { "classify (ForestSwarm)", "investigate (HeavySwarm)", "postmortem (WikiAgent)", "memory (HNSWLib)" },
    openIncidents = store.All().Count(i => i.Status != IncidentStatus.Completed && i.Status != IncidentStatus.Rejected),
    utc      = DateTime.UtcNow
}));

// ── Submit incident ───────────────────────────────────────────────────────────
// Runs full pipeline and returns when HITL gate is reached (AwaitingApproval).
app.MapPost("/incident", async (IncidentRequest req, IncidentPipelineOrchestrator pipeline, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Description))
        return Results.BadRequest(new { error = "description is required." });

    var record = await pipeline.RunAsync(req.Description, ct);

    return Results.Ok(new
    {
        incidentId      = record.Id,
        status          = record.Status.ToString(),
        severity        = record.Severity.ToString(),
        classification  = record.Classification,
        rootCause       = record.Investigation?.RootCause,
        confidence      = record.Investigation?.ConfidenceScore,
        mitigation      = record.Investigation?.Mitigation,
        proposedActions = record.ProposedActions.Select(a => new
        {
            actionId    = a.ActionId,
            type        = a.Type,
            description = a.Description
        }),
        postmortemTitle = record.Postmortem?.Title,
        timeline        = record.Timeline.Select(t => new { ts = t.Ts, phase = t.Phase, detail = t.Detail }),
        message         = "Pipeline complete. Review proposed actions and POST /incident/{id}/approve to execute."
    });
});

// ── Get incident details ──────────────────────────────────────────────────────
app.MapGet("/incident/{id}", (string id, IncidentStore store) =>
{
    var record = store.Get(id);
    if (record is null) return Results.NotFound(new { error = $"Incident '{id}' not found." });

    return Results.Ok(new
    {
        incidentId      = record.Id,
        status          = record.Status.ToString(),
        severity        = record.Severity.ToString(),
        createdAt       = record.CreatedAt,
        completedAt     = record.CompletedAt,
        rawInput        = record.RawInput,
        classification  = record.Classification,
        investigation   = record.Investigation is not null ? new
        {
            rootCause          = record.Investigation.RootCause,
            blastRadius        = record.Investigation.BlastRadius,
            timeline           = record.Investigation.Timeline,
            mitigation         = record.Investigation.Mitigation,
            recommendedActions = record.Investigation.RecommendedActions,
            confidenceScore    = record.Investigation.ConfidenceScore
        } : null,
        postmortem      = record.Postmortem is not null ? new
        {
            title                = record.Postmortem.Title,
            markdownContent      = record.Postmortem.MarkdownContent,
            remediationChecklist = record.Postmortem.RemediationChecklist,
            preventiveActions    = record.Postmortem.PreventiveActions
        } : null,
        proposedActions = record.ProposedActions,
        executedActions = record.ExecutedActions,
        approverNote    = record.ApproverNote,
        timeline        = record.Timeline.Select(t => new { ts = t.Ts, phase = t.Phase, detail = t.Detail })
    });
});

// ── Approve + execute actions ─────────────────────────────────────────────────
// Body: { "note": "approved by @oncall", "actionIds": ["action-jira", "action-status-page"] }
// Omit actionIds to execute all proposed actions.
app.MapPost("/incident/{id}/approve", async (
    string id,
    ApproveRequest req,
    IncidentPipelineOrchestrator pipeline,
    CancellationToken ct) =>
{
    try
    {
        var record = await pipeline.ApproveAndExecuteAsync(id, req.Note, req.ActionIds, ct);
        return Results.Ok(new
        {
            incidentId     = record.Id,
            status         = record.Status.ToString(),
            executedActions = record.ExecutedActions.Select(a => new
            {
                actionId    = a.ActionId,
                type        = a.Type,
                success     = a.Success,
                executedAt  = a.ExecutedAt
            }),
            message        = "Actions executed. Postmortem indexed into knowledge base."
        });
    }
    catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
    catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
});

// ── Reject incident ───────────────────────────────────────────────────────────
app.MapPost("/incident/{id}/reject", (string id, RejectRequest req, IncidentStore store) =>
{
    var record = store.Get(id);
    if (record is null) return Results.NotFound(new { error = $"Incident '{id}' not found." });

    record.Status      = IncidentStatus.Rejected;
    record.ApproverNote = req.Reason;
    record.CompletedAt = DateTime.UtcNow;
    record.AddTimeline("Rejected", $"Human rejected. Reason: {req.Reason ?? "(none)"}");

    return Results.Ok(new { incidentId = id, status = "Rejected", reason = req.Reason });
});

// ── List all incidents ────────────────────────────────────────────────────────
app.MapGet("/incidents", (IncidentStore store) =>
    Results.Ok(store.All().Select(r => new
    {
        incidentId = r.Id,
        status     = r.Status.ToString(),
        severity   = r.Severity.ToString(),
        createdAt  = r.CreatedAt,
        rootCause  = r.Investigation?.RootCause,
        rawInput   = r.RawInput[..Math.Min(80, r.RawInput.Length)]
    }))
);

// ── Search past incidents (semantic memory) ───────────────────────────────────
app.MapGet("/incidents/search", async (string q, IncidentStore store, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(q))
        return Results.BadRequest(new { error = "q parameter is required." });

    var results = await store.SearchSimilarAsync(q, ct);
    return Results.Ok(new
    {
        query   = q,
        results = results.Select(r => new
        {
            score   = r.Score,
            content = r.Content[..Math.Min(200, r.Content.Length)],
            metadata = r.Metadata
        })
    });
});

app.MapGet("/", () =>
    "AGXIOME Incident Intelligence Pipeline\n" +
    "POST /incident              — submit incident, run full pipeline\n" +
    "GET  /incident/{id}         — get incident details + postmortem\n" +
    "POST /incident/{id}/approve — execute proposed actions (HITL)\n" +
    "POST /incident/{id}/reject  — reject without executing actions\n" +
    "GET  /incidents             — list all incidents\n" +
    "GET  /incidents/search?q=   — semantic search past postmortems");

app.Run();

// ── Request/Response models ───────────────────────────────────────────────────

/// <summary>Incident submission.</summary>
public record IncidentRequest(string Description);

/// <summary>Approve and optionally select which actions to execute.</summary>
public record ApproveRequest(string? Note = null, IEnumerable<string>? ActionIds = null);

/// <summary>Reject without executing actions.</summary>
public record RejectRequest(string? Reason = null);
