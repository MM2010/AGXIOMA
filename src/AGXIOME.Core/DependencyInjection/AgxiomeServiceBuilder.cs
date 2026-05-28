using AGXIOME.Core.Patterns;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AGXIOME.Core.DependencyInjection;

/// <summary>
/// Fluent builder returned by <c>services.AddAgxiome()</c>.
/// Configures provider once, then registers any number of swarm patterns — zero boilerplate.
/// <example>
/// <code>
/// builder.Services.AddAgxiome()
///     .UseProvider("deepseek")
///     .AddForestSwarm("support", forest =>
///     {
///         forest.AddTree("billing")
///             .AddAgent("billing-specialist", "You handle billing questions.")
///             .WithKeywords("invoice", "payment", "refund", "subscription");
///
///         forest.AddTree("technical")
///             .AddAgent("technical-specialist", "You handle technical issues.")
///             .WithKeywords("error", "bug", "api", "auth", "performance");
///     })
///     .AddHeavySwarm("researcher", heavy =>
///     {
///         heavy.WithWorkers(3).WithParallelResearch();
///     });
/// </code>
/// </example>
/// </summary>
public sealed class AgxiomeServiceBuilder
{
    private LLMProviderType _provider = LLMProviderType.DeepSeek;
    private string _model = "deepseek-chat";
    private string? _apiKey;
    private string? _endpoint;

    /// <summary>The underlying service collection. Use for raw DI registrations.</summary>
    public IServiceCollection Services { get; }

    internal AgxiomeServiceBuilder(IServiceCollection services)
    {
        Services = services;
    }

    /// <summary>
    /// Set the LLM provider for all swarms registered via this builder.
    /// API key is resolved from environment variables automatically.
    /// </summary>
    /// <param name="providerName">Provider name: "deepseek", "openai", "anthropic", "gemini", "ollama", etc.</param>
    /// <param name="model">Override the default model for this provider.</param>
    /// <param name="endpoint">Override the default endpoint for this provider.</param>
    public AgxiomeServiceBuilder UseProvider(string providerName, string? model = null, string? endpoint = null)
    {
        var (provider, defaultModel, defaultEndpoint) = AgxiomeProviderResolver.Resolve(providerName, null);
        _provider = provider;
        _model    = model ?? defaultModel;
        _apiKey   = AgxiomeProviderResolver.ResolveApiKey(provider, null);
        _endpoint = endpoint ?? defaultEndpoint;
        return this;
    }

    /// <summary>
    /// Register a <see cref="ForestSwarm"/> (keyword-routed multi-tree) in DI.
    /// </summary>
    public AgxiomeServiceBuilder AddForestSwarm(string name, Action<ForestSwarmBuilder> configure)
    {
        var sb = new ForestSwarmBuilder(name, _provider, _model, _apiKey, _endpoint);
        configure(sb);
        Services.AddSingleton(sb.Build());
        return this;
    }

    /// <summary>
    /// Register a <see cref="HeavySwarm"/> (multi-phase parallel research) in DI.
    /// </summary>
    public AgxiomeServiceBuilder AddHeavySwarm(string name, Action<HeavySwarmBuilder> configure)
    {
        var hb = new HeavySwarmBuilder(name, _provider, _model, _apiKey, _endpoint);
        configure(hb);
        Services.AddSingleton(hb.Build());
        return this;
    }
}

// ── ForestSwarm fluent builders ───────────────────────────────────────────────

/// <summary>
/// Fluent builder for a <see cref="ForestSwarm"/>.
/// Obtained from <see cref="AgxiomeServiceBuilder.AddForestSwarm"/>.
/// </summary>
public sealed class ForestSwarmBuilder
{
    private readonly string _name;
    private readonly LLMProviderType _provider;
    private readonly string _model;
    private readonly string? _apiKey;
    private readonly string? _endpoint;
    private readonly List<ForestTreeBuilder> _trees = [];
    private int _minMatchScore;

    internal ForestSwarmBuilder(string name, LLMProviderType provider, string model, string? apiKey, string? endpoint)
    {
        _name = name; _provider = provider; _model = model; _apiKey = apiKey; _endpoint = endpoint;
    }

    /// <summary>
    /// Add a named routing tree. Returns a <see cref="ForestTreeBuilder"/> to register agents.
    /// </summary>
    public ForestTreeBuilder AddTree(string treeName)
    {
        var tb = new ForestTreeBuilder(treeName, _provider, _model, _apiKey, _endpoint);
        _trees.Add(tb);
        return tb;
    }

    /// <summary>Minimum keyword overlap score required before routing to any agent (default 0).</summary>
    public ForestSwarmBuilder WithMinMatchScore(int score) { _minMatchScore = score; return this; }

    internal ForestSwarm Build()
    {
        if (_trees.Count == 0)
            throw new InvalidOperationException($"ForestSwarm '{_name}' requires at least one tree.");

        return new ForestSwarm(
            new ForestSwarmConfig { MinMatchScore = _minMatchScore },
            _trees.Select(t => t.BuildTree()).ToList(),
            _name);
    }
}

/// <summary>
/// Fluent builder for a single routing tree inside a <see cref="ForestSwarmBuilder"/>.
/// </summary>
public sealed class ForestTreeBuilder
{
    private readonly string _treeName;
    private readonly LLMProviderType _provider;
    private readonly string _model;
    private readonly string? _apiKey;
    private readonly string? _endpoint;
    private readonly List<AgentDef> _defs = [];

    internal ForestTreeBuilder(string treeName, LLMProviderType provider, string model, string? apiKey, string? endpoint)
    {
        _treeName = treeName; _provider = provider; _model = model; _apiKey = apiKey; _endpoint = endpoint;
    }

    /// <summary>
    /// Add an agent to this tree.
    /// Returns a <see cref="ForestAgentBuilder"/>; call
    /// <see cref="ForestAgentBuilder.WithKeywords"/> to set routing keywords and
    /// return to the tree builder.
    /// </summary>
    public ForestAgentBuilder AddAgent(string agentId, string? systemPrompt = null)
    {
        var def = new AgentDef(agentId, systemPrompt);
        _defs.Add(def);
        return new ForestAgentBuilder(def.Keywords, def.Tools, p => def.SystemPrompt = p, this);
    }

    internal ForestTree BuildTree()
    {
        if (_defs.Count == 0)
            throw new InvalidOperationException($"Tree '{_treeName}' requires at least one agent.");

        var entries = _defs.Select(def =>
        {
            var ab = AgxiomeBuilder.Agent(def.Id)
                .UseModel(_provider, _model, _apiKey, _endpoint)
                .WithSystemPrompt(def.SystemPrompt ?? $"You are a helpful {def.Id} specialist.");
            foreach (var tool in def.Tools) ab = ab.WithTool(tool);
            return new ForestAgentEntry(def.Id, def.Id, def.Keywords.AsReadOnly(), ab.Build());
        }).ToList();

        return new ForestTree(_treeName, entries);
    }

    private sealed class AgentDef(string id, string? systemPrompt)
    {
        public string Id                 { get; }    = id;
        public string? SystemPrompt      { get; set; } = systemPrompt;
        public List<string> Keywords     { get; }    = [];
        public List<IAgxiomeTool> Tools  { get; }    = [];
    }
}

/// <summary>
/// Fluent builder for an individual agent within a <see cref="ForestTreeBuilder"/>.
/// </summary>
public sealed class ForestAgentBuilder
{
    private readonly List<string> _keywords;
    private readonly List<IAgxiomeTool> _tools;
    private readonly Action<string> _setSystemPrompt;
    private readonly ForestTreeBuilder _parent;

    internal ForestAgentBuilder(
        List<string> keywords,
        List<IAgxiomeTool> tools,
        Action<string> setSystemPrompt,
        ForestTreeBuilder parent)
    {
        _keywords        = keywords;
        _tools           = tools;
        _setSystemPrompt = setSystemPrompt;
        _parent          = parent;
    }

    /// <summary>Override the system prompt for this agent.</summary>
    public ForestAgentBuilder WithSystemPrompt(string prompt) { _setSystemPrompt(prompt); return this; }

    /// <summary>Register a tool the agent can call during inference.</summary>
    public ForestAgentBuilder WithTool(IAgxiomeTool tool) { _tools.Add(tool); return this; }

    /// <summary>
    /// Register routing keywords and return to the parent tree builder
    /// so you can chain the next <see cref="ForestTreeBuilder.AddAgent"/> call.
    /// </summary>
    public ForestTreeBuilder WithKeywords(params string[] keywords)
    {
        _keywords.AddRange(keywords);
        return _parent;
    }
}

// ── HeavySwarm fluent builder ─────────────────────────────────────────────────

/// <summary>
/// Fluent builder for a <see cref="HeavySwarm"/> (multi-phase parallel research pipeline).
/// Obtained from <see cref="AgxiomeServiceBuilder.AddHeavySwarm"/>.
/// </summary>
public sealed class HeavySwarmBuilder
{
    private readonly string _name;
    private readonly LLMProviderType _provider;
    private readonly string _model;
    private readonly string? _apiKey;
    private readonly string? _endpoint;

    private int _workerCount = 3;
    private bool _enableParallel = true;

    private string _questionPrompt =
        "Generate 5 focused research questions about the following topic. " +
        "Cover: fundamentals, context, risks, opportunities, and key metrics. " +
        "Output each question on a new line.";

    private string _workerPromptTemplate =
        "You are Research Analyst #{n}. Answer the assigned question thoroughly, " +
        "citing data or public information where available. Flag data gaps explicitly.";

    private string _synthesisPrompt =
        "Synthesise the research findings into a structured report:\n" +
        "1. Executive Summary\n" +
        "2. Key Findings\n" +
        "3. Risk Factors\n" +
        "4. Opportunities\n" +
        "5. Conclusion\n" +
        "Be objective and evidence-based.";

    internal HeavySwarmBuilder(string name, LLMProviderType provider, string model, string? apiKey, string? endpoint)
    {
        _name = name; _provider = provider; _model = model; _apiKey = apiKey; _endpoint = endpoint;
    }

    /// <summary>Number of parallel research worker agents (default 3).</summary>
    public HeavySwarmBuilder WithWorkers(int count) { _workerCount = count; return this; }

    /// <summary>Enable or disable parallel research (default true).</summary>
    public HeavySwarmBuilder WithParallelResearch(bool enabled = true) { _enableParallel = enabled; return this; }

    /// <summary>System prompt for the question-generation agent.</summary>
    public HeavySwarmBuilder WithQuestionPrompt(string prompt) { _questionPrompt = prompt; return this; }

    /// <summary>
    /// System prompt template for each worker agent.
    /// Use <c>{n}</c> to insert the worker number.
    /// </summary>
    public HeavySwarmBuilder WithWorkerPrompt(string template) { _workerPromptTemplate = template; return this; }

    /// <summary>System prompt for the synthesis agent.</summary>
    public HeavySwarmBuilder WithSynthesisPrompt(string prompt) { _synthesisPrompt = prompt; return this; }

    internal HeavySwarm Build()
    {
        var questionAgent = AgxiomeBuilder.Agent("question-generator")
            .UseModel(_provider, _model, _apiKey, _endpoint)
            .WithSystemPrompt(_questionPrompt)
            .Build();

        var workers = Enumerable.Range(1, _workerCount)
            .Select(i => AgxiomeBuilder.Agent($"research-worker-{i}")
                .UseModel(_provider, _model, _apiKey, _endpoint)
                .WithSystemPrompt(_workerPromptTemplate.Replace("{n}", i.ToString()))
                .Build())
            .ToList();

        var synthesizer = AgxiomeBuilder.Agent("synthesizer")
            .UseModel(_provider, _model, _apiKey, _endpoint)
            .WithSystemPrompt(_synthesisPrompt)
            .Build();

        return new HeavySwarm(
            new HeavySwarmConfig
            {
                QuestionModel          = _model,
                WorkerModel            = _model,
                SynthesisModel         = _model,
                EnableParallelResearch = _enableParallel
            },
            questionAgent,
            workers,
            synthesizer);
    }
}
