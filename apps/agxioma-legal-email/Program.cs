using AGXIOME.Core;
using AGXIOME.Core.Agent;
using AGXIOME.Core.DependencyInjection;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using AGXIOME.Storage.Embedders;
using AgxiomaLLMWiki;
using AgxiomaLegalEmail.Hubs;
using AgxiomaLegalEmail.Models;
using AgxiomaLegalEmail.Services;
using System.Globalization;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ─────────────────────────────────────────────────────────
var providerName = builder.Configuration["Agent:Provider"] ?? "deepseek";
var configuredModel = builder.Configuration["Agent:Model"];
var (provider, defaultModel, _) = AgxiomeProviderResolver.Resolve(providerName, builder.Configuration);
var model = string.IsNullOrWhiteSpace(configuredModel) ? defaultModel : configuredModel;
var apiKey = AgxiomeProviderResolver.ResolveApiKey(provider, builder.Configuration);

// ── Wiki settings (from AGXIOME library) ─────────────────────────────────
var wikiSettings = new WikiSettings
{
    ProjectRootPath = builder.Configuration["Legal:DataPath"] 
        ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data")),
    SourceExtensions = [".md"],
    SourceExcludes = [],
    LLMProvider = providerName,
    LLMModel = model,
    OllamaBaseUrl = builder.Configuration["Legal:OllamaBaseUrl"] ?? "http://localhost:11434",
    EmbedModel = builder.Configuration["Legal:EmbedModel"] ?? "mxbai-embed-large",
    EmbedDimensions = int.Parse(builder.Configuration["Legal:EmbedDimensions"] ?? "1024"),
    ChunkMaxTokens = 512, ChunkOverlap = 64, EmbedMaxChars = 1200,
    TopK = int.Parse(builder.Configuration["Legal:TopK"] ?? "8"),
    MinScore = double.Parse(builder.Configuration["Legal:MinScore"] ?? "0.25", CultureInfo.InvariantCulture),
    IngestParallelism = 8
};
builder.Services.AddSingleton(wikiSettings);
builder.Services.AddSingleton<IIngestProfileProvider, AppSettingsProfileProvider>();

// ── CORS + SignalR ────────────────────────────────────────────────────────
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins("http://localhost:3000", "http://localhost:3001")
     .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddSignalR();

// ── Swarm registration (ForestSwarm + HeavySwarm via AGXIOME fluent API) ─
builder.Services.AddAgxiome()
    .UseModel(provider, model, apiKey)
    .AddLegalRouterSwarm()      // ForestSwarm "legal-router"
    .AddLegalResearchSwarm();   // HeavySwarm "legal-researcher"

// ── Pipeline orchestrator ─────────────────────────────────────────────────
builder.Services.AddSingleton<PipelineOrchestrator>();

// ── HttpClient for Ollama ─────────────────────────────────────────────────
builder.Services.AddHttpClient("ollama", c => { c.BaseAddress = new Uri(wikiSettings.OllamaBaseUrl); c.Timeout = TimeSpan.FromSeconds(60); });

// ── Ollama Embedder ──────────────────────────────────────────────────────
builder.Services.AddSingleton<OllamaMxbaiEmbedder>(sp =>
{
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("ollama");
    return new OllamaMxbaiEmbedder(http, new OllamaEmbedderOptions { BaseUrl = wikiSettings.OllamaBaseUrl, Model = wikiSettings.EmbedModel });
});

// ── Redis (optional) ─────────────────────────────────────────────────────
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    try { return ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6380,abortConnect=false,connectTimeout=3000"); }
    catch { sp.GetRequiredService<ILoggerFactory>().CreateLogger("Redis").LogWarning("Redis unavailable — caching disabled"); return null!; }
});

// ── PostgreSQL + pgvector ─────────────────────────────────────────────────
var connString = builder.Configuration.GetConnectionString("PostgreSQL") ?? throw new InvalidOperationException("PostgreSQL connection string required.");
builder.Services.AddSingleton<WikiDatabase>(sp => new WikiDatabase(connString, sp.GetService<IConnectionMultiplexer>(), sp.GetRequiredService<ILogger<WikiDatabase>>()));

// ── DeepSeek Agent (for wiki + RAG) ───────────────────────────────────────
builder.Services.AddSingleton<IAgxiomeAgent>(sp =>
    AgxiomeBuilder.Agent("legal-llm")
        .UseDeepSeek(apiKey, model)
        .WithSystemPrompt("You are an expert Italian legal assistant. Answer queries based on the Italian Civil Code and Penal Code. Always cite specific articles (e.g. [[art624-cp]]). Be precise, professional.")
        .WithAutoLoops(upperBound: 2)
        .Build());

// ── Wiki services (from AGXIOME library) ─────────────────────────────────
builder.Services.AddSingleton<WikiPageWriter>();
builder.Services.AddSingleton<WikiIngester>();
builder.Services.AddSingleton<WikiQueryEngine>();
builder.Services.AddSingleton<IWikiIngester>(sp => sp.GetRequiredService<WikiIngester>());
builder.Services.AddSingleton<IWikiQueryEngine>(sp => sp.GetRequiredService<WikiQueryEngine>());

// ── Reranker (Ollama qwen2.5-coder:3b) ───────────────────────────────
builder.Services.AddSingleton<LegalReranker>();
builder.Services.AddSingleton<LegalQueryExpander>();

// ── Startup ingestion ────────────────────────────────────────────────────
builder.Services.AddSingleton<LegalCodeIngestionService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<LegalCodeIngestionService>());

// ── App ──────────────────────────────────────────────────────────────────
var app = builder.Build();
app.UseCors();
app.MapHub<PipelineHub>("/hub/pipeline");

// ── Endpoints ─────────────────────────────────────────────────────────────

app.MapGet("/health", () => Results.Ok(new { status = "ok", provider = providerName, model, utc = DateTime.UtcNow }));

app.MapPost("/api/email/ingest", (IngestEmailRequest req, PipelineOrchestrator orch) =>
{
    if (string.IsNullOrWhiteSpace(req.Sender) || string.IsNullOrWhiteSpace(req.Subject))
        return Results.BadRequest(new { error = "sender and subject required." });
    var id = orch.StartPipelineAsync(req);
    return Results.Created($"/api/email/{id}", new { emailId = id, state = "PIPELINE_STARTED" });
});

app.MapGet("/api/email/{id}/pipeline", (string id, PipelineOrchestrator orch) =>
    Results.Ok(new PipelineStatusResponse(id, orch.GetState(id), orch.GetTimeline(id))));

app.MapGet("/api/email/{id}/draft", (string id, PipelineOrchestrator orch) =>
{
    var draft = orch.GetDraft(id);
    return draft is null ? Results.NotFound(new { error = "No draft yet." }) : Results.Ok(new { draft });
});

app.MapPost("/api/review/approve", (ReviewApproveRequest req, PipelineOrchestrator orch) =>
{
    if (string.IsNullOrWhiteSpace(req.EmailId)) return Results.BadRequest(new { error = "emailId required." });
    orch.Approve(req.EmailId);
    return Results.Ok(new { status = "approved" });
});

app.MapPost("/api/review/reject", (ReviewRejectRequest req, PipelineOrchestrator orch) =>
{
    if (string.IsNullOrWhiteSpace(req.EmailId)) return Results.BadRequest(new { error = "emailId required." });
    orch.Reject(req.EmailId, req.Notes);
    return Results.Ok(new { status = "rejected" });
});

app.MapGet("/api/legal/query", async (string q, IWikiQueryEngine wiki, LegalReranker reranker, LegalQueryExpander expander, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(q)) return Results.BadRequest(new { error = "query 'q' required." });
    var expanded = await expander.ExpandAsync(q, ct);
    var result = await wiki.QueryAsync(new WikiQueryRequest(Question: expanded), ct);
    if (result.ContextFound && result.Sources.Count > 1)
    {
        var reranked = await reranker.RerankAsync(q, result.Sources.ToList(), ct);
        // Return reranked sources with combined scores
        return Results.Ok(new
        {
            result.Question,
            result.Answer,
            result.ContextFound,
            result.LatencyMs,
            Sources = reranked.Select(r => new
            {
                r.Source.Slug,
                r.Source.Title,
                r.Source.Snippet,
                embedScore = r.EmbedScore,
                rerankScore = r.RerankScore,
                score = r.CombinedScore
            }).ToList()
        });
    }
    return Results.Ok(result);
});

app.MapGet("/api/dashboard", (PipelineOrchestrator orch) => Results.Ok(orch.GetDashboard()));
app.MapGet("/api/legal/ingestion-status", (LegalCodeIngestionService svc) => Results.Ok(new { ingested = svc.IsIngested, chunks = svc.TotalChunks }));
app.MapGet("/", () => "AGXIOME Legal Email Demo — POST /api/email/ingest | GET /api/legal/query?q=...");
app.Run();
