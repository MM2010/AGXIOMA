using AGXIOME.Core;
using AGXIOME.Runtime;
using AGXIOME.Security.Rbac;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Storage.Embedders;
using AgxiomaLLMWiki;
using StackExchange.Redis;

// ── Bootstrap ─────────────────────────────────────────────────────────────────
var builder = WebApplication.CreateBuilder(args);
builder.AddAgxiomeSecurityDefaults();

// Settings
var wikiSettings = BindWikiSettings(builder.Configuration);
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
builder.Services.AddSingleton<IWikiDatabase>(sp => sp.GetRequiredService<WikiDatabase>());

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
builder.Services.AddSingleton<IWikiPageWriter>(sp => sp.GetRequiredService<WikiPageWriter>());
builder.Services.AddSingleton<IWikiIngester>(sp => sp.GetRequiredService<WikiIngester>());
builder.Services.AddSingleton<IWikiQueryEngine>(sp => sp.GetRequiredService<WikiQueryEngine>());
builder.Services.AddSingleton<IWikiLintEngine>(sp => sp.GetRequiredService<WikiLintEngine>());
builder.Services.AddLogging(c => c.AddConsole());

// ── App ───────────────────────────────────────────────────────────────────────
var app = builder.Build();

// Detailed exception pages are only safe in local development.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

await app.UseAgxiomeSecurityDefaultsAsync();

// ── Startup ───────────────────────────────────────────────────────────────────
var db          = app.Services.GetRequiredService<IWikiDatabase>();
var settings    = app.Services.GetRequiredService<WikiSettings>();
var startLogger = app.Services.GetRequiredService<ILogger<WikiApplication>>();

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
app.MapGet("/health", async (IWikiDatabase wdb) =>
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
})
    .AllowAnonymousAgxiome();

// POST /ingest
app.MapPost("/ingest", async (
    IngestJobRequest req,
    IWikiDatabase    wdb,
    IWikiIngester    ingester,
    ILogger<WikiApplication> log) =>
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
})
    .RequireAgxiomePermission(AgxiomePermission.KnowledgeIngest)
    .WithAgxiomeRateLimit(10, TimeSpan.FromMinutes(1));

// GET /ingest/{jobId}
app.MapGet("/ingest/{jobId}", async (string jobId, IWikiDatabase wdb) =>
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
})
    .RequireAgxiomePermission(AgxiomePermission.WorkflowRead)
    .WithAgxiomeRateLimit(30, TimeSpan.FromMinutes(1));

// GET /wiki
app.MapGet("/wiki", async (IWikiDatabase wdb, string? category) =>
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
})
    .AllowAnonymousAgxiome();

// GET /wiki/{**slug}
app.MapGet("/wiki/{**slug}", async (string slug, IWikiDatabase wdb, IWikiPageWriter pageWriter) =>
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
})
    .AllowAnonymousAgxiome();

// POST /query
app.MapPost("/query", async (WikiQueryRequest req, IWikiQueryEngine queryEngine) =>
{
    if (string.IsNullOrWhiteSpace(req.Question))
        return Results.BadRequest(new { error = "Question is required." });
    var result = await queryEngine.QueryAsync(req);
    return Results.Ok(result);
})
    .RequireAgxiomePermission(AgxiomePermission.KnowledgeSearch)
    .WithAgxiomeRateLimit(20, TimeSpan.FromMinutes(1));

// POST /lint
app.MapPost("/lint", async (IWikiLintEngine lintEngine) =>
{
    var report = await lintEngine.RunLintAsync();
    return Results.Ok(report);
})
    .RequireAgxiomePermission(AgxiomePermission.AdminConfig)
    .WithAgxiomeRateLimit(10, TimeSpan.FromMinutes(1));

// GET /log
app.MapGet("/log", async (IWikiDatabase wdb, int? limit) =>
{
    var entries = await wdb.GetLogAsync(limit ?? 50);
    return Results.Ok(new { total = entries.Count, entries });
})
    .RequireAgxiomePermission(AgxiomePermission.AdminLogs)
    .WithAgxiomeRateLimit(20, TimeSpan.FromMinutes(1));

// POST /admin/reset-embeddings — drops and recreates wiki_embeddings with correct dimension
app.MapPost("/admin/reset-embeddings", async (IWikiDatabase wdb, ILogger<WikiApplication> log) =>
{
    await wdb.EnsureSchemaAsync();
    log.LogInformation("[Admin] wiki_embeddings reset complete.");
    return Results.Ok(new { message = "wiki_embeddings table reset. Run /ingest to repopulate." });
})
    .RequireAgxiomePermission(AgxiomePermission.AdminConfig)
    .WithAgxiomeRateLimit(5, TimeSpan.FromMinutes(1));

// GET /stats
app.MapGet("/stats", async (IWikiDatabase wdb) =>
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
})
    .RequireAgxiomePermission(AgxiomePermission.AdminMetrics)
    .WithAgxiomeRateLimit(20, TimeSpan.FromMinutes(1));

// ── Start ─────────────────────────────────────────────────────────────────────
app.Urls.Add("http://localhost:5009");
app.Run();

static WikiSettings BindWikiSettings(IConfiguration configuration)
{
    var section = configuration.GetSection("Wiki");
    if (!section.Exists())
        throw new InvalidOperationException("Wiki configuration section is missing.");

    return new WikiSettings
    {
        ProjectRootPath = ReadPathOrDefault(section, "ProjectRootPath", "."),
        WikiOutputPath = ReadPathOrDefault(section, "WikiOutputPath", "wiki"),
        LLMModel = section["LLMModel"] ?? "deepseek-v4-flash",
        LLMProvider = section["LLMProvider"] ?? "deepseek",
        OllamaBaseUrl = section["OllamaBaseUrl"] ?? "http://localhost:11434/",
        EmbedModel = section["EmbedModel"] ?? "mxbai-embed-large",
        EmbedDimensions = ReadInt(section, "EmbedDimensions", 1024),
        ChunkMaxTokens = ReadInt(section, "ChunkMaxTokens", 512),
        ChunkOverlap = ReadInt(section, "ChunkOverlap", 64),
        EmbedMaxChars = ReadInt(section, "EmbedMaxChars", 1200),
        TopK = ReadInt(section, "TopK", 8),
        MinScore = ReadDouble(section, "MinScore", 0.45),
        SourceExtensions = section.GetSection("SourceExtensions").Get<string[]>()
            ?? [".cs", ".md", ".json", ".csproj", ".props", ".txt", ".yml", ".yaml"],
        SourceExcludes = section.GetSection("SourceExcludes").Get<string[]>()
            ?? ["bin", "obj", "nupkgs", "packages", ".git", "wiki"],
        IngestParallelism = ReadInt(section, "IngestParallelism", 4)
    };
}

static int ReadInt(IConfiguration section, string key, int fallback) =>
    int.TryParse(section[key], out var value) ? value : fallback;

static double ReadDouble(IConfiguration section, string key, double fallback) =>
    double.TryParse(section[key], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value)
        ? value
        : fallback;

static string ReadPathOrDefault(IConfiguration section, string key, string fallback)
{
    var value = section[key];
    if (string.IsNullOrWhiteSpace(value))
        return fallback;

    return value.IndexOfAny(['<', '>']) >= 0
        ? fallback
        : value;
}

file sealed class WikiApplication;

public partial class Program { }
