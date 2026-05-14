using AGXIOME.Core.DependencyInjection;
using AGXIOME.Core.Patterns;
using AgxiomaSupportHub.Tools;

// ── Bootstrap ─────────────────────────────────────────────────────────────────
var builder = WebApplication.CreateBuilder(args);

var provider = builder.Configuration["Agent:Provider"] ?? "deepseek";
var model    = builder.Configuration["Agent:Model"];
var workers  = builder.Configuration.GetValue("Agent:ResearchWorkers", 3);

// ── Swarm registration — fluent API ──────────────────────────────────────────
builder.Services.AddAgxiome()
    .UseProvider(provider, string.IsNullOrEmpty(model) ? null : model)

    // ── Customer support routing (ForestSwarm) ────────────────────────────
    .AddForestSwarm("support", forest =>
    {
        // Billing tree — two agents covering different complexity tiers
        forest.AddTree("billing")
            .AddAgent("billing-specialist",
                "You are a billing support specialist. You look up invoice status and help " +
                "customers understand their charges. When a customer asks for a refund on a " +
                "paid invoice, use the process_refund tool. Always confirm the invoice ID " +
                "before taking any action.")
            .WithTool(new GetInvoiceStatusTool())
            .WithTool(new ProcessRefundTool())
            .WithKeywords("invoice", "payment", "charge", "refund", "subscription",
                          "billing", "receipt", "overdue", "paid", "balance");

        // Technical tree — first-line support + escalation agent with ticket creation
        forest.AddTree("technical")
            .AddAgent("tech-specialist",
                "You are a technical support engineer. You diagnose API errors, authentication " +
                "failures, and performance issues. Always check system status first before " +
                "troubleshooting. Provide step-by-step resolution guides.")
            .WithTool(new GetSystemStatusTool())
            .WithKeywords("error", "bug", "crash", "api", "auth", "token", "401", "403",
                          "timeout", "slow", "performance", "latency", "exception")
            .AddAgent("escalation-agent",
                "You handle critical outages and incidents that require immediate engineering " +
                "escalation. Check system status, assess impact, and create a high-priority " +
                "ticket for the on-call team. Be concise and precise in the ticket description.")
            .WithTool(new GetSystemStatusTool())
            .WithTool(new CreateTicketTool())
            .WithKeywords("urgent", "outage", "down", "critical", "incident", "unavailable",
                          "production", "sev1", "sev2", "all users");

        // Sales tree — pricing queries and plan upgrades
        forest.AddTree("sales")
            .AddAgent("sales-specialist",
                "You are a knowledgeable sales advisor. Help prospects and customers understand " +
                "our plans, compare features, and choose the right tier. For Enterprise inquiries, " +
                "collect requirements and offer to connect them with the sales team. Be helpful, " +
                "not pushy.")
            .WithTool(new GetPricingInfoTool())
            .WithKeywords("plan", "pricing", "price", "cost", "trial", "enterprise", "upgrade",
                          "features", "seats", "demo", "discount", "annual", "monthly");

        // General fallback tree
        forest.AddTree("general")
            .AddAgent("general-assistant",
                "You are a helpful general-purpose assistant for questions that don't fall into " +
                "billing, technical, or sales categories. Answer concisely and guide the user to " +
                "the right resource when needed.")
            .WithKeywords("help", "question", "info", "explain", "what", "how", "why",
                          "where", "account", "profile");
    })

    // ── Deep research pipeline (HeavySwarm) ───────────────────────────────
    .AddHeavySwarm("analyst", heavy =>
    {
        heavy
            .WithWorkers(workers)
            .WithParallelResearch()
            .WithQuestionPrompt(
                "You are a senior product analyst. Generate 5 focused research questions " +
                "about the following topic relevant to a SaaS support platform. " +
                "Cover: user impact, root causes, resolution patterns, industry benchmarks, " +
                "and preventive measures. Output each question on a new line.")
            .WithSynthesisPrompt(
                "Synthesise the analyst findings into a structured insight report:\n" +
                "1. Executive Summary (2-3 sentences)\n" +
                "2. Root Cause Analysis\n" +
                "3. User Impact Assessment\n" +
                "4. Resolution Recommendations (prioritised)\n" +
                "5. Prevention & Long-term Strategy\n" +
                "Be specific, actionable, and evidence-based.");
    });

var app = builder.Build();

// ── Endpoints ─────────────────────────────────────────────────────────────────

// Health check
app.MapGet("/health", (IConfiguration cfg) => Results.Ok(new
{
    status   = "ok",
    provider = cfg["Agent:Provider"],
    model    = cfg["Agent:Model"],
    swarms   = new[] { "support (ForestSwarm)", "analyst (HeavySwarm)" },
    utc      = DateTime.UtcNow
}));

// POST /support — route customer message to the best-matching agent
app.MapPost("/support", async (SupportRequest req, ForestSwarm support) =>
{
    if (string.IsNullOrWhiteSpace(req.Message))
        return Results.BadRequest(new { error = "message is required." });

    var result = await support.RunAsync(req.Message);
    return Results.Ok(new
    {
        message         = req.Message,
        routedTo        = result.SelectedAgent,
        tree            = result.SelectedTree,
        matchedKeywords = result.MatchedKeywords,
        response        = result.Output,
        durationMs      = result.Duration.TotalMilliseconds
    });
});

// POST /analyze — deep multi-phase research on any support topic
app.MapPost("/analyze", async (AnalysisRequest req, HeavySwarm analyst) =>
{
    if (string.IsNullOrWhiteSpace(req.Topic))
        return Results.BadRequest(new { error = "topic is required." });

    var result = await analyst.RunAsync(req.Topic);
    return Results.Ok(new
    {
        topic     = req.Topic,
        synthesis = result.Synthesis,
        analysis  = result.Analysis,
        findings  = result.ResearchFindings,
        phases    = result.PhaseTiming.Select(kv => new { phase = kv.Key, durationS = kv.Value.TotalSeconds })
    });
});

app.MapGet("/", () =>
    "AGXIOME Support Hub — POST /support (customer questions) | POST /analyze (deep research)");

app.Run();

// ── Request models ────────────────────────────────────────────────────────────

/// <summary>Customer support message to route via ForestSwarm.</summary>
public record SupportRequest(string Message);

/// <summary>Topic to research via HeavySwarm parallel pipeline.</summary>
public record AnalysisRequest(string Topic);
