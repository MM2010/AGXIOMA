using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Core.Patterns;

/// <summary>
/// ForestSwarm — tree-based agent routing pattern.
/// Organizes agents into named trees; routes tasks to the most semantically
/// relevant tree and agent using keyword-overlap scoring.
///
/// Parity with Swarms Python: ForestSwarm(trees=[Tree(agents=[TreeAgent(...)])]).
///
/// Routing strategy:
///   1. For each tree, score each agent: count keyword overlaps between task
///      and the agent's keyword set (case-insensitive).
///   2. Pick the tree/agent with the highest score (ties broken by agent order).
///   3. If no overlap at all, fall back to the first agent of the first tree.
/// </summary>
public sealed class ForestSwarm
{
    private readonly ForestSwarmConfig _config;
    private readonly IReadOnlyList<ForestTree> _trees;

    public string Name        { get; }
    public string Description { get; }
    public IReadOnlyList<ForestTree> Trees => _trees;

    public ForestSwarm(
        ForestSwarmConfig config,
        IReadOnlyList<ForestTree> trees,
        string name = "forest-swarm",
        string description = "Multi-tree agent routing swarm")
    {
        _config     = config ?? throw new ArgumentNullException(nameof(config));
        _trees      = trees  ?? throw new ArgumentNullException(nameof(trees));
        Name        = name;
        Description = description;

        if (_trees.Count == 0) throw new ArgumentException("ForestSwarm requires at least one tree.", nameof(trees));
    }

    /// <summary>
    /// Route task to the best matching agent and execute it.
    /// </summary>
    public async Task<ForestSwarmResult> RunAsync(string task, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var (tree, agent, matchedKeywords) = FindBestMatch(task);

        var response = await agent.Agent.RunAsync(new AgentRequest(task), ct);

        sw.Stop();
        return new ForestSwarmResult(
            task,
            tree.TreeName,
            agent.AgentId,
            response.Content,
            matchedKeywords,
            sw.Elapsed
        );
    }

    /// <summary>
    /// Find the tree and agent that best match the task (no LLM call — pure keyword scoring).
    /// </summary>
    public (ForestTree Tree, ForestAgentEntry Agent, IReadOnlyList<string> MatchedKeywords) FindBestMatch(string task)
    {
        var taskTokens = Tokenize(task);

        ForestTree? bestTree = null;
        ForestAgentEntry? bestAgent = null;
        List<string> bestMatches = new();
        int bestScore = -1;

        foreach (var tree in _trees)
        {
            foreach (var agent in tree.Agents)
            {
                var agentTokens = agent.Keywords
                    .Select(k => k.ToLowerInvariant())
                    .ToHashSet();

                var overlapping = taskTokens.Intersect(agentTokens).ToList();
                int score = overlapping.Count;

                if (score > bestScore)
                {
                    bestScore  = score;
                    bestTree   = tree;
                    bestAgent  = agent;
                    bestMatches = overlapping;
                }
            }
        }

        // Fallback: first agent of first tree
        bestTree  ??= _trees[0];
        bestAgent ??= _trees[0].Agents[0];

        return (bestTree, bestAgent, bestMatches.AsReadOnly());
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static HashSet<string> Tokenize(string text)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var span = text.AsSpan();
        int start = 0;
        for (int i = 0; i <= span.Length; i++)
        {
            if (i == span.Length || !char.IsLetterOrDigit(span[i]))
            {
                if (i > start)
                {
                    var token = span[start..i].ToString().ToLowerInvariant();
                    if (token.Length > 2) // skip noise words shorter than 3 chars
                        tokens.Add(token);
                }
                start = i + 1;
            }
        }
        return tokens;
    }
}

// ─── Supporting types ─────────────────────────────────────────────────────────

/// <summary>A named tree of agents within a ForestSwarm.</summary>
public sealed class ForestTree
{
    public string TreeName { get; }
    public IReadOnlyList<ForestAgentEntry> Agents { get; }

    public ForestTree(string treeName, IReadOnlyList<ForestAgentEntry> agents)
    {
        if (string.IsNullOrWhiteSpace(treeName)) throw new ArgumentException("TreeName is required.", nameof(treeName));
        if (agents == null || agents.Count == 0) throw new ArgumentException("Tree requires at least one agent.", nameof(agents));
        TreeName = treeName;
        Agents   = agents;
    }
}

/// <summary>An agent registered in a ForestTree with associated routing keywords.</summary>
public sealed class ForestAgentEntry
{
    public string            AgentId     { get; }
    public string            Description { get; }
    public IReadOnlyList<string> Keywords { get; }
    public IAgxiomeAgent     Agent       { get; }

    /// <param name="agentId">Unique identifier for the agent slot in this tree.</param>
    /// <param name="description">Human-readable description of the agent's specialty.</param>
    /// <param name="keywords">
    ///   Words/phrases used to route tasks here. Case-insensitive.
    ///   Example: ["finance", "budget", "investment", "revenue"].
    /// </param>
    /// <param name="agent">The underlying agent implementation.</param>
    public ForestAgentEntry(
        string agentId,
        string description,
        IReadOnlyList<string> keywords,
        IAgxiomeAgent agent)
    {
        if (string.IsNullOrWhiteSpace(agentId)) throw new ArgumentException("AgentId is required.", nameof(agentId));
        AgentId     = agentId;
        Description = description ?? string.Empty;
        Keywords    = keywords ?? Array.Empty<string>();
        Agent       = agent    ?? throw new ArgumentNullException(nameof(agent));
    }
}

/// <summary>Configuration for a ForestSwarm.</summary>
public record ForestSwarmConfig
{
    /// <summary>Minimum keyword overlap score to consider a match (default 0 = always match).</summary>
    public int MinMatchScore { get; init; } = 0;
}
