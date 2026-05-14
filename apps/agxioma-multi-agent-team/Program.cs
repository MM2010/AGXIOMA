using AGXIOME.Core;
using AGXIOME.Core.Patterns;
using AGXIOME.Shared.Models;
using AgxiomaMultiAgentTeam;

// ── Bootstrap ─────────────────────────────────────────────────────────────────
var builder = WebApplication.CreateBuilder(args);

var cfg = builder.Configuration.GetSection("Agent").Get<AgentSettings>() ?? new AgentSettings();

builder.Services.AddSingleton<ForestSwarm>(_ =>
{
    var provider = cfg.Provider switch
    {
        "anthropic" => LLMProviderType.Anthropic,
        "ollama"    => LLMProviderType.Ollama,
        "deepseek"  => LLMProviderType.DeepSeek,
        _           => LLMProviderType.OpenAI
    };
    var model    = cfg.Model ?? "gpt-4o";

    // Resolve API key
    var apiKey = provider switch
    {
        LLMProviderType.DeepSeek  => builder.Configuration["LLMProvider:DeepSeek:ApiKey"]
                                     ?? Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY"),
        LLMProviderType.Anthropic => builder.Configuration["LLMProvider:Anthropic:ApiKey"]
                                     ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"),
        _                         => builder.Configuration["LLMProvider:OpenAI:ApiKey"]
                                     ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    };

    // ── Build specialist agents ───────────────────────────────────────────────

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
            "Escalate to engineering (create a ticket) if the issue cannot be resolved in this session.")
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

    // ── Build ForestSwarm with keyword-based routing ──────────────────────────

    var billingTree = new ForestTree("billing", new[]
    {
        new ForestAgentEntry("billing-specialist",
            "Invoice, payment, subscription, refund, billing, pricing",
            new[] { "invoice", "payment", "bill", "billing", "charge", "subscription", "refund",
                    "cancel", "cancellation", "upgrade", "downgrade", "receipt", "tax", "price",
                    "cost", "fee", "proration", "coupon", "discount", "credit" },
            billingAgent)
    });

    var technicalTree = new ForestTree("technical", new[]
    {
        new ForestAgentEntry("technical-specialist",
            "API errors, installation, bugs, performance, authentication, configuration",
            new[] { "error", "bug", "crash", "crashes", "api", "install", "installation", "configuration",
                    "config", "authentication", "auth", "token", "key", "performance", "slow",
                    "timeout", "exception", "stack", "trace", "debug", "version", "update",
                    "upgrade", "connection", "endpoint", "ssl", "certificate", "build", "deploy",
                    "webhook", "webhooks", "event", "events", "url", "upload", "uploads",
                    "receiving", "receive", "response", "request", "failure", "fails", "broken",
                    "integration", "oauth", "http", "https", "status", "log", "logs", "console" },
            technicalAgent)
    });

    var salesTree = new ForestTree("sales", new[]
    {
        new ForestAgentEntry("sales-specialist",
            "Plans, features, enterprise, trial, comparison, partnership",
            new[] { "plan", "plans", "feature", "features", "enterprise", "trial", "demo",
                    "comparison", "compare", "comparing", "competitor", "competitors", "versus", "vs",
                    "partner", "reseller", "contract", "sla",
                    "quote", "proposal", "pilot", "poc", "proof", "concept", "evaluation",
                    "purchase", "buy", "buying", "license", "licenses", "seats", "users",
                    "volume", "bulk", "negotiate", "negotiation", "discount", "pricing",
                    "tier", "tiers", "package", "packages", "onboarding",
                    "schedule", "meeting", "call", "presentation", "walkthrough" },
            salesAgent)
    });

    var generalTree = new ForestTree("general", new[]
    {
        new ForestAgentEntry("general-support",
            "General questions, greetings, unclassified requests",
            new[] { "help", "question", "info", "information", "how", "what", "where",
                    "when", "explain", "tell", "understand", "mobile", "app", "application",
                    "available", "account", "profile",
                    "contact", "support", "human", "person", "speak",
                    "maintenance", "window", "news",
                    "hello", "hi", "hey", "started", "begin", "first", "guide", "tutorial" },
            generalAgent)
    });

    return new ForestSwarm(
        config: new ForestSwarmConfig { MinMatchScore = 0 },
        trees: new[] { billingTree, technicalTree, salesTree, generalTree },
        name: "MY_AGENT_NAME",
        description: "Customer support router — auto-routes to Billing, Technical, Sales, or General specialist"
    );
});

builder.Services.AddSingleton<SupportSessionStore>();

var app = builder.Build();

// ── Endpoints ─────────────────────────────────────────────────────────────────

app.MapGet("/health", () => Results.Ok(new
{
    status  = "ok",
    agent   = "MY_AGENT_NAME",
    pattern = "ForestSwarm",
    teams   = new[] { "billing", "technical", "sales", "general" },
    routing = "keyword-based automatic routing",
    utc     = DateTime.UtcNow
}));

// Main support endpoint — auto-routes to the right specialist
app.MapPost("/support", async (
    SupportRequest req,
    ForestSwarm swarm,
    SupportSessionStore sessions,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Message))
        return Results.BadRequest(new { error = "Message is required." });

    var history = sessions.Get(req.SessionId);
    var task    = history.Count > 0
        ? $"[Conversation history]\n{string.Join("\n", history.TakeLast(6).Select(h => $"{h.Role}: {h.Content}"))}\n\n[New message]\n{req.Message}"
        : req.Message;

    var result = await swarm.RunAsync(task, ct);
    sessions.Append(req.SessionId, req.Message, result.Output);

    return Results.Ok(new SupportResponse(
        Message:          req.Message,
        SessionId:        req.SessionId,
        RoutedTo:         result.SelectedAgent,
        TeamCategory:     result.SelectedTree,
        MatchedKeywords:  result.MatchedKeywords.ToList(),
        Answer:           result.Output,
        Duration:         $"{result.Duration.TotalMilliseconds:F0}ms"
    ));
});

// Force routing to a specific team
app.MapPost("/support/{team}", async (
    string team,
    SupportRequest req,
    ForestSwarm swarm,
    SupportSessionStore sessions,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Message))
        return Results.BadRequest(new { error = "Message is required." });

    // Prepend team-hint keywords to bias ForestSwarm routing
    var teamHint = team.ToLower() switch
    {
        "billing"   => "billing invoice payment",
        "technical" => "error bug api technical",
        "sales"     => "plan feature trial sales",
        _           => "help"
    };
    var biasedTask = $"{teamHint}: {req.Message}";
    var result = await swarm.RunAsync(biasedTask, ct);
    sessions.Append(req.SessionId, req.Message, result.Output);

    return Results.Ok(new SupportResponse(req.Message, req.SessionId, result.SelectedAgent,
        result.SelectedTree, result.MatchedKeywords.ToList(), result.Output,
        $"{result.Duration.TotalMilliseconds:F0}ms"));
});

// Get routing map (useful for debugging keyword weights)
app.MapGet("/routing-map", () => Results.Ok(new
{
    trees = new[]
    {
        new { team = "billing",   keywords = new[] { "invoice", "payment", "bill", "refund", "subscription", "charge" } },
        new { team = "technical", keywords = new[] { "error", "bug", "api", "install", "auth", "performance", "crash" } },
        new { team = "sales",     keywords = new[] { "plan", "feature", "trial", "enterprise", "price", "buy" } },
        new { team = "general",   keywords = new[] { "help", "question", "info", "how", "what", "explain" } }
    }
}));

// Delete session history
app.MapDelete("/session/{sessionId}", (string sessionId, SupportSessionStore sessions) =>
{
    sessions.Delete(sessionId);
    return Results.Ok(new { sessionId, deleted = true });
});

app.Run();
