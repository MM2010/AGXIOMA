using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using AGXIOME.LlmWiky;
using static AGXIOME.Core.Patterns.SwarmFactory;

namespace AGXIOME.Core.Patterns;

/// <summary>
/// AutoSwarmBuilder — LLM-driven goal analysis and team generation.
/// Given a textual goal, uses the LLM to decide the optimal swarm topology,
/// generate agent specs, and instantiate the team.
/// Parity with Swarms: AutoSwarm(goal, agents).
/// </summary>
public sealed class AutoSwarmBuilder
{
    private readonly ILLMWIKYEngine _engine;
    private readonly string _goal;
    private readonly int _maxAgents;
    private readonly List<IAgxiomeAgent> _customAgents = new();

    public AutoSwarmBuilder(ILLMWIKYEngine engine, string goal, int maxAgents = 8)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _goal = goal ?? throw new ArgumentNullException(nameof(goal));
        _maxAgents = Math.Clamp(maxAgents, 1, 16);
    }

    /// <summary>Optionally seed with pre-built agents instead of generating them.</summary>
    public AutoSwarmBuilder WithAgents(IReadOnlyList<IAgxiomeAgent> agents)
    {
        _customAgents.AddRange(agents);
        return this;
    }

    /// <summary>Analyze goal and build the swarm. Returns the constructed team object.</summary>
    public async Task<object> BuildAsync(CancellationToken ct = default)
    {
        // Step 1: LLM analyzes goal → structured plan
        var analysis = await AnalyzeGoalAsync(ct);

        // Step 2: Generate agents if none provided
        var agents = _customAgents.Count > 0
            ? _customAgents
            : await GenerateAgentsAsync(analysis, ct);

        // Step 3: Choose pattern and instantiate
        return InstantiateSwarm(analysis, agents);
    }

    private async Task<AutoSwarmPlan> AnalyzeGoalAsync(CancellationToken ct)
    {
        var prompt = $@"You are an expert swarm architect. Analyze this goal and return a JSON plan.

Goal: {_goal}

Return valid JSON only (no markdown):
{{
  ""pattern"": ""mixture_of_agents | group_chat | hierarchical | majority_voting | planner_worker | sequential"",
  ""agentCount"": <int 1-{_maxAgents}>,
  ""agents"": [
    {{ ""name"": ""short_name"", ""role"": ""role description"", ""systemPrompt"": ""detailed system prompt"" }}
  ],
  ""reasoning"": ""one-line explanation""
}}";

        var request = new LLMWIKYRequest(
            "You output ONLY valid JSON. No markdown, no explanation.",
            prompt,
            Temperature: 0.3,
            MaxTokens: 2048
        );

        var response = await _engine.InferAsync(request, ct);
        return ParsePlan(response.Content);
    }

    private static AutoSwarmPlan ParsePlan(string json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            return new AutoSwarmPlan(
                root.GetProperty("pattern").GetString() ?? "sequential",
                root.GetProperty("agentCount").GetInt32(),
                root.TryGetProperty("agents", out var agents)
                    ? agents.EnumerateArray().Select(a => new AgentSpec(
                        a.GetProperty("name").GetString() ?? "agent",
                        a.GetProperty("role").GetString() ?? "",
                        a.GetProperty("systemPrompt").GetString() ?? ""
                    )).ToList()
                    : new List<AgentSpec>(),
                root.GetProperty("reasoning").GetString()
            );
        }
        catch
        {
            return new AutoSwarmPlan("sequential", 1,
                new List<AgentSpec> { new("agent", "generalist", "You are a helpful assistant.") },
                "Fallback: sequential single-agent");
        }
    }

    private async Task<List<IAgxiomeAgent>> GenerateAgentsAsync(AutoSwarmPlan plan, CancellationToken ct)
    {
        var agents = new List<IAgxiomeAgent>();
        foreach (var spec in plan.Agents.Take(plan.AgentCount))
        {
            var agent = Agent.AgentFactory.CreateTestAgent(spec.Name);
            // Note: in production, the caller should configure LLM on each agent
            agents.Add(agent);
        }
        if (agents.Count == 0)
            agents.Add(Agent.AgentFactory.CreateTestAgent("default"));
        return agents;
    }

    private static object InstantiateSwarm(AutoSwarmPlan plan, List<IAgxiomeAgent> agents)
    {
        return plan.Pattern.ToLowerInvariant() switch
        {
            "mixture_of_agents" => MixtureOfAgents(agents, layers: 2),
            "group_chat" => GroupChat(agents, maxLoops: 20),
            "hierarchical" when agents.Count >= 2 => Hierarchical(agents[0], agents.Skip(1).ToList()),
            "majority_voting" => MajorityVoting(agents),
            "planner_worker" when agents.Count >= 2 => PlannerWorker(agents[0], agents.Skip(1).ToList()),
            _ => AutoSwarm(plan.Reasoning ?? plan.Pattern, agents)
        };
    }
}

/// <summary>Result of LLM goal analysis.</summary>
public record AutoSwarmPlan(string Pattern, int AgentCount, List<AgentSpec> Agents, string? Reasoning);

/// <summary>Specification for a single agent in the swarm.</summary>
public record AgentSpec(string Name, string Role, string SystemPrompt);
