using AGXIOME.Core.DependencyInjection;
using AGXIOME.Core.Patterns;

var builder = WebApplication.CreateBuilder(args);

var provider = builder.Configuration["Agent:Provider"] ?? "PROVIDER_PLACEHOLDER";
var model    = builder.Configuration["Agent:Model"];

// Register a ForestSwarm team with keyword-based routing.
// Each tree handles a domain; agents inside a tree are scored by keyword overlap.
// Add / remove trees and agents to fit your use case.
builder.Services.AddAgxiome()
    .UseProvider(provider, string.IsNullOrEmpty(model) ? null : model)
    .AddForestSwarm("team", forest =>
    {
        forest.AddTree("billing")
            .AddAgent("billing-specialist",
                "You are a billing support specialist. " +
                "You handle invoice, payment, subscription, and refund questions with accuracy and empathy.")
            .WithKeywords("invoice", "payment", "charge", "refund", "subscription", "billing", "receipt");

        forest.AddTree("technical")
            .AddAgent("technical-specialist",
                "You are a technical support engineer. " +
                "You diagnose and resolve API errors, authentication issues, and performance problems.")
            .WithKeywords("error", "bug", "crash", "api", "auth", "token", "timeout", "performance", "issue");

        forest.AddTree("sales")
            .AddAgent("sales-specialist",
                "You are a sales advisor. " +
                "You explain plans, pricing, and enterprise options to prospective customers.")
            .WithKeywords("plan", "pricing", "trial", "enterprise", "upgrade", "features", "demo");

        forest.AddTree("general")
            .AddAgent("general-assistant",
                "You are a helpful general-purpose assistant. " +
                "Answer questions that don't fall into billing, technical, or sales categories.")
            .WithKeywords("help", "question", "info", "explain", "what", "how", "why");
    });

var app = builder.Build();

// POST /chat — route to the best-matching team agent
app.MapPost("/chat", async (ChatRequest req, ForestSwarm team) =>
{
    var result = await team.RunAsync(req.Message);
    return Results.Ok(new
    {
        message      = req.Message,
        routedTo     = result.SelectedAgentId,
        tree         = result.SelectedTreeName,
        keywords     = result.MatchedKeywords,
        response     = result.Response,
        duration     = result.Duration.TotalMilliseconds
    });
});

app.MapGet("/", () => "AGXIOME Team (ForestSwarm) is running!");

app.Run();

public record ChatRequest(string Message);
