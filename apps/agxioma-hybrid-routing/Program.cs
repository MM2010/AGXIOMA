using AGXIOME.Core;
using AGXIOME.Shared.Models;
using AgxiomaHybridRouting.AgentServices;
using AgxiomaHybridRouting.Routing;
using Microsoft.Extensions.Logging;

// ─────────────────────────────────────────────────────────────────────────────
// T9 — Hybrid Semantic Routing Demo
// Port: 5008
// Routing: keyword (0.25) + cosine-semantic (0.65) + priority (0.10)
//          + deterministic reranking rules
// Fallback: keyword-only when Ollama mxbai-embed-large is unavailable
// ─────────────────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

var agentCfg  = builder.Configuration.GetSection("Agent").Get<AgentSettings>()        ?? new AgentSettings();
var routingCfg = builder.Configuration.GetSection("HybridRouting");

// ── Resolve provider ──────────────────────────────────────────────────────────
var provider = agentCfg.Provider switch
{
    "anthropic" => LLMProviderType.Anthropic,
    "ollama"    => LLMProviderType.Ollama,
    "deepseek"  => LLMProviderType.DeepSeek,
    _           => LLMProviderType.OpenAI
};
var model = agentCfg.Model ?? "gpt-4o";

var apiKey = provider switch
{
    LLMProviderType.DeepSeek  => builder.Configuration["LLMProvider:DeepSeek:ApiKey"]
                                 ?? Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY"),
    LLMProviderType.Anthropic => builder.Configuration["LLMProvider:Anthropic:ApiKey"]
                                 ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"),
    _                         => builder.Configuration["LLMProvider:OpenAI:ApiKey"]
                                 ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
};

// ── Build specialist agents (identical prompts to T6 for consistency) ─────────

var billingAgent = AgxiomeBuilder.Agent("billing-specialist")
    .UseModel(provider, model, apiKey)
    .WithSystemPrompt(
        "You are a Billing Specialist. Help customers with:\n" +
        "- Invoice questions and payment methods\n" +
        "- Subscription upgrades/downgrades/cancellations\n" +
        "- Refunds, billing errors, and proration\n" +
        "- Tax certificates and receipts\n" +
        "Always confirm the account holder's information before discussing billing details.")
    .Build();

var technicalAgent = AgxiomeBuilder.Agent("technical-specialist")
    .UseModel(provider, model, apiKey)
    .WithSystemPrompt(
        "You are a Technical Support Specialist. Help customers with:\n" +
        "- Software installation, configuration, and updates\n" +
        "- API integration errors and authentication issues\n" +
        "- Performance problems and debugging\n" +
        "- Bug reports (collect steps to reproduce, environment, version)\n" +
        "Escalate to engineering if the issue cannot be resolved in this session.")
    .Build();

var salesAgent = AgxiomeBuilder.Agent("sales-specialist")
    .UseModel(provider, model, apiKey)
    .WithSystemPrompt(
        "You are a Sales Specialist. Help prospects and customers with:\n" +
        "- Product features and comparisons\n" +
        "- Pricing, plans, and enterprise contracts\n" +
        "- Trial extensions and proof-of-concept requests\n" +
        "- Partner and reseller enquiries\n" +
        "Be consultative — understand the customer's use case before recommending a plan.")
    .Build();

var generalAgent = AgxiomeBuilder.Agent("general-support")
    .UseModel(provider, model, apiKey)
    .WithSystemPrompt(
        "You are a General Support Agent. Handle any request that doesn't clearly belong to billing, " +
        "technical, or sales teams. Provide helpful answers, and if you identify this is a specialised " +
        "topic, let the customer know you can connect them with the right team.")
    .Build();

// ── Agent entries with hybrid routing metadata ────────────────────────────────

var agentEntries = new List<HybridAgentEntry>
{
    new(
        TreeName    : "billing",
        AgentName   : "billing-specialist",
        Description : "Invoice, payment, subscription, refund, billing, pricing, tax",
        Keywords    : new[]
        {
            "invoice", "payment", "bill", "billing", "charge", "charged",
            "subscription", "refund", "cancel", "cancellation", "receipt", "tax",
            "vat", "proration", "coupon", "discount", "credit", "downgrade",
            "bank transfer", "wire", "annual billing", "billing address",
            "credit card", "card declined", "overpaid", "billed"
        },
        Agent    : billingAgent,
        Priority : 0.0),

    new(
        TreeName    : "technical",
        AgentName   : "technical-specialist",
        Description : "API errors, installation, bugs, performance, authentication, configuration, webhook, SSL",
        Keywords    : new[]
        {
            "error", "bug", "crash", "api", "install", "installation",
            "configuration", "config", "authentication", "auth", "token",
            "oauth", "performance", "slow", "timeout", "exception", "stack trace",
            "debug", "version", "update", "connection", "endpoint", "ssl", "certificate",
            "cors", "deploy", "deployment", "webhook", "http", "https",
            "log", "logs", "401", "403", "500", "forbidden", "unauthorized",
            "rate limit", "websocket", "null pointer", "dependency", "build",
            "integration", "connect", "refused"
        },
        Agent    : technicalAgent,
        Priority : 0.0),

    new(
        TreeName    : "sales",
        AgentName   : "sales-specialist",
        Description : "Plans, features, enterprise, trial, comparison, partnership, quote, demo, pilot",
        Keywords    : new[]
        {
            "plan", "plans", "feature", "features", "enterprise", "trial",
            "demo", "comparison", "compare", "competitor", "versus",
            "partner", "reseller", "contract", "sla", "quote", "proposal",
            "pilot", "proof of concept", "poc", "evaluation", "purchase",
            "license", "licenses", "seats", "volume", "bulk", "negotiate",
            "discount", "pricing", "tier", "package", "walkthrough",
            "startup", "non-profit", "nonprofit", "academic", "on-premise",
            "extend trial", "large teams", "500 users"
        },
        Agent    : salesAgent,
        Priority : 0.0),

    new(
        TreeName    : "general",
        AgentName   : "general-support",
        Description : "General questions, greetings, account, documentation, onboarding",
        Keywords    : new[]
        {
            "help", "question", "how", "what", "where", "when", "explain",
            "mobile", "app", "documentation", "docs", "status", "contact",
            "human", "speak", "maintenance", "hello", "hi", "hey",
            "get started", "onboarding", "guide", "tutorial", "dashboard",
            "account", "profile", "password", "reset", "languages", "integrations",
            "business hours", "news", "update"
        },
        Agent    : generalAgent,
        Priority : 0.0)
};

// ── Hybrid routing config ─────────────────────────────────────────────────────

var hybridConfig = new HybridForestSwarmConfig
{
    RoutingMode              = Enum.Parse<RoutingMode>(
                                   routingCfg["RoutingMode"] ?? "Hybrid", ignoreCase: true),
    SemanticWeight           = double.TryParse(routingCfg["SemanticWeight"],  out var sw) ? sw : 0.65,
    KeywordWeight            = double.TryParse(routingCfg["KeywordWeight"],   out var kw) ? kw : 0.25,
    PriorityWeight           = double.TryParse(routingCfg["PriorityWeight"],  out var pw) ? pw : 0.10,
    MinMatchScore            = double.TryParse(routingCfg["MinMatchScore"],   out var mm) ? mm : 0.35,
    AmbiguityGap             = double.TryParse(routingCfg["AmbiguityGap"],    out var ag) ? ag : 0.08,
    EnableDeterministicRerank = bool.TryParse(routingCfg["EnableDeterministicRerank"], out var er) ? er : true,
    ReturnRoutingDiagnostics  = bool.TryParse(routingCfg["ReturnRoutingDiagnostics"],  out var rd) ? rd : true
};

// ── Register services ─────────────────────────────────────────────────────────

builder.Services.AddSingleton<SupportSessionStore>();

builder.Services.AddSingleton<IRouteEmbedder>(sp =>
{
    var log = sp.GetRequiredService<ILogger<OllamaRouteEmbedder>>();
    var endpoint  = routingCfg["OllamaEndpoint"] ?? "http://localhost:11434";
    var embedModel = routingCfg["EmbedModel"]    ?? "mxbai-embed-large";
    return new OllamaRouteEmbedder(endpoint, embedModel, log);
});

builder.Services.AddSingleton<IRouteReranker>(_ => new DeterministicRouteReranker(hybridConfig));

builder.Services.AddSingleton(sp =>
{
    var embedder  = sp.GetRequiredService<IRouteEmbedder>();
    var reranker  = sp.GetRequiredService<IRouteReranker>();
    var log       = sp.GetRequiredService<ILogger<HybridSemanticRouter>>();
    return new HybridSemanticRouter(hybridConfig, embedder, reranker, log);
});

builder.Services.AddSingleton(sp =>
{
    var router = sp.GetRequiredService<HybridSemanticRouter>();
    var log    = sp.GetRequiredService<ILogger<HybridForestSwarm>>();
    return new HybridForestSwarm(
        hybridConfig,
        agentEntries,
        router,
        log,
        name        : agentCfg.Id,
        description : "Customer support router — hybrid semantic routing across Billing / Technical / Sales / General");
});

var app = builder.Build();

// ── Initialise embeddings on startup ─────────────────────────────────────────
app.Lifetime.ApplicationStarted.Register(() =>
{
    var swarm  = app.Services.GetRequiredService<HybridForestSwarm>();
    var logger = app.Services.GetRequiredService<ILogger<Program>>();

    _ = Task.Run(async () =>
    {
        try
        {
            logger.LogInformation("Initialising hybrid routing embeddings…");
            await swarm.InitializeAsync(CancellationToken.None);
            logger.LogInformation("Hybrid routing ready.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Embedding initialisation failed — keyword-only fallback active.");
        }
    });
});

// ─────────────────────────────────────────────────────────────────────────────
// Endpoints
// ─────────────────────────────────────────────────────────────────────────────

// GET /health
app.MapGet("/health", (HybridForestSwarm swarm, IRouteEmbedder embedder) =>
    Results.Ok(new
    {
        status          = "ok",
        agent           = swarm.Name,
        pattern         = "HybridForestSwarm",
        teams           = new[] { "billing", "technical", "sales", "general" },
        routingMode     = hybridConfig.RoutingMode.ToString(),
        semanticWeight  = hybridConfig.SemanticWeight,
        keywordWeight   = hybridConfig.KeywordWeight,
        priorityWeight  = hybridConfig.PriorityWeight,
        embeddingAvailable = embedder.IsAvailable,
        utc             = DateTime.UtcNow
    }));

// POST /support — route + invoke LLM agent
app.MapPost("/support", async (
    SupportRequest req,
    HybridForestSwarm swarm,
    SupportSessionStore sessions,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Message))
        return Results.BadRequest(new { error = "Message cannot be empty." });

    var history = sessions.Get(req.SessionId);
    sessions.Append(req.SessionId, "user", req.Message);

    var result = await swarm.RunAsync(req.Message, history, ct);

    sessions.Append(req.SessionId, "assistant", result.Output);

    var matchedKw = result.RoutingDecision.Candidates.Count > 0
        ? result.RoutingDecision.Candidates[0].MatchedKeywords
        : new List<string>();

    var response = new SupportResponse(
        Message           : req.Message,
        SessionId         : req.SessionId,
        RoutedTo          : result.SelectedAgent,
        TeamCategory      : result.SelectedTree,
        MatchedKeywords   : matchedKw,
        Answer            : result.Output,
        Duration          : result.Duration.TotalMilliseconds.ToString("F0") + "ms",
        RoutingDiagnostics: hybridConfig.ReturnRoutingDiagnostics ? result.RoutingDecision : null);

    return Results.Ok(response);
});

// POST /route/diagnose — routing only, no LLM call (fast for testing)
app.MapPost("/route/diagnose", async (
    SupportRequest req,
    HybridForestSwarm swarm,
    SupportSessionStore sessions,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Message))
        return Results.BadRequest(new { error = "Message cannot be empty." });

    var history  = sessions.Get(req.SessionId);
    var decision = await swarm.DiagnoseAsync(req.Message, history, ct);

    return Results.Ok(new
    {
        message           = req.Message,
        sessionId         = req.SessionId,
        selectedTree      = decision.SelectedTree,
        selectedAgent     = decision.SelectedAgent,
        selectedScore     = Math.Round(decision.SelectedScore, 4),
        confidence        = decision.Confidence.ToString(),
        isAmbiguous       = decision.IsAmbiguous,
        embeddingUsed     = decision.EmbeddingUsed,
        embeddingFallback = decision.EmbeddingFallback,
        candidates        = decision.Candidates.Select(c => new
        {
            tree              = c.TreeName,
            agent             = c.AgentName,
            semanticScore     = Math.Round(c.SemanticScore, 4),
            keywordScore      = Math.Round(c.KeywordScore, 4),
            priorityBias      = c.PriorityBias,
            initialScore      = Math.Round(c.InitialScore, 4),
            rerankAdjustment  = Math.Round(c.RerankAdjustment, 4),
            finalScore        = Math.Round(c.FinalScore, 4),
            matchedKeywords   = c.MatchedKeywords,
            explanation       = c.Explanation
        })
    });
});

// GET /routing-profiles — show all semantic profiles and embedding dimensions
app.MapGet("/routing-profiles", (HybridForestSwarm swarm) =>
{
    var profiles = swarm.GetProfiles();
    return Results.Ok(new
    {
        count    = profiles.Count,
        profiles = profiles.Select(p => new
        {
            tree          = p.TreeName,
            agent         = p.AgentName,
            description   = p.Description,
            keywords      = p.Keywords,
            profileText   = p.ProfileText,
            embeddingDim  = p.Embedding?.Length ?? 0,
            priority      = p.Priority
        })
    });
});

app.Run();
