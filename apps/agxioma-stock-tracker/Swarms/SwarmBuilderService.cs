using AGXIOME.Core;
using AGXIOME.Core.DependencyInjection;
using AGXIOME.Core.Patterns;
using AGXIOME.Shared.Models;

namespace AgxiomaStockTracker.Swarms;

/// <summary>
/// Builds HeavySwarm and GrokSwarm instances on demand.
/// Both are designed to be created fresh per request — they carry no mutable state.
/// </summary>
public sealed class SwarmBuilderService
{
    private readonly AgentSettings _cfg;
    private readonly LLMProviderType _provider;
    private readonly string _model;
    private readonly string? _apiKey;

    public SwarmBuilderService(IConfiguration configuration)
    {
        _cfg      = configuration.GetSection("Agent").Get<AgentSettings>() ?? new AgentSettings();
        var resolved = AgxiomeProviderResolver.Resolve(configuration["Agent:Provider"] ?? "openai", configuration);
        _provider = resolved.Provider;
        _model    = _cfg.Model ?? resolved.DefaultModel;
        _apiKey   = AgxiomeProviderResolver.ResolveApiKey(_provider, configuration);
    }

    // ── HeavySwarm — deep multi-phase research ────────────────────────────────

    public HeavySwarm BuildHeavySwarm()
    {
        var questionAgent = AgxiomeBuilder.Agent("question-generator")
            .UseModel(_provider, _model, _apiKey)
            .WithSystemPrompt(
                "You generate penetrating research questions for financial asset analysis.\n" +
                "Given a ticker, produce 5 key questions spanning: fundamentals, technicals,\n" +
                "macro environment, risks, and competitive positioning.\n" +
                "Output each question on a new line.")
            .Build();

        var workerCount = _cfg.WorkerCount;
        var workers = Enumerable.Range(1, workerCount).Select(i =>
            AgxiomeBuilder.Agent($"research-worker-{i}")
                .UseModel(_provider, _model, _apiKey)
                .WithSystemPrompt(
                    $"You are Research Analyst #{i} specialising in financial markets.\n" +
                    "Answer the assigned research question thoroughly, citing data, ratios,\n" +
                    "or public information where available. Flag any data gaps explicitly.")
                .Build()
        ).ToList();

        var synthesizer = AgxiomeBuilder.Agent("synthesis-agent")
            .UseModel(_provider, _model, _apiKey)
            .WithSystemPrompt(
                "You are a senior investment strategist. Synthesise multi-perspective research\n" +
                "findings into a clear, structured report:\n" +
                "1. Executive Summary (3-4 sentences)\n" +
                "2. Key Findings (bullet list)\n" +
                "3. Risk Factors\n" +
                "4. Opportunity / Bull Case\n" +
                "5. Conclusion\n" +
                "Do not give personal financial advice. Remain objective.")
            .Build();

        return new HeavySwarm(
            config: new HeavySwarmConfig
            {
                QuestionModel       = _model,
                WorkerModel         = _model,
                SynthesisModel      = _model,
                EnableParallelResearch = true
            },
            questionAgent: questionAgent,
            workers:       workers,
            synthesizer:   synthesizer
        );
    }

    // ── GrokSwarm — 4-agent buy/hold/sell debate ──────────────────────────────

    public GrokSwarm BuildGrokSwarm()
    {
        var captain = AgxiomeBuilder.Agent("debate-captain")
            .UseModel(_provider, _model, _apiKey)
            .WithSystemPrompt(
                "You chair a financial debate. After hearing the Bull, Bear, and Neutral perspectives,\n" +
                "you synthesise them into a clear BUY / HOLD / SELL verdict with confidence level (0-100%)\n" +
                "and a one-paragraph rationale.")
            .Build();

        var harper = AgxiomeBuilder.Agent("bull-harper")
            .UseModel(_provider, _model, _apiKey)
            .WithSystemPrompt(
                "You argue the BULL (BUY) case for the given asset. Present the strongest growth\n" +
                "catalysts, competitive advantages, undervaluation thesis, or momentum signals.\n" +
                "Be analytical and evidence-based.")
            .Build();

        var benjamin = AgxiomeBuilder.Agent("bear-benjamin")
            .UseModel(_provider, _model, _apiKey)
            .WithSystemPrompt(
                "You argue the BEAR (SELL) case for the given asset. Identify overvaluation,\n" +
                "macro headwinds, competitive threats, deteriorating fundamentals, or technical\n" +
                "breakdown signals. Be analytical and evidence-based.")
            .Build();

        var lucas = AgxiomeBuilder.Agent("neutral-lucas")
            .UseModel(_provider, _model, _apiKey)
            .WithSystemPrompt(
                "You represent the NEUTRAL (HOLD) perspective. Weigh bull and bear arguments\n" +
                "impartially, highlight uncertainties and missing information, and identify\n" +
                "the key catalyst or data point that would swing the verdict.")
            .Build();

        return SwarmFactory.GrokSwarm(captain, harper, benjamin, lucas, maxLoops: 1);
    }
}
