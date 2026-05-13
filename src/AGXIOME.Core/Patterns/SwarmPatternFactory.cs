using AGXIOME.Core.Patterns;
using AGXIOME.Core.Routing;
using AGXIOME.Shared.Interfaces;

namespace AGXIOME.Core.Patterns;

/// <summary>
/// AGXIOMA SwarmPatternFactory — create any swarm architecture in 1 line.
///
/// This is the zero-boilerplate entry point to all AGXIOMA swarm patterns.
/// Inspired by Swarms' AutoSwarmBuilder but more structured and type-safe.
///
/// Examples:
///   var moa = SwarmFactory.MixtureOfAgents(agents, layers: 3);
///   var chat = SwarmFactory.GroupChat(agents, policy: SpeakerPolicy.Priority);
///   var hier = SwarmFactory.Hierarchical(director, workers, evaluator);
///   var vote = SwarmFactory.MajorityVoting(agents, mode: VotingMode.LLMJudge, judge);
///   var pw   = SwarmFactory.PlannerWorker(planner, workers, evaluator);
/// </summary>
public static class SwarmFactory
{
    // ─── Mixture of Agents ────────────────────────────────────────────────────

    public static MixtureOfAgentsSwarm MixtureOfAgents(
        IReadOnlyList<IAgxiomeAgent> agents,
        int layers = 3,
        IAgxiomeAgent? finalAgent = null,
        LayerStrategy layerStrategy = LayerStrategy.Parallel,
        AggregationMode aggregation = AggregationMode.CriticSynth,
        Action<int, MoALayerResult>? onLayerComplete = null)
        => new(agents, new MoAConfig
        {
            Layers = layers,
            LayerStrategy = layerStrategy,
            AggregationMode = aggregation,
            OnLayerComplete = onLayerComplete
        }, finalAgent);

    // ─── Group Chat ───────────────────────────────────────────────────────────

    public static GroupChatSwarm GroupChat(
        IReadOnlyList<IAgxiomeAgent> agents,
        int maxLoops = 20,
        SpeakerPolicy policy = SpeakerPolicy.RoundRobin,
        Dictionary<string, int>? priorityWeights = null,
        string? summariserAgentId = null,
        Action<int, ChatTurn>? onTurn = null)
        => new(agents, new GroupChatConfig
        {
            MaxLoops = maxLoops,
            SpeakerPolicy = policy,
            PriorityWeights = priorityWeights ?? new(),
            SummariserAgentId = summariserAgentId,
            OnTurn = onTurn
        });

    // ─── Hierarchical ─────────────────────────────────────────────────────────

    public static HierarchicalSwarm Hierarchical(
        IAgxiomeAgent director,
        IReadOnlyList<IAgxiomeAgent> workers,
        IAgxiomeAgent? evaluator = null,
        bool parallel = true,
        int maxFeedbackRounds = 2,
        Action<SubtaskPlan, string, bool>? onSubtaskComplete = null)
        => new(director, workers, new HierarchicalConfig
        {
            ParallelExecution = parallel,
            MaxFeedbackRounds = maxFeedbackRounds,
            OnSubtaskComplete = onSubtaskComplete
        }, evaluator);

    // ─── Majority Voting ─────────────────────────────────────────────────────

    public static MajorityVotingSwarm MajorityVoting(
        IReadOnlyList<IAgxiomeAgent> agents,
        VotingMode mode = VotingMode.SimpleCount,
        IAgxiomeAgent? judgeAgent = null,
        double confidenceThreshold = 0.5,
        DisagreementPolicy disagreementPolicy = DisagreementPolicy.ForceBest,
        Action<VotingResult>? onVoteComplete = null)
        => new(agents, new MajorityVotingConfig
        {
            VotingMode = mode,
            ConfidenceThreshold = confidenceThreshold,
            DisagreementPolicy = disagreementPolicy,
            OnVoteComplete = onVoteComplete
        }, judgeAgent);

    // ─── Planner / Worker ─────────────────────────────────────────────────────

    public static PlannerWorkerSwarm PlannerWorker(
        IAgxiomeAgent planner,
        IReadOnlyList<IAgxiomeAgent> workers,
        IAgxiomeAgent? evaluator = null,
        bool parallelWorkers = true,
        Action<WorkerTaskResult>? onTaskComplete = null)
        => new(planner, workers, new PlannerWorkerConfig
        {
            ParallelWorkers = parallelWorkers,
            OnTaskComplete = onTaskComplete
        }, evaluator);

    // ─── Model Router ─────────────────────────────────────────────────────────

    public static AgxiomeModelRouter ModelRouter(
        IReadOnlyList<RouterEntry> providers,
        RoutingStrategy strategy = RoutingStrategy.Hybrid,
        bool fallback = true,
        Action<string, string, TimeSpan>? onRoute = null)
        => new(providers, new ModelRouterConfig
        {
            DefaultStrategy = strategy,
            TryFallback = fallback,
            OnRoute = onRoute
        });

    // ─── AutoSwarm (zero-config) ──────────────────────────────────────────────

    /// <summary>
    /// AutoSwarm: given a goal string and a list of agents, automatically chooses
    /// the best swarm architecture based on agent count and goal keywords.
    ///
    /// Goal keywords:
    ///   - "vote", "best answer", "consensus" → MajorityVoting
    ///   - "debate", "discuss", "conversation" → GroupChat
    ///   - "plan", "execute", "workflow" → PlannerWorker (first agent = planner)
    ///   - "synthesise", "combine", "aggregate" → MixtureOfAgents
    ///   - default (2+ agents) → Hierarchical (first agent = director)
    /// </summary>
    public static object AutoSwarm(string goal, IReadOnlyList<IAgxiomeAgent> agents)
    {
        if (agents.Count == 0) throw new ArgumentException("AutoSwarm requires at least 1 agent.");

        var lower = goal.ToLowerInvariant();

        if (lower.Contains("vote") || lower.Contains("best answer") || lower.Contains("consensus"))
            return MajorityVoting(agents);

        if (lower.Contains("debate") || lower.Contains("discuss") || lower.Contains("conversation") || lower.Contains("chat"))
            return GroupChat(agents);

        if (lower.Contains("plan") || lower.Contains("execute") || lower.Contains("workflow") || lower.Contains("task"))
            return PlannerWorker(agents[0], agents.Count > 1 ? agents.Skip(1).ToList() : agents);

        if (lower.Contains("synthes") || lower.Contains("combine") || lower.Contains("aggregate") || lower.Contains("mixture"))
            return MixtureOfAgents(agents);

        // Default: Hierarchical with first agent as director
        return agents.Count > 1
            ? (object)Hierarchical(agents[0], agents.Skip(1).ToList())
            : MixtureOfAgents(agents, layers: 1);
    }
}
