using AGXIOME.Core;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

var builder = WebApplication.CreateBuilder(args);

// ── Provider / model resolved from env-vars or appsettings (agent-agnostic) ──
// Default: DeepSeek. Override via AGXIOME__Provider and AGXIOME__Model env-vars.
var cfg      = builder.Configuration;
var provider = Enum.TryParse<LLMProviderType>(cfg["Agxiome:Provider"] ?? "DeepSeek", ignoreCase: true, out var p)
               ? p : LLMProviderType.DeepSeek;
var model    = cfg["Agxiome:Model"]
               ?? Environment.GetEnvironmentVariable("AGXIOME_MODEL")
               ?? "deepseek-chat";
var apiKey   = cfg["Agxiome:ApiKey"]
               ?? Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY")
               ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

// Create AGXIOME agent with zero boilerplate
var agent = AgxiomeBuilder.Agent("my-agent")
    .UseModel(provider, model, apiKey)
    .WithSystemPrompt("You are a helpful AI assistant.")
    .WithAutoLoops(upperBound: 50)
    .Build();

// Register for DI
builder.Services.AddSingleton(agent);

var app = builder.Build();

// Simple API endpoint
app.MapPost("/chat", async (ChatRequest req, IAgxiomeAgent ag) =>
{
    var result = await ag.RunAsync(new AgentRequest(req.Prompt));
    return Results.Ok(new { response = result.Content });
});

app.MapGet("/", () => "AGXIOME Agent is running!");

app.Run();

public record ChatRequest(string Prompt);
