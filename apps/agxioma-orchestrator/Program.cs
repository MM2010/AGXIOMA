using AGXIOME.Core.DependencyInjection;
using AGXIOME.Core.Patterns;

var builder = WebApplication.CreateBuilder(args);

var provider = builder.Configuration["Agent:Provider"] ?? "PROVIDER_PLACEHOLDER";
var model    = builder.Configuration["Agent:Model"];
var workers  = builder.Configuration.GetValue<int>("Agent:Workers", WORKERS_PLACEHOLDER);

// Register a HeavySwarm research orchestrator — three parallel analysts + synthesizer
builder.Services.AddAgxiome()
    .UseProvider(provider, string.IsNullOrEmpty(model) ? null : model)
    .AddHeavySwarm("researcher", heavy =>
    {
        heavy
            .WithWorkers(workers)
            .WithParallelResearch()
            .WithQuestionPrompt(
                "Generate 5 focused research questions about the following topic. " +
                "Cover: fundamentals, context, risks, opportunities, and key metrics. " +
                "Output each question on a new line.")
            .WithSynthesisPrompt(
                "Synthesise the analyst findings into a structured report:\n" +
                "1. Executive Summary\n" +
                "2. Key Findings\n" +
                "3. Risk Factors\n" +
                "4. Opportunities\n" +
                "5. Conclusion\n" +
                "Be objective and evidence-based.");
    });

var app = builder.Build();

// POST /research — deep multi-phase research on any topic
app.MapPost("/research", async (ResearchRequest req, HeavySwarm swarm) =>
{
    var result = await swarm.RunAsync(req.Topic);
    return Results.Ok(new
    {
        topic    = req.Topic,
        report   = result.FinalReport,
        duration = result.TotalDuration.TotalSeconds
    });
});

app.MapGet("/", () => "AGXIOME Orchestrator (HeavySwarm) is running!");

app.Run();

public record ResearchRequest(string Topic);
