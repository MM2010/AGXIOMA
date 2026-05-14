using AGXIOME.Core;
using AGXIOME.Core.DependencyInjection;
using AGXIOME.Core.Patterns;
using AGXIOME.Shared.Models;
using AgxiomaCodeReviewer;

// ── Bootstrap ─────────────────────────────────────────────────────────────────
var builder = WebApplication.CreateBuilder(args);

var cfg = builder.Configuration.GetSection("Agent").Get<AgentSettings>() ?? new AgentSettings();

builder.Services.AddSingleton<GrokSwarm>(_ =>
{
    var (provider, defaultModel, _) = AgxiomeProviderResolver.Resolve(cfg.Provider, builder.Configuration);
    var model  = cfg.Model ?? defaultModel;
    var apiKey = AgxiomeProviderResolver.ResolveApiKey(provider, builder.Configuration);

    // Captain: decomposes the code review task, then synthesizes the final report
    var captain = AgxiomeBuilder.Agent("captain")
        .UseModel(provider, model, apiKey)
        .WithSystemPrompt(
            "You are the Lead Code Review Architect. Your job is to:\n" +
            "1. Decompose code submissions into focused review questions for your specialists.\n" +
            "2. Synthesize their findings into a structured, actionable review report.\n\n" +
            "Report format:\n" +
            "## Summary\n## Bugs & Security (from Harper)\n## Performance & Logic (from Benjamin)\n" +
            "## Architecture & Alternatives (from Lucas)\n## Overall Score (1-10)\n## Action Items (sorted by priority)")
        .Build();

    // Harper: bug hunter + security reviewer
    var harper = AgxiomeBuilder.Agent("harper")
        .UseModel(provider, model, apiKey)
        .WithSystemPrompt(
            "You are Harper, a specialist in bug detection and security vulnerabilities.\n" +
            "Focus ONLY on:\n" +
            "- Null pointer / NullReferenceException risks\n" +
            "- Off-by-one errors, boundary conditions\n" +
            "- SQL injection, XSS, path traversal, auth bypass (OWASP Top 10)\n" +
            "- Race conditions, thread safety issues\n" +
            "- Unhandled exceptions and error paths\n" +
            "For each issue: state LOCATION, SEVERITY (Critical/High/Medium/Low), and RECOMMENDATION.")
        .Build();

    // Benjamin: performance + logic correctness
    var benjamin = AgxiomeBuilder.Agent("benjamin")
        .UseModel(provider, model, apiKey)
        .WithSystemPrompt(
            "You are Benjamin, a specialist in performance optimisation and logic correctness.\n" +
            "Focus ONLY on:\n" +
            "- Algorithm complexity (O(n²) where O(n log n) exists, etc.)\n" +
            "- Memory allocations, unnecessary copies, boxing/unboxing\n" +
            "- N+1 query problems, missing indexes, inefficient DB access\n" +
            "- Logic errors: wrong conditions, incorrect calculations, missed edge cases\n" +
            "- Async/await misuse (sync-over-async, blocking calls, CancellationToken ignoring)\n" +
            "For each issue: state LOCATION, IMPACT (High/Medium/Low), and OPTIMISED VERSION.")
        .Build();

    // Lucas: creative contrarian + architecture advisor
    var lucas = AgxiomeBuilder.Agent("lucas")
        .UseModel(provider, model, apiKey)
        .WithSystemPrompt(
            "You are Lucas, a contrarian architectural advisor.\n" +
            "Focus ONLY on:\n" +
            "- Design patterns that could simplify the code (Strategy, Factory, Builder, etc.)\n" +
            "- SOLID principle violations\n" +
            "- Over-engineering or under-engineering\n" +
            "- Better library/framework alternatives\n" +
            "- Testability improvements\n" +
            "- Code that will be hard to maintain in 6 months\n" +
            "For each suggestion: state WHY the current approach is problematic and show the ALTERNATIVE.")
        .Build();

    return SwarmFactory.GrokSwarm(captain, harper, benjamin, lucas, maxLoops: cfg.MaxLoops);
});

var app = builder.Build();

// ── Endpoints ─────────────────────────────────────────────────────────────────

app.MapGet("/health", () => Results.Ok(new
{
    status      = "ok",
    agent       = "MY_AGENT_NAME",
    pattern     = "GrokSwarm",
    specialists = new[] { "Captain (orchestration)", "Harper (bugs+security)", "Benjamin (perf+logic)", "Lucas (architecture)" },
    utc         = DateTime.UtcNow
}));

// Review code snippet from request body
app.MapPost("/review", async (
    ReviewRequest req,
    GrokSwarm swarm,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Code))
        return Results.BadRequest(new { error = "Code is required." });

    var task =
        $"""
        Review the following {req.Language ?? "code"} snippet.
        Context: {req.Context ?? "No additional context provided."}

        ```{(req.Language ?? "").ToLower()}
        {req.Code}
        ```
        """;

    var result = await swarm.RunAsync(task, ct);

    return Results.Ok(new ReviewResponse(
        Language:        req.Language ?? "unknown",
        Context:         req.Context,
        HarperFindings:  result.HarperOutput,
        BenjaminFindings:result.BenjaminOutput,
        LucasFindings:   result.LucasOutput,
        FinalReport:     result.CaptainSynthesis,
        PhaseTiming:     result.PhaseTiming.ToDictionary(kv => kv.Key, kv => $"{kv.Value.TotalSeconds:F1}s")
    ));
});

// Review a local file
app.MapPost("/review/file", async (
    ReviewFileRequest req,
    GrokSwarm swarm,
    CancellationToken ct) =>
{
    if (!File.Exists(req.FilePath))
        return Results.NotFound(new { error = $"File not found: {req.FilePath}" });

    var ext  = Path.GetExtension(req.FilePath).TrimStart('.').ToLower();
    var code = await File.ReadAllTextAsync(req.FilePath, ct);

    if (code.Length > 20_000)
        return Results.BadRequest(new { error = "File too large. Max 20,000 characters for code review." });

    var task =
        $"""
        Review the following {ext} file: {Path.GetFileName(req.FilePath)}
        Context: {req.Context ?? "Production code — be thorough."}

        ```{ext}
        {code}
        ```
        """;

    var result = await swarm.RunAsync(task, ct);

    return Results.Ok(new ReviewResponse(
        Language:        ext,
        Context:         req.Context ?? req.FilePath,
        HarperFindings:  result.HarperOutput,
        BenjaminFindings:result.BenjaminOutput,
        LucasFindings:   result.LucasOutput,
        FinalReport:     result.CaptainSynthesis,
        PhaseTiming:     result.PhaseTiming.ToDictionary(kv => kv.Key, kv => $"{kv.Value.TotalSeconds:F1}s")
    ));
});

// Review a diff / git patch
app.MapPost("/review/diff", async (
    DiffReviewRequest req,
    GrokSwarm swarm,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Diff))
        return Results.BadRequest(new { error = "Diff is required." });

    var task =
        $"""
        Review this git diff for a pull request.
        PR Title: {req.PrTitle ?? "Untitled PR"}
        Description: {req.Description ?? "No description."}

        ```diff
        {req.Diff}
        ```
        """;

    var result = await swarm.RunAsync(task, ct);

    return Results.Ok(new ReviewResponse(
        Language:        "diff",
        Context:         req.PrTitle,
        HarperFindings:  result.HarperOutput,
        BenjaminFindings:result.BenjaminOutput,
        LucasFindings:   result.LucasOutput,
        FinalReport:     result.CaptainSynthesis,
        PhaseTiming:     result.PhaseTiming.ToDictionary(kv => kv.Key, kv => $"{kv.Value.TotalSeconds:F1}s")
    ));
});

app.Run();
