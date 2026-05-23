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
builder.Services.AddSingleton<OllamaReranker>(sp =>
{
    var http   = sp.GetRequiredService<IHttpClientFactory>().CreateClient("ollama");
    var model  = builder.Configuration["WikiSettings:RerankerModel"] ?? "llama3.1:8b";
    var logger = sp.GetRequiredService<ILogger<OllamaReranker>>();
    return new OllamaReranker(http, model, logger);
});
builder.Services.AddSingleton<WikiPageWriter>();
builder.Services.AddSingleton<WikiIngester>();
builder.Services.AddSingleton<WikiQueryEngine>(sp => new WikiQueryEngine(
    sp.GetRequiredService<WikiSettings>(),
    sp.GetRequiredService<IIngestProfileProvider>(),
    sp.GetRequiredService<WikiDatabase>(),
    sp.GetRequiredService<WikiPageWriter>(),
    sp.GetRequiredService<OllamaMxbaiEmbedder>(),
    sp.GetRequiredService<IAgxiomeAgent>(),
    sp.GetRequiredService<ILogger<WikiQueryEngine>>(),
    sp.GetRequiredService<OllamaReranker>()));
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

// GET /demo — interactive HTML demo page (no auth required for local dev)
app.MapGet("/demo", () =>
{
    const string html = """
        <!DOCTYPE html>
        <html lang="it">
        <head>
        <meta charset="UTF-8"/>
        <meta name="viewport" content="width=device-width,initial-scale=1"/>
        <title>AGXIOMA LLM Wiki — Demo</title>
        <style>
          body{font-family:system-ui,sans-serif;max-width:820px;margin:40px auto;padding:0 20px;background:#0f0f0f;color:#e8e8e8}
          h1{font-size:1.4rem;color:#a78bfa;margin-bottom:4px}
          .sub{color:#6b7280;font-size:.85rem;margin-bottom:28px}
          textarea{width:100%;height:90px;background:#1a1a2e;border:1px solid #374151;border-radius:8px;
                   color:#e8e8e8;padding:10px;font-size:.95rem;resize:vertical;box-sizing:border-box}
          .row{display:flex;gap:12px;align-items:center;margin:12px 0}
          label{display:flex;align-items:center;gap:8px;cursor:pointer;user-select:none}
          .toggle{position:relative;width:42px;height:24px}
          .toggle input{opacity:0;width:0;height:0}
          .slider{position:absolute;inset:0;background:#374151;border-radius:12px;transition:.2s}
          .slider:before{content:"";position:absolute;width:18px;height:18px;left:3px;top:3px;
                         background:#fff;border-radius:50%;transition:.2s}
          input:checked+.slider{background:#7c3aed}
          input:checked+.slider:before{transform:translateX(18px)}
          .badge{font-size:.72rem;background:#7c3aed22;border:1px solid #7c3aed66;
                 color:#a78bfa;padding:2px 8px;border-radius:20px}
          button{background:#7c3aed;color:#fff;border:none;padding:9px 24px;border-radius:8px;
                 font-size:.95rem;cursor:pointer;transition:.15s}
          button:hover{background:#6d28d9}
          button:disabled{background:#4b5563;cursor:default}
          #result{margin-top:24px}
          .card{background:#1a1a2e;border:1px solid #374151;border-radius:10px;padding:18px;margin-top:10px}
          .answer{white-space:pre-wrap;line-height:1.6}
          .meta{font-size:.78rem;color:#6b7280;margin-top:10px;display:flex;gap:16px;flex-wrap:wrap}
          .meta span{display:flex;align-items:center;gap:4px}
          .reranker-on{color:#34d399}
          .reranker-off{color:#6b7280}
          .sources{margin-top:14px}
          .sources summary{cursor:pointer;color:#a78bfa;font-size:.85rem}
          .src-item{font-size:.82rem;border-top:1px solid #374151;padding:6px 0;display:flex;gap:8px;align-items:baseline}
          .src-slug{font-family:monospace;color:#60a5fa;min-width:160px}
          .src-score{color:#6b7280}
          .src-snip{color:#9ca3af;font-style:italic;overflow:hidden;display:-webkit-box;-webkit-line-clamp:2;-webkit-box-orient:vertical}
          .err{color:#f87171;background:#1a1a2e;border:1px solid #7f1d1d;border-radius:8px;padding:14px;margin-top:10px}
          .spin{display:inline-block;width:16px;height:16px;border:2px solid #7c3aed;
                border-top-color:transparent;border-radius:50%;animation:spin .6s linear infinite;vertical-align:middle;margin-right:6px}
          @keyframes spin{to{transform:rotate(360deg)}}
        </style>
        </head>
        <body>
        <h1>AGXIOMA LLM Wiki</h1>
        <p class="sub">Semantic Q&A over the ingested codebase — powered by pgvector + DeepSeek</p>

        <textarea id="q" placeholder="Scrivi una domanda sul framework…&#10;Es: Come funziona l'AgxiomeBuilder?"></textarea>

        <div class="row">
          <label>
            <span class="toggle">
              <input type="checkbox" id="reranker"/>
              <span class="slider"></span>
            </span>
            Reranker <span class="badge">OllamaReranker</span>
          </label>
          <span id="reranker-hint" style="font-size:.8rem;color:#6b7280">
            Off — ordina per similarità vettoriale
          </span>
        </div>

        <div class="row" style="gap:10px">
          <button id="btn" onclick="query()">Chiedi</button>
          <label style="font-size:.85rem;color:#6b7280">
            TopK <input id="topk" type="number" value="5" min="1" max="20"
              style="width:52px;background:#1a1a2e;border:1px solid #374151;color:#e8e8e8;border-radius:4px;padding:3px 6px;margin-left:4px"/>
          </label>
        </div>

        <div id="result"></div>

        <script>
        document.getElementById('reranker').addEventListener('change', e => {
          document.getElementById('reranker-hint').textContent = e.target.checked
            ? 'On — cross-encoder LLM riordina i risultati (più lento)'
            : 'Off — ordina per similarità vettoriale';
        });

        async function query() {
          const q   = document.getElementById('q').value.trim();
          const btn = document.getElementById('btn');
          const res = document.getElementById('result');
          if (!q) return;

          btn.disabled = true;
          btn.innerHTML = '<span class="spin"></span>Elaborazione…';
          res.innerHTML = '';

          try {
            const body = {
              question:    q,
              topK:        parseInt(document.getElementById('topk').value) || 5,
              useReranker: document.getElementById('reranker').checked
            };
            const r = await fetch('/query', {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify(body)
            });

            if (!r.ok) {
              const err = await r.json().catch(() => ({ error: r.statusText }));
              res.innerHTML = `<div class="err">Errore ${r.status}: ${err.error ?? r.statusText}</div>`;
              return;
            }

            const d = await r.json();
            const rerankerBadge = d.rerankerUsed
              ? '<span class="reranker-on">⟳ Reranker ON</span>'
              : '<span class="reranker-off">Reranker OFF</span>';

            let sourcesHtml = '';
            if (d.sources && d.sources.length) {
              const items = d.sources.map(s =>
                `<div class="src-item">
                  <span class="src-slug">[[${s.slug}]]</span>
                  <span class="src-score">${(s.score*100).toFixed(1)}%</span>
                  <span class="src-snip">${escHtml(s.snippet)}</span>
                </div>`).join('');
              sourcesHtml = `<div class="sources"><details><summary>Fonti (${d.sources.length})</summary>${items}</details></div>`;
            }

            res.innerHTML = `<div class="card">
              <div class="answer">${escHtml(d.answer)}</div>
              <div class="meta">
                <span>⏱ ${d.latencyMs.toFixed(0)} ms</span>
                <span>📄 ${d.sources?.length ?? 0} pagine</span>
                <span>${rerankerBadge}</span>
              </div>
              ${sourcesHtml}
            </div>`;
          } catch(e) {
            res.innerHTML = `<div class="err">Errore di rete: ${e.message}</div>`;
          } finally {
            btn.disabled = false;
            btn.textContent = 'Chiedi';
          }
        }

        function escHtml(s) {
          return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
        }

        document.getElementById('q').addEventListener('keydown', e => {
          if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) query();
        });
        </script>
        </body>
        </html>
        """;
    return Results.Content(html, "text/html");
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
