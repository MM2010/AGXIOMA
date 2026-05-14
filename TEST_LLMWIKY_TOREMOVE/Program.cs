using AGXIOME.Core;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Storage.Embedders;
using AgxiomaLLMWiki;
using StackExchange.Redis;

// ── Bootstrap ─────────────────────────────────────────────────────────────────
var builder = WebApplication.CreateBuilder(args);

// Settings
var wikiSettings = builder.Configuration.GetSection("Wiki").Get<WikiSettings>()
    ?? throw new InvalidOperationException("Wiki configuration section is missing.");
builder.Services.AddSingleton(wikiSettings);

// IngestProfile provider — default: reads from appsettings.json via WikiSettings.
// Swap this registration to load profiles from a DB, API, env vars, etc.
builder.Services.AddSingleton<IIngestProfileProvider, AppSettingsProfileProvider>();

// HttpClient for Ollama embedder
builder.Services.AddHttpClient("ollama", c =>
{
    c.BaseAddress = new Uri(wikiSettings.OllamaBaseUrl);
    c.Timeout     = TimeSpan.FromSeconds(60);
});

// OllamaMxbaiEmbedder
builder.Services.AddSingleton<OllamaMxbaiEmbedder>(sp =>
{
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("ollama");
    return new OllamaMxbaiEmbedder(http, new OllamaEmbedderOptions
    {
        BaseUrl = wikiSettings.OllamaBaseUrl,
        Model   = wikiSettings.EmbedModel
    });
});

// Redis (graceful degradation if unavailable)
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    try
    {
        return ConnectionMultiplexer.Connect(
            builder.Configuration.GetConnectionString("Redis")
            ?? "localhost:6379,abortConnect=false,connectTimeout=3000");
    }
    catch (Exception ex)
    {
        sp.GetRequiredService<ILoggerFactory>()
          .CreateLogger("Redis")
          .LogWarning("Redis unavailable — caching disabled: {Error}", ex.Message);
        return null!;
    }
});

// PostgreSQL + Redis data layer
var connString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Postgres connection string missing.");
builder.Services.AddSingleton<WikiDatabase>(sp => new WikiDatabase(
    connString,
    sp.GetService<IConnectionMultiplexer>(),
    sp.GetRequiredService<ILogger<WikiDatabase>>()));

// DeepSeek agent
builder.Services.AddSingleton<IAgxiomeAgent>(sp =>
{
    var apiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY")
        ?? throw new InvalidOperationException("DEEPSEEK_API_KEY environment variable not set.");
    return AgxiomeBuilder.Agent("llmwiki")
        .UseDeepSeek(apiKey, wikiSettings.LLMModel)
        .WithSystemPrompt("""
            You are an expert technical writer for the AGXIOMA .NET agent framework.
            You generate clear, concise, accurate wiki pages from source code and documentation.
            Always follow the specified frontmatter and structure format exactly.
            """)
        .WithAutoLoops(upperBound: 1)
        .Build();
});

// Wiki services
builder.Services.AddSingleton<WikiPageWriter>();
builder.Services.AddSingleton<WikiIngester>();
builder.Services.AddSingleton<WikiQueryEngine>();
builder.Services.AddSingleton<WikiLintEngine>();
builder.Services.AddLogging(c => c.AddConsole());

// ── App ───────────────────────────────────────────────────────────────────────
var app = builder.Build();
app.UseDeveloperExceptionPage();

// ── Startup ───────────────────────────────────────────────────────────────────
var db          = app.Services.GetRequiredService<WikiDatabase>();
var settings    = app.Services.GetRequiredService<WikiSettings>();
var startLogger = app.Services.GetRequiredService<ILogger<Program>>();

try
{
    await db.EnsureSchemaAsync();
    startLogger.LogInformation("[Start] PostgreSQL schema ready.");
}
catch (Exception ex)
{
    startLogger.LogError("[Start] Schema migration failed: {Error}", ex.Message);
    throw;
}

Directory.CreateDirectory(settings.WikiOutputPath);
Directory.CreateDirectory(Path.Combine(settings.WikiOutputPath, "entities"));
Directory.CreateDirectory(Path.Combine(settings.WikiOutputPath, "concepts"));
Directory.CreateDirectory(Path.Combine(settings.WikiOutputPath, "sources"));
startLogger.LogInformation("[Start] Wiki output dir: {Dir}", settings.WikiOutputPath);

// ── Endpoints ─────────────────────────────────────────────────────────────────

// GET /health
app.MapGet("/health", async (WikiDatabase wdb) =>
{
    var pages   = await wdb.CountPagesAsync();
    var sources = await wdb.CountSourcesAsync();
    var logs    = await wdb.CountLogEntriesAsync();
    return Results.Ok(new
    {
        status    = "ok",
        pages,
        sources,
        logs,
        framework = "net10.0",
        timestamp = DateTime.UtcNow
    });
});

// POST /ingest
app.MapPost("/ingest", async (
    IngestJobRequest req,
    WikiDatabase     wdb,
    WikiIngester     ingester,
    ILogger<Program> log) =>
{
    var jobId = Guid.NewGuid().ToString("N")[..12];
    var state = new IngestJobState { JobId = jobId, Status = "running" };
    await wdb.SaveJobStateAsync(state);

    _ = Task.Run(async () =>
    {
        try
        {
            await ingester.RunAsync(state, req.SubPath, req.Force, CancellationToken.None);
            state.Status = "done";
        }
        catch (Exception ex)
        {
            log.LogError("[Ingest] Job {Id} failed: {Error}", jobId, ex.Message);
            state.Status       = "error";
            state.ErrorMessage = ex.Message;
        }
        finally
        {
            await wdb.SaveJobStateAsync(state);
        }
    });

    return Results.Accepted($"/ingest/{jobId}",
        new IngestJobResponse(jobId, "running", 0, "Ingest started in background."));
});

// GET /ingest/{jobId}
app.MapGet("/ingest/{jobId}", async (string jobId, WikiDatabase wdb) =>
{
    var state = await wdb.GetJobStateAsync(jobId);
    if (state is null) return Results.NotFound(new { error = $"Job '{jobId}' not found." });
    return Results.Ok(new IngestProgressResponse(
        JobId:          state.JobId,
        Status:         state.Status,
        FilesTotal:     state.FilesTotal,
        FilesProcessed: state.FilesProcessed,
        PagesCreated:   state.PagesCreated,
        PagesUpdated:   state.PagesUpdated,
        CurrentFile:    state.CurrentFile,
        ErrorMessage:   state.ErrorMessage));
});

// GET /wiki
app.MapGet("/wiki", async (WikiDatabase wdb, string? category) =>
{
    var pages = await wdb.ListPagesAsync(category);
    return Results.Ok(new WikiPageListResponse(
        Total: pages.Count,
        Pages: pages.Select(p => new WikiPageSummary(
            Slug:        p.Slug,
            Title:       p.Title,
            Category:    p.Category,
            Summary:     p.Summary.Length > 200 ? p.Summary[..200] : p.Summary,
            SourceCount: p.SourceCount,
            UpdatedAt:   p.UpdatedAt)).ToList()));
});

// GET /wiki/{**slug}
app.MapGet("/wiki/{**slug}", async (string slug, WikiDatabase wdb, WikiPageWriter pageWriter) =>
{
    var pageRecord = await wdb.GetPageBySlugAsync(slug);
    if (pageRecord is null) return Results.NotFound(new { error = $"Page '{slug}' not found." });

    var cached = await wdb.GetCachedPageContentAsync(slug);
    string content;
    if (cached is not null)
    {
        content = cached;
    }
    else
    {
        var disk = await pageWriter.ReadWikiPageAsync(slug);
        if (disk is null) return Results.NotFound(new { error = $"Page '{slug}' file not found on disk." });
        content = disk;
        await wdb.CachePageContentAsync(slug, content);
    }

    return Results.Ok(new WikiPageContentResponse(
        Slug:      pageRecord.Slug,
        Title:     pageRecord.Title,
        Category:  pageRecord.Category,
        Content:   content,
        UpdatedAt: pageRecord.UpdatedAt));
});

// POST /query
app.MapPost("/query", async (WikiQueryRequest req, WikiQueryEngine queryEngine) =>
{
    if (string.IsNullOrWhiteSpace(req.Question))
        return Results.BadRequest(new { error = "Question is required." });
    var result = await queryEngine.QueryAsync(req);
    return Results.Ok(result);
});

// POST /lint
app.MapPost("/lint", async (WikiLintEngine lintEngine) =>
{
    var report = await lintEngine.RunLintAsync();
    return Results.Ok(report);
});

// GET /log
app.MapGet("/log", async (WikiDatabase wdb, int? limit) =>
{
    var entries = await wdb.GetLogAsync(limit ?? 50);
    return Results.Ok(new { total = entries.Count, entries });
});

// POST /admin/reset-embeddings — drops and recreates wiki_embeddings with correct dimension
app.MapPost("/admin/reset-embeddings", async (WikiDatabase wdb, ILogger<Program> log) =>
{
    await wdb.EnsureSchemaAsync();
    log.LogInformation("[Admin] wiki_embeddings reset complete.");
    return Results.Ok(new { message = "wiki_embeddings table reset. Run /ingest to repopulate." });
});

// GET /stats
app.MapGet("/stats", async (WikiDatabase wdb) =>
{
    var pages      = await wdb.CountPagesAsync();
    var sources    = await wdb.CountSourcesAsync();
    var logs       = await wdb.CountLogEntriesAsync();
    var lastIngest = await wdb.GetLastIngestAtAsync() ?? DateTime.MinValue;
    var lastQuery  = await wdb.GetLastQueryAtAsync()  ?? DateTime.MinValue;
    return Results.Ok(new WikiStatsResponse(
        TotalSources:    sources,
        TotalPages:      pages,
        TotalLogEntries: logs,
        TotalChunks:     0,
        LastIngestAt:    lastIngest,
        LastQueryAt:     lastQuery));
});

// ── Start ─────────────────────────────────────────────────────────────────────
app.Urls.Add("http://localhost:5009");
app.Run();
