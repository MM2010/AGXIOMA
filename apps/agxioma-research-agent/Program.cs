using AGXIOME.Core;
using AGXIOME.Core.Patterns;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using AgxiomaResearchAgent;

// ── Bootstrap ─────────────────────────────────────────────────────────────────
var builder = WebApplication.CreateBuilder(args);

var cfg = builder.Configuration.GetSection("Agent").Get<AgentSettings>() ?? new AgentSettings();

// Build the HeavySwarm
builder.Services.AddSingleton<HeavySwarm>(sp =>
{
    var provider  = cfg.Provider switch
    {
        "anthropic" => LLMProviderType.Anthropic,
        "ollama"    => LLMProviderType.Ollama,
        "deepseek"  => LLMProviderType.DeepSeek,
        _           => LLMProviderType.OpenAI
    };
    var orchModel = cfg.OrchestratorModel ?? "gpt-4o";
    var wrkModel  = cfg.WorkerModel       ?? "gpt-4o-mini";

    // Resolve API key for the chosen provider
    var apiKey = provider switch
    {
        LLMProviderType.DeepSeek  => builder.Configuration["LLMProvider:DeepSeek:ApiKey"]
                                     ?? Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY"),
        LLMProviderType.Anthropic => builder.Configuration["LLMProvider:Anthropic:ApiKey"]
                                     ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"),
        _                         => builder.Configuration["LLMProvider:OpenAI:ApiKey"]
                                     ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    };

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

// ── Endpoints ─────────────────────────────────────────────────────────────────

app.MapGet("/health", () => Results.Ok(new
{
    status        = "ok",
    agent         = "MY_AGENT_NAME",
    pattern       = "HeavySwarm",
    workerCount   = cfg.WorkerCount > 0 ? cfg.WorkerCount : 3,
    phases        = new[] { "QuestionGen", "Research", "Analysis", "Alternatives", "Synthesis" },
    utc           = DateTime.UtcNow
}));

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
});

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
});

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
});

// List all jobs
app.MapGet("/research", (ResearchJobStore jobs) =>
    Results.Ok(jobs.All().Select(j => new
    {
        j.JobId,
        j.Topic,
        Status   = j.Status.ToString(),
        Duration = j.Duration?.ToString(@"mm\:ss")
    }))
);

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
