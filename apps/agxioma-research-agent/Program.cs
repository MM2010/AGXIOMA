using AGXIOME.Core;
using AGXIOME.Core.DependencyInjection;
using AGXIOME.Core.Patterns;
using AGXIOME.Runtime;
using AGXIOME.Security.Rbac;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using AgxiomaResearchAgent;

// ── Bootstrap ─────────────────────────────────────────────────────────────────
var builder = WebApplication.CreateBuilder(args);
builder.AddAgxiomeSecurityDefaults();

var cfg = builder.Configuration.GetSection("Agent").Get<AgentSettings>() ?? new AgentSettings();

// Build the HeavySwarm
builder.Services.AddSingleton<HeavySwarm>(sp =>
{
    var (provider, defaultModel, _) = AgxiomeProviderResolver.Resolve(cfg.Provider, builder.Configuration);
    var orchModel = cfg.OrchestratorModel ?? defaultModel;
    var wrkModel  = cfg.WorkerModel       ?? defaultModel;
    var apiKey    = AgxiomeProviderResolver.ResolveApiKey(provider, builder.Configuration);

    // Question agent: generates the research question from raw input
    var questionAgent = AgxiomeBuilder.Agent("question-agent")
        .UseModel(provider, orchModel, apiKey)
        .WithSystemPrompt(
            "You are a research architect. Given a topic or request, generate the single most important " +
            "research question that, if answered, would provide maximum insight.")
        .Build();

    // Research workers: run in parallel, each takes a sub-question
    var workerCount = cfg.WorkerCount > 0 ? cfg.WorkerCount : 3;
    var workers = Enumerable.Range(1, workerCount).Select(i =>
        AgxiomeBuilder.Agent($"worker-{i}")
            .UseModel(provider, wrkModel, apiKey)
            .WithSystemPrompt(
                $"You are Research Worker {i}. Answer your assigned question thoroughly with evidence, " +
                "examples, and data. Be exhaustive — depth is more important than brevity.")
            .Build() as IAgxiomeAgent
    ).ToList();

    // Synthesizer: merges all phase outputs into a final report
    var synthesizer = AgxiomeBuilder.Agent("synthesizer")
        .UseModel(provider, orchModel, apiKey)
        .WithSystemPrompt(
            "You are a senior research analyst. Synthesize multiple research findings into a coherent, " +
            "structured report with: Executive Summary, Key Findings, Analysis, Alternative Perspectives, " +
            "Conclusions, and Recommended Next Steps.")
        .Build();

    return new HeavySwarm(
        new HeavySwarmConfig
        {
            QuestionModel          = orchModel,
            WorkerModel            = wrkModel,
            SynthesisModel         = orchModel,
            EnableParallelResearch = true
        },
        questionAgent,
        workers,
        synthesizer
    );
});

// In-memory job store for long-running research tasks
builder.Services.AddSingleton<ResearchJobStore>();

var app = builder.Build();
await app.UseAgxiomeSecurityDefaultsAsync();

// ── Endpoints ─────────────────────────────────────────────────────────────────

app.MapGet("/health", () => Results.Ok(new
{
    status        = "ok",
    agent         = "MY_AGENT_NAME",
    pattern       = "HeavySwarm",
    workerCount   = cfg.WorkerCount > 0 ? cfg.WorkerCount : 3,
    phases        = new[] { "QuestionGen", "Research", "Analysis", "Alternatives", "Synthesis" },
    utc           = DateTime.UtcNow
}))
    .AllowAnonymousAgxiome();

// Start a research job (async — returns a job ID immediately)
app.MapPost("/research", async (
    ResearchRequest req,
    HeavySwarm swarm,
    ResearchJobStore jobs,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Topic))
        return Results.BadRequest(new { error = "Topic is required." });

    var jobId = Guid.NewGuid().ToString("N")[..12];
    jobs.Start(jobId, req.Topic);

    // Fire and forget — client polls /research/{jobId}
    _ = Task.Run(async () =>
    {
        try
        {
            var result = await swarm.RunAsync(req.Topic);
            jobs.Complete(jobId, result);
        }
        catch (Exception ex)
        {
            jobs.Fail(jobId, ex.Message);
        }
    }, CancellationToken.None);

    return Results.Accepted($"/research/{jobId}", new { jobId, status = "started", topic = req.Topic });
})
    .RequireAgxiomePermission(AgxiomePermission.WorkflowExecute)
    .WithAgxiomeRateLimit(10, TimeSpan.FromMinutes(1));

// Synchronous research (blocks until complete — fine for short topics)
app.MapPost("/research/sync", async (
    ResearchRequest req,
    HeavySwarm swarm,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Topic))
        return Results.BadRequest(new { error = "Topic is required." });

    var result = await swarm.RunAsync(req.Topic, ct);
    return Results.Ok(MapResult(req.Topic, result));
})
    .RequireAgxiomePermission(AgxiomePermission.WorkflowExecute)
    .WithAgxiomeRateLimit(5, TimeSpan.FromMinutes(1));

// Poll job status
app.MapGet("/research/{jobId}", (string jobId, ResearchJobStore jobs) =>
{
    var job = jobs.Get(jobId);
    if (job is null) return Results.NotFound(new { error = $"Job {jobId} not found." });

    if (job.Status == JobStatus.Completed && job.Result is not null)
        return Results.Ok(MapResult(job.Topic, job.Result));

    return Results.Ok(new
    {
        jobId,
        status   = job.Status.ToString(),
        topic    = job.Topic,
        error    = job.Error,
        duration = job.Duration?.ToString(@"mm\:ss")
    });
})
    .RequireAgxiomePermission(AgxiomePermission.WorkflowRead)
    .WithAgxiomeRateLimit(30, TimeSpan.FromMinutes(1));

// List all jobs
app.MapGet("/research", (ResearchJobStore jobs) =>
    Results.Ok(jobs.All().Select(j => new
    {
        j.JobId,
        j.Topic,
        Status   = j.Status.ToString(),
        Duration = j.Duration?.ToString(@"mm\:ss")
    }))
)
    .RequireAgxiomePermission(AgxiomePermission.WorkflowRead)
    .WithAgxiomeRateLimit(30, TimeSpan.FromMinutes(1));

app.Run();

// ── Helpers ───────────────────────────────────────────────────────────────────

static object MapResult(string topic, HeavySwarmResult r) => new
{
    topic,
    question      = r.Question,
    researchCount = r.ResearchFindings.Count,
    research      = r.ResearchFindings,
    analysis      = r.Analysis,
    alternatives  = r.Alternatives,
    synthesis     = r.Synthesis,
    phaseTiming   = r.PhaseTiming.ToDictionary(kv => kv.Key, kv => $"{kv.Value.TotalSeconds:F1}s"),
    totalDuration = r.PhaseTiming.Values.Aggregate(TimeSpan.Zero, (a, b) => a + b).ToString(@"mm\:ss\.fff")
};

public partial class Program { }
