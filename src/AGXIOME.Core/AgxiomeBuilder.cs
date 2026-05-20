using AGXIOME.Core.Agent;
using AGXIOME.Core.Patterns;
using AGXIOME.Core.Routing;
using AGXIOME.Core.Teams;
using AGXIOME.LlmWiky;
using AGXIOME.LlmWiky.Providers;
using AGXIOME.Shared.Config;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AGXIOME.Core;

/// <summary>
/// AGXIOMA Fluent Builder — zero-boilerplate agent and swarm creation.
///
/// Go from nothing to a working agent in 1 line:
///   var agent = AgxiomeBuilder.Agent("analyst").UseDeepSeek(apiKey).Build();
///
/// Or a full swarm in 5 lines:
///   var swarm = AgxiomeBuilder.Swarm()
///       .AddAgent("researcher", b => b.UseOllama().WithSystemPrompt("You research topics."))
///       .AddAgent("writer",     b => b.UseDeepSeek(key).WithSystemPrompt("You write reports."))
///       .AsMixtureOfAgents(layers: 2)
///       .Build();
///
/// Replaces all manual AgentConfig + LLMWIKYConfig + AgentFactory wiring.
/// Type-safe, IntelliSense-friendly, no magic strings.
/// </summary>
public sealed class AgxiomeBuilder
{
    // ─── Entry Points ──────────────────────────────────────────────────────────

    public static AgentBuilder Agent(string name) => new(name);
    public static SwarmBuilder Swarm() => new();
    public static QuickStartHelper QuickStart => QuickStartHelper.Instance;

    // ─── AgentBuilder ──────────────────────────────────────────────────────────

    public sealed class AgentBuilder
    {
        private readonly string _name;
        private string? _systemPrompt;
        private int _maxIterations = 10;
        private MaxIterationsMode _maxIterationsMode = MaxIterationsMode.Fixed;
        private TimeSpan _timeout = TimeSpan.FromSeconds(60);
        private LLMWIKYConfig _llmConfig = new();
        private readonly List<IAgxiomeTool> _tools = new();
        private ILoggerFactory? _loggerFactory;

        internal AgentBuilder(string name) => _name = name;

        // ─── LLM Provider shortcuts ──────────────────────────────────────

        public AgentBuilder UseDeepSeek(string? apiKey = null, string model = "deepseek-chat")
            => WithLLM(new LLMWIKYConfig
            {
                Provider = LLMProviderType.DeepSeek,
                ApiKey = apiKey ?? GetEnv("DEEPSEEK_API_KEY"),
                Model = model,
                Endpoint = "https://api.deepseek.com/v1"
            });

        public AgentBuilder UseOpenAI(string? apiKey = null, string model = "gpt-4o")
            => WithLLM(new LLMWIKYConfig
            {
                Provider = LLMProviderType.OpenAI,
                ApiKey = apiKey ?? GetEnv("OPENAI_API_KEY"),
                Model = model
            });

        public AgentBuilder UseAnthropic(string? apiKey = null, string model = "claude-sonnet-4-20250514")
            => WithLLM(new LLMWIKYConfig
            {
                Provider = LLMProviderType.Anthropic,
                ApiKey = apiKey ?? GetEnv("ANTHROPIC_API_KEY"),
                Model = model
            });

        public AgentBuilder UseGemini(string? apiKey = null, string model = "gemini-2.0-flash")
            => WithLLM(new LLMWIKYConfig
            {
                Provider = LLMProviderType.Gemini,
                ApiKey = apiKey ?? GetEnv("GEMINI_API_KEY"),
                Model = model
            });

        public AgentBuilder UseOllama(string model = "llama3.1", string endpoint = "http://localhost:11434")
            => WithLLM(new LLMWIKYConfig
            {
                Provider = LLMProviderType.Ollama,
                Model = model,
                Endpoint = endpoint
            });

        public AgentBuilder UseGroq(string? apiKey = null, string model = "llama-3.1-70b-versatile")
            => WithLLM(new LLMWIKYConfig
            {
                Provider = LLMProviderType.Groq,
                ApiKey = apiKey ?? GetEnv("GROQ_API_KEY"),
                Model = model
            });

        public AgentBuilder UseMistral(string? apiKey = null, string model = "mistral-large-latest")
            => WithLLM(new LLMWIKYConfig
            {
                Provider = LLMProviderType.Mistral,
                ApiKey = apiKey ?? GetEnv("MISTRAL_API_KEY"),
                Model = model
            });

        public AgentBuilder UseModel(LLMProviderType provider, string model, string? apiKey = null, string? endpoint = null)
            => WithLLM(new LLMWIKYConfig
            {
                Provider = provider,
                Model = model,
                ApiKey = apiKey,
                Endpoint = endpoint
            });

        /// <summary>Auto-configure from environment variables. Picks first available provider.</summary>
        public AgentBuilder AutoDetectProvider()
        {
            if (GetEnv("DEEPSEEK_API_KEY") is { } dk)   return UseDeepSeek(dk);
            if (GetEnv("OPENAI_API_KEY") is { } ok)     return UseOpenAI(ok);
            if (GetEnv("ANTHROPIC_API_KEY") is { } ak)  return UseAnthropic(ak);
            if (GetEnv("GEMINI_API_KEY") is { } gk)     return UseGemini(gk);
            if (GetEnv("GROQ_API_KEY") is { } grk)      return UseGroq(grk);
            if (GetEnv("MISTRAL_API_KEY") is { } mk)    return UseMistral(mk);
            // Fallback: Ollama (no key needed)
            return UseOllama();
        }

        // ─── Agent Configuration ─────────────────────────────────────────

        public AgentBuilder WithSystemPrompt(string prompt) { _systemPrompt = prompt; return this; }
        public AgentBuilder WithMaxIterations(int n) { _maxIterations = n; return this; }
        public AgentBuilder WithAutoLoops(int upperBound = 50) { _maxIterations = upperBound; _maxIterationsMode = MaxIterationsMode.Auto; return this; }
        public AgentBuilder WithInfiniteLoops(int maxSafe = 1000) { _maxIterations = maxSafe; _maxIterationsMode = MaxIterationsMode.Infinite; return this; }
        public AgentBuilder WithTimeout(TimeSpan t) { _timeout = t; return this; }
        public AgentBuilder WithTool(IAgxiomeTool tool) { _tools.Add(tool); return this; }
        public AgentBuilder WithLogging(ILoggerFactory lf) { _loggerFactory = lf; return this; }

        private AgentBuilder WithLLM(LLMWIKYConfig c) { _llmConfig = c; return this; }

        public IAgxiomeAgent Build()
        {
            if (_loggerFactory is not null) AgentFactory.Configure(_loggerFactory);

            var factory = new LLMProviderFactory();
            var engine = new LLMWIKYEngine(_llmConfig);

            var config = new AgentConfig
            {
                Id = _name,
                SystemPrompt = _systemPrompt,
                MaxIterations = _maxIterations,
                MaxIterationsMode = _maxIterationsMode,
                Timeout = _timeout,
                ReasoningEngine = _llmConfig
            };

            var agent = AgentFactory.CreateWithEngine(config, engine);
            foreach (var tool in _tools) agent.RegisterTool(tool);
            return agent;
        }

        private static string? GetEnv(string key) =>
            Environment.GetEnvironmentVariable(key) is { Length: > 0 } v ? v : null;
    }

    // ─── SwarmBuilder ──────────────────────────────────────────────────────────

    public sealed class SwarmBuilder
    {
        private readonly List<IAgxiomeAgent> _agents = new();
        private SwarmType _swarmType = SwarmType.Sequential;
        private int _layers = 3;
        private SpeakerPolicy _speakerPolicy = SpeakerPolicy.RoundRobin;
        private VotingMode _votingMode = VotingMode.SimpleCount;
        private int _maxLoops = 20;
        private string? _flow;
        private Func<string, IReadOnlyList<IAgxiomeAgent>, CancellationToken, Task<TeamResponse>>? _socialCallable;
        private string? _goal;
        private int _maxAgents = 8;

        internal SwarmBuilder() { }

        public SwarmBuilder AddAgent(string name, Action<AgentBuilder>? configure = null)
        {
            var builder = AgxiomeBuilder.Agent(name);
            configure?.Invoke(builder);
            _agents.Add(builder.Build());
            return this;
        }

        public SwarmBuilder AddAgent(IAgxiomeAgent agent) { _agents.Add(agent); return this; }

        public SwarmBuilder AsMixtureOfAgents(int layers = 3)
        { _swarmType = SwarmType.MixtureOfAgents; _layers = layers; return this; }

        public SwarmBuilder AsGroupChat(int maxLoops = 20, SpeakerPolicy policy = SpeakerPolicy.RoundRobin)
        { _swarmType = SwarmType.GroupChat; _maxLoops = maxLoops; _speakerPolicy = policy; return this; }

        public SwarmBuilder AsHierarchical()
        { _swarmType = SwarmType.Hierarchical; return this; }

        public SwarmBuilder AsMajorityVoting(VotingMode mode = VotingMode.SimpleCount)
        { _swarmType = SwarmType.MajorityVoting; _votingMode = mode; return this; }

        public SwarmBuilder AsPlannerWorker()
        { _swarmType = SwarmType.PlannerWorker; return this; }

        /// <summary>Auto-select swarm type based on agent count and roles.</summary>
        public SwarmBuilder AutoSelect()
        { _swarmType = SwarmType.Auto; return this; }

        /// <summary>AutoSwarm: LLM-driven goal analysis + team generation.</summary>
        public SwarmBuilder AutoSwarm(string goal, int maxAgents = 8)
        { _swarmType = SwarmType.AutoSwarm; _goal = goal; _maxAgents = maxAgents; return this; }

        /// <summary>AgentRearrange: einsum-style flow "A -> B, C".</summary>
        public SwarmBuilder WithRearrangeFlow(string flow)
        { _swarmType = SwarmType.Rearrange; _flow = flow; return this; }

        /// <summary>SocialAlgorithm: custom callable orchestrates team interactions.</summary>
        public SwarmBuilder WithSocialAlgorithm(Func<string, IReadOnlyList<IAgxiomeAgent>, CancellationToken, Task<TeamResponse>> callable)
        { _swarmType = SwarmType.SocialAlgorithm; _socialCallable = callable; return this; }

        public object Build()
        {
            if (_agents.Count == 0) throw new InvalidOperationException("No agents added to swarm.");
            return _swarmType switch
            {
                SwarmType.MixtureOfAgents => SwarmFactory.MixtureOfAgents(_agents, _layers),
                SwarmType.GroupChat => SwarmFactory.GroupChat(_agents, _maxLoops, _speakerPolicy),
                SwarmType.Hierarchical => SwarmFactory.Hierarchical(_agents[0], _agents.Count > 1 ? _agents[1..] : _agents),
                SwarmType.MajorityVoting => SwarmFactory.MajorityVoting(_agents, _votingMode),
                SwarmType.PlannerWorker => SwarmFactory.PlannerWorker(_agents[0], _agents.Count > 1 ? _agents[1..] : _agents),
                SwarmType.Rearrange => new Workflows.AgentRearrange(_flow ?? "A -> B", _agents).BuildDagFromFlow(),
                SwarmType.SocialAlgorithm => BuildSocialTeam(),
                SwarmType.AutoSwarm => BuildAutoSwarmAsync().GetAwaiter().GetResult(),
                SwarmType.Sequential or SwarmType.Auto or _ => SwarmFactory.AutoSwarm("execute", _agents),
            };
        }

        private object BuildSocialTeam()
        {
            var leader = _agents.FirstOrDefault() ?? throw new InvalidOperationException("No agents for SocialAlgorithm");
            var team = new Teams.AgxiomeTeam("social", new Shared.Config.TeamConfig { Mode = TeamMode.SocialAlgorithm }, leader);
            foreach (var a in _agents.Skip(1)) { var _ = team.AddAgent(a.Id, ""); }
            team.SocialAlgorithmCallable = _socialCallable;
            return team;
        }

        private async Task<object> BuildAutoSwarmAsync()
        {
            // Use LLMWIKYEngine from first agent if available, else fallback to keyword-based
            return SwarmFactory.AutoSwarm(_goal ?? "execute", _agents);
        }
    }

    private enum SwarmType { Sequential, MixtureOfAgents, GroupChat, Hierarchical, MajorityVoting, PlannerWorker, Auto, Rearrange, SocialAlgorithm, AutoSwarm }
}

/// <summary>
/// AgxiomeQuickStart — 1-line agent creation for demos and prototypes.
/// Parity with Swarms: Agent(model_name="gpt-4") → zero boilerplate.
/// </summary>
public sealed class QuickStartHelper
{
    public static readonly QuickStartHelper Instance = new();

    /// <summary>Create a ready-to-use agent. ApiKey from env if not provided.</summary>
    public IAgxiomeAgent CreateAgent(
        string providerModel = "deepseek-chat",
        string? apiKey = null,
        string? systemPrompt = null)
    {
        var (provider, model) = ParseProviderModel(providerModel);
        var builder = AgxiomeBuilder.Agent(providerModel)
            .UseModel(provider, model, apiKey)
            .WithMaxIterations(10);

        if (systemPrompt is not null) builder.WithSystemPrompt(systemPrompt);
        return builder.Build();
    }

    private static (LLMProviderType, string) ParseProviderModel(string providerModel) => providerModel.ToLowerInvariant() switch
    {
        var m when m.StartsWith("gpt") => (LLMProviderType.OpenAI, providerModel),
        var m when m.StartsWith("claude") => (LLMProviderType.Anthropic, providerModel),
        var m when m.StartsWith("gemini") => (LLMProviderType.Gemini, providerModel),
        var m when m.StartsWith("deepseek") => (LLMProviderType.DeepSeek, providerModel),
        var m when m.StartsWith("llama") || m.StartsWith("mistral") || m.StartsWith("phi") => (LLMProviderType.Ollama, providerModel),
        _ => (LLMProviderType.Custom, providerModel)
    };
}
