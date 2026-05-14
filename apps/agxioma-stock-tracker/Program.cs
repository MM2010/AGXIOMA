using AGXIOME.Core;
using AGXIOME.Core.DependencyInjection;
using AGXIOME.Core.Patterns;
using AGXIOME.Shared.Models;
using AgxiomaStockTracker;
using AgxiomaStockTracker.Storage;
using AgxiomaStockTracker.Swarms;

// ── Bootstrap ─────────────────────────────────────────────────────────────────
var builder = WebApplication.CreateBuilder(args);

var cfg = builder.Configuration.GetSection("Agent").Get<AgentSettings>() ?? new AgentSettings();

// ── SQLite price store ────────────────────────────────────────────────────────
builder.Services.AddSingleton<PriceStore>(sp =>
{
    var db = sp.GetRequiredService<IConfiguration>()["Agent:DatabasePath"] ?? "stock_tracker.db";
    return new PriceStore(db);
});

// ── Analysis job queue ────────────────────────────────────────────────────────
builder.Services.AddSingleton<AnalysisJobStore>();

// ── Market data client ────────────────────────────────────────────────────────
builder.Services.AddHttpClient<MarketDataClient>();

// ── ForestSwarm — route by asset class (stock / crypto / etf) ────────────────
builder.Services.AddSingleton<ForestSwarm>(_ =>
{
    var (provider, defaultModel, _) = AgxiomeProviderResolver.Resolve(builder.Configuration["Agent:Provider"] ?? "openai", builder.Configuration);
    var model  = cfg.Model ?? defaultModel;
    var apiKey = AgxiomeProviderResolver.ResolveApiKey(provider, builder.Configuration);

    var stockAnalyst = AgxiomeBuilder.Agent("stock-analyst")
        .UseModel(provider, model, apiKey)
        .WithSystemPrompt(
            "You are an expert equity analyst specialising in publicly traded stocks.\n" +
            "When given a ticker, you produce a concise SWOT analysis, P/E and PEG context,\n" +
            "sector comparison, and a 30-day price momentum commentary.\n" +
            "State all caveats clearly. Never give financial advice.")
        .Build();

    var cryptoAnalyst = AgxiomeBuilder.Agent("crypto-analyst")
        .UseModel(provider, model, apiKey)
        .WithSystemPrompt(
            "You are an expert in digital assets and cryptocurrency markets.\n" +
            "When given a ticker (e.g. BTC, ETH), analyse: on-chain metrics, exchange flows,\n" +
            "market cap vs. peers, DeFi/L2 ecosystem, macro crypto sentiment.\n" +
            "Mention regulatory risk. Never give financial advice.")
        .Build();

    var etfAnalyst = AgxiomeBuilder.Agent("etf-analyst")
        .UseModel(provider, model, apiKey)
        .WithSystemPrompt(
            "You are an expert in exchange-traded funds (ETFs).\n" +
            "When given a ticker, analyse: underlying index, expense ratio, tracking error,\n" +
            "holdings concentration, sector/geo exposure, liquidity, and AUM trend.\n" +
            "Compare with 2-3 alternative ETFs. Never give financial advice.")
        .Build();

    var stockTree  = new ForestTree("equity", new[]
    {
        new ForestAgentEntry("stock-analyst",
            "Equity stock analysis",
            new[] { "stock", "equity", "share", "earnings", "revenue", "dividend",
                    "eps", "pe", "ratio", "market", "cap", "nasdaq", "nyse",
                    "msft", "aapl", "googl", "amzn", "nvda", "tsla", "meta" },
            stockAnalyst)
    });

    var cryptoTree = new ForestTree("crypto", new[]
    {
        new ForestAgentEntry("crypto-analyst",
            "Cryptocurrency and digital asset analysis",
            new[] { "crypto", "bitcoin", "ethereum", "btc", "eth", "defi", "nft",
                    "blockchain", "token", "coin", "altcoin", "staking", "yield",
                    "wallet", "exchange", "dex", "sol", "bnb", "xrp", "ada" },
            cryptoAnalyst)
    });

    var etfTree    = new ForestTree("etf", new[]
    {
        new ForestAgentEntry("etf-analyst",
            "ETF / fund analysis",
            new[] { "etf", "fund", "index", "vanguard", "blackrock", "ishares", "spdr",
                    "spy", "qqq", "vti", "arkk", "expense", "tracking", "aum",
                    "diversified", "portfolio", "passive", "active", "bond", "reit" },
            etfAnalyst)
    });

    return new ForestSwarm(
        config: new ForestSwarmConfig { MinMatchScore = 0 },
        trees: new[] { stockTree, cryptoTree, etfTree },
        name: "asset-router",
        description: "Routes by asset class: equity / crypto / ETF"
    );
});

// ── SwarmBuilderService — builds HeavySwarm + GrokSwarm on demand ────────────
builder.Services.AddSingleton<SwarmBuilderService>();

var app = builder.Build();

// Ensure SQLite schema
await app.Services.GetRequiredService<PriceStore>().InitAsync();

// ── Endpoints ─────────────────────────────────────────────────────────────────

app.MapGet("/health", () => Results.Ok(new
{
    status  = "ok",
    pattern = "ForestSwarm + HeavySwarm + GrokSwarm",
    features = new[] { "auto-routing", "deep-research", "debate", "sqlite-history" },
    utc     = DateTime.UtcNow
}));

// ── GET /quote/{ticker} — live quote + persist ────────────────────────────────
app.MapGet("/quote/{ticker}", async (
    string ticker,
    MarketDataClient market,
    PriceStore store,
    CancellationToken ct) =>
{
    ticker = ticker.ToUpperInvariant().Trim();
    var quote = await market.GetQuoteAsync(ticker, ct);
    if (quote == null) return Results.NotFound(new { error = $"Ticker '{ticker}' not found." });

    await store.SavePriceAsync(ticker, quote.Price, quote.Change, quote.ChangePercent, ct);

    return Results.Ok(new
    {
        ticker,
        quote.Price,
        quote.Change,
        quote.ChangePercent,
        quote.Volume,
        quote.MarketCap,
        quote.Source,
        savedAt = DateTime.UtcNow
    });
});

// ── GET /history/{ticker} — SQLite price history ─────────────────────────────
app.MapGet("/history/{ticker}", async (
    string ticker,
    PriceStore store,
    int? days,
    CancellationToken ct) =>
{
    ticker = ticker.ToUpperInvariant().Trim();
    var rows = await store.GetHistoryAsync(ticker, days ?? 30, ct);
    return Results.Ok(new { ticker, rows });
});

// ── POST /analyze/{ticker} — ForestSwarm routing + HeavySwarm deep research ──
app.MapPost("/analyze/{ticker}", async (
    string ticker,
    ForestSwarm router,
    SwarmBuilderService swarms,
    MarketDataClient market,
    PriceStore store,
    AnalysisJobStore jobs,
    CancellationToken ct) =>
{
    ticker = ticker.ToUpperInvariant().Trim();

    // Start async job
    var jobId = Guid.NewGuid().ToString("N")[..8];
    jobs.Start(jobId, ticker, "deep-analysis");

    _ = Task.Run(async () =>
    {
        try
        {
            // 1. Get recent price data for context
            var history = await store.GetHistoryAsync(ticker, 7, CancellationToken.None);
            var priceCtx = history.Count > 0
                ? $"Recent 7-day prices: {string.Join(", ", history.TakeLast(7).Select(h => $"{h.Date:MMM d}: ${h.Price:F2}"))}"
                : "No local price history available.";

            // 2. Route to correct specialist via ForestSwarm
            var routeTask = $"{ticker} analysis request";
            var route     = await router.RunAsync(routeTask, CancellationToken.None);

            // 3. Deep analysis via HeavySwarm
            var heavy   = swarms.BuildHeavySwarm();
            var input   = $"Provide a comprehensive investment analysis for {ticker}.\n{priceCtx}\nFocus: {route.SelectedTree} specialist view.";
            var research = await heavy.RunAsync(input, CancellationToken.None);

            jobs.Complete(jobId, new AnalysisResult(
                Ticker:        ticker,
                AssetClass:    route.SelectedTree,
                RoutedAgent:   route.SelectedAgent,
                Questions:     string.IsNullOrEmpty(research.Question) ? new List<string>() : new List<string> { research.Question },
                Findings:      research.ResearchFindings.ToList(),
                Analysis:      research.Analysis,
                Alternatives:  research.Alternatives.ToList(),
                Synthesis:     research.Synthesis,
                Timing:        research.PhaseTiming
            ));
        }
        catch (Exception ex)
        {
            jobs.Fail(jobId, ex.Message);
        }
    }, CancellationToken.None);

    return Results.Accepted($"/analyze/{ticker}/jobs/{jobId}",
        new { jobId, ticker, status = "processing" });
});

// ── GET /analyze/{ticker}/jobs/{jobId} — poll job ────────────────────────────
app.MapGet("/analyze/{ticker}/jobs/{jobId}", (
    string ticker,
    string jobId,
    AnalysisJobStore jobs) =>
{
    var job = jobs.Get(jobId);
    return job == null
        ? Results.NotFound(new { error = "Job not found." })
        : Results.Ok(job);
});

// ── POST /debate/{ticker} — GrokSwarm buy/hold/sell debate ───────────────────
app.MapPost("/debate/{ticker}", async (
    string ticker,
    SwarmBuilderService swarms,
    MarketDataClient market,
    PriceStore store,
    CancellationToken ct) =>
{
    ticker = ticker.ToUpperInvariant().Trim();

    var history = await store.GetHistoryAsync(ticker, 14, ct);
    var priceCtx = history.Count > 0
        ? $"14-day price context: {string.Join(", ", history.TakeLast(10).Select(h => $"{h.Date:MMM d}=${h.Price:F2}"))}"
        : string.Empty;

    var grok   = swarms.BuildGrokSwarm();
    var debate = $"For the asset {ticker}: should an investor BUY, HOLD, or SELL right now?\n{priceCtx}";

    var result = await grok.RunAsync(debate, ct);

    return Results.Ok(new
    {
        ticker,
        harperFindings   = result.HarperOutput,
        benjaminFindings = result.BenjaminOutput,
        lucasView        = result.LucasOutput,
        verdict          = result.CaptainSynthesis,
        timing           = result.PhaseTiming
    });
});

// ── GET /watchlist — list all tickers in SQLite ───────────────────────────────
app.MapGet("/watchlist", async (PriceStore store, CancellationToken ct) =>
{
    var tickers = await store.GetAllTickersAsync(ct);
    return Results.Ok(new { tickers });
});

app.Run();
