using AGXIOME.Core;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

var builder = WebApplication.CreateBuilder(args);

// Create AGXIOME agent with zero boilerplate
var agent = AgxiomeBuilder.Agent("my-agent")
    .UseModel(LLMProviderType.PROVIDER_PLACEHOLDER, "MODEL_PLACEHOLDER")
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
