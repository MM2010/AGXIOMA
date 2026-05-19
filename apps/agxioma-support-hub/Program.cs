using AGXIOME.Core.DependencyInjection;
using AGXIOME.Core.Patterns;
using AGXIOME.Runtime;
using AGXIOME.Security.Rbac;
using AgxiomaSupportHub.Tools;

// ── Bootstrap ─────────────────────────────────────────────────────────────────
var builder = WebApplication.CreateBuilder(args);
builder.AddAgxiomeSecurityDefaults();
builder.Services.AddSingleton<BackofficeRuntimeService>();

// Allow Next.js frontend (demo)
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins("http://localhost:3000")
     .AllowAnyHeader()
     .AllowAnyMethod()));

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

    .AddForestSwarm("backoffice-router", forest =>
    {
        forest.AddTree("orders")
            .AddAgent("orders-router", "Route order and shipping issues to the operations lane.")
            .WithKeywords("order", "tracking", "shipment", "delivery", "address", "package", "spedizione");

        forest.AddTree("billing")
            .AddAgent("billing-router", "Route invoice and payment issues to the billing lane.")
            .WithKeywords("invoice", "fattura", "billing", "payment", "charge", "vat", "receipt", "addebito");

        forest.AddTree("refunds")
            .AddAgent("refunds-router", "Route refund and dispute issues to the refunds lane.")
            .WithKeywords("refund", "rimborso", "return", "chargeback", "cancel", "dispute", "contestazione");

        forest.AddTree("account")
            .AddAgent("account-router", "Route account and identity issues to the account lane.")
            .WithKeywords("account", "login", "access", "password", "identity", "profile", "profilo");

        forest.AddTree("technical")
            .AddAgent("technical-router", "Route technical incidents to the technical lane.")
            .WithKeywords("bug", "error", "api", "integration", "timeout", "platform", "incident");
    })

    .AddForestSwarm("backoffice-content", forest =>
    {
        forest.AddTree("composer")
            .AddAgent("backoffice-composer",
                "You are a senior backoffice operations coordinator for an enterprise support desk. " +
                "Given a customer case, produce only valid JSON with exactly these string properties: " +
                "resolutionPlan, reviewNotes, customerReply. " +
                "resolutionPlan must be a concise numbered plan in the same language as the customer context. " +
                "reviewNotes must be short internal notes for operators, highlighting risk, approvals, and next checks. " +
                "customerReply must be suitable for sending directly to the customer, concise, professional, and action-oriented. " +
                "Never add greetings, signatures, placeholders, markdown fences, or extra fields. " +
                "Do not invent facts, approvals, payments, or completed actions that are not present in the case data.")
            .WithKeywords("backoffice", "ticket", "customer", "reply", "resolution", "plan", "review", "billing", "refund", "order", "account", "technical");
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
app.UseCors();
await app.UseAgxiomeSecurityDefaultsAsync();
await app.Services.GetRequiredService<BackofficeRuntimeService>().InitializeAsync();

// ── Endpoints ─────────────────────────────────────────────────────────────────

// Health check
app.MapGet("/health", (IConfiguration cfg) => Results.Ok(new
{
    status   = "ok",
    provider = cfg["Agent:Provider"],
    model    = cfg["Agent:Model"],
    swarms   = new[] { "support (ForestSwarm)", "analyst (HeavySwarm)" },
    utc      = DateTime.UtcNow
}))
    .AllowAnonymousAgxiome();

// POST /support — route customer message to the best-matching agent
app.MapPost("/support", async (SupportRequest req, IEnumerable<ForestSwarm> swarms) =>
{
    if (string.IsNullOrWhiteSpace(req.Message))
        return Results.BadRequest(new { error = "message is required." });

    var support = swarms.First(s => string.Equals(s.Name, "support", StringComparison.OrdinalIgnoreCase));
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
})
    .RequireAgxiomePermission(AgxiomePermission.WorkflowExecute)
    .WithAgxiomeRateLimit(20, TimeSpan.FromMinutes(1));

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
})
    .RequireAgxiomePermission(AgxiomePermission.WorkflowExecute)
    .WithAgxiomeRateLimit(10, TimeSpan.FromMinutes(1));

app.MapPost("/backoffice", async (BackofficeRequestInput req, BackofficeRuntimeService runtime, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.CustomerId) ||
        string.IsNullOrWhiteSpace(req.CustomerEmail) ||
        string.IsNullOrWhiteSpace(req.Subject) ||
        string.IsNullOrWhiteSpace(req.Message))
    {
        return Results.BadRequest(new { error = "customerId, customerEmail, subject and message are required." });
    }

    var run = await runtime.CreateRunAsync(req, ct);
    return Results.Created($"/backoffice/{run.RunId}", run);
})
    .AllowAnonymousAgxiome();

app.MapGet("/backoffice/dashboard", async (BackofficeRuntimeService runtime, CancellationToken ct) =>
{
    var dashboard = await runtime.GetDashboardAsync(ct);
    return Results.Ok(dashboard);
})
    .AllowAnonymousAgxiome();

app.MapGet("/backoffice/runs", async (BackofficeRuntimeService runtime, CancellationToken ct) =>
{
    var runs = await runtime.ListRunsAsync(50, ct);
    return Results.Ok(runs);
})
    .AllowAnonymousAgxiome();

app.MapGet("/backoffice/tickets", async (BackofficeRuntimeService runtime, CancellationToken ct) =>
{
    var tickets = await runtime.ListTicketsAsync(50, ct);
    return Results.Ok(tickets);
})
    .AllowAnonymousAgxiome();

app.MapGet("/backoffice/surveys", async (BackofficeRuntimeService runtime, CancellationToken ct) =>
{
    var surveys = await runtime.ListSurveysAsync(50, ct);
    return Results.Ok(surveys);
})
    .AllowAnonymousAgxiome();

app.MapGet("/backoffice/{runId}", async (string runId, BackofficeRuntimeService runtime, CancellationToken ct) =>
{
    var run = await runtime.GetRunAsync(runId, ct);
    return run is null ? Results.NotFound(new { error = "Run not found." }) : Results.Ok(run);
})
    .AllowAnonymousAgxiome();

app.MapPatch("/backoffice/{runId}", async (string runId, BackofficeRunActionRequest req, BackofficeRuntimeService runtime, CancellationToken ct) =>
{
    if (!string.Equals(req.Action, "close", StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest(new { error = "Unsupported action." });

    var run = await runtime.CloseRunAsync(runId, ct);
    return run is null ? Results.NotFound(new { error = "Run not found." }) : Results.Ok(run);
})
    .AllowAnonymousAgxiome();

app.MapPost("/backoffice/confirm", async (BackofficeConfirmRequest req, BackofficeRuntimeService runtime, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Token))
        return Results.BadRequest(new { error = "Token required." });

    var run = await runtime.ConfirmRunAsync(req.Token, ct);
    return run is null ? Results.NotFound(new { error = "Invalid token." }) : Results.Ok(run);
})
    .AllowAnonymousAgxiome();

app.MapPost("/backoffice/survey", async (BackofficeSurveyRequest req, BackofficeRuntimeService runtime, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.TicketId) || req.Score is < 1 or > 5)
        return Results.BadRequest(new { error = "ticketId is required and score must be between 1 and 5." });

    var survey = await runtime.SaveSurveyAsync(req.TicketId, req.Score, req.Comment, ct);
    return Results.Created($"/backoffice/survey/{survey.TicketId}", survey);
})
    .AllowAnonymousAgxiome();

app.MapGet("/", () =>
    "AGXIOME Support Hub — POST /support | POST /analyze | POST /backoffice | GET /backoffice/dashboard")
    .AllowAnonymousAgxiome();

app.Run();

// ── Request models ────────────────────────────────────────────────────────────

/// <summary>Customer support message to route via ForestSwarm.</summary>
public record SupportRequest(string Message);

/// <summary>Topic to research via HeavySwarm parallel pipeline.</summary>
public record AnalysisRequest(string Topic);

public sealed record BackofficeRequestInput(
    string CustomerId,
    string CustomerEmail,
    string? CustomerPhone,
    string? TelegramChatId,
    string Subject,
    string Message,
    string PreferredLanguage,
    IReadOnlyList<string> PreferredChannels);

public sealed record BackofficeRunActionRequest(string? Action);

public sealed record BackofficeConfirmRequest(string? Token);

public sealed record BackofficeSurveyRequest(string? TicketId, int Score, string? Comment);

public sealed record SurveyRecord(string TicketId, int Score, string? Comment, DateTimeOffset SubmittedAt);

public sealed record RunTimelineEntry(string Key, string Title, string Description, DateTimeOffset CreatedAt);

public sealed record RunRecord(
    string RunId,
    string Status,
    string Area,
    string Priority,
    string? TicketId,
    string? CheckpointId,
    string? ConfirmationToken,
    string CustomerReply,
    string ResolutionPlan,
    string ReviewNotes,
    DateTimeOffset CreatedAt,
    double AvgResponseSeconds,
    IReadOnlyList<string> ChannelsNotified,
    IReadOnlyList<RunTimelineEntry> Timeline);

public partial class Program { }
