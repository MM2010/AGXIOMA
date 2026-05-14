using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Core.Workflows;

/// <summary>
/// AgentRearrange — einsum-style flow parser that builds DAG execution plans.
/// Syntax: "A -> B, C" means A runs first, then B and C in parallel.
/// Syntax: "A -> B -> C" means sequential A → B → C.
/// Syntax: "A, B -> C" means A and B in parallel, then C.
/// Parity with Swarms: AgentRearrange(flow, agents).
/// </summary>
public sealed class AgentRearrange
{
    private readonly Dictionary<string, IAgxiomeAgent> _agentMap;
    private readonly string _flow;

    public AgentRearrange(string flow, IReadOnlyList<IAgxiomeAgent> agents)
    {
        _flow = flow ?? throw new ArgumentNullException(nameof(flow));
        _agentMap = agents?.ToDictionary(a => a.Id, a => a)
                    ?? throw new ArgumentNullException(nameof(agents));
    }

    /// <summary>Parse the flow string and build an executable DAG workflow.</summary>
    public AgxiomeDagWorkflow BuildDagFromFlow()
    {
        var dag = new AgxiomeDagWorkflow(_flow);
        var stages = FlowParser.Parse(_flow);

        string? previousNodeId = null;

        foreach (var stage in stages)
        {
            if (stage.Count == 1)
            {
                var nodeId = stage[0];
                if (_agentMap.TryGetValue(nodeId, out var agent))
                    dag.AddNode(nodeId, NodeType.Agent, agent);
                else
                    dag.AddFunctionNode(nodeId, async (ctx, ct) =>
                    {
                        return $"Agent '{nodeId}' not found";
                    });

                if (previousNodeId != null)
                    dag.AddEdge(previousNodeId, nodeId);
                previousNodeId = nodeId;
            }
            else
            {
                // Parallel branch: collect agents for this stage
                var branchAgents = stage
                    .Select(name => _agentMap.TryGetValue(name, out var a) ? a : null)
                    .Where(a => a != null)
                    .Cast<IAgxiomeAgent>()
                    .ToList();

                var branchId = $"parallel_{string.Join("_", stage)}";
                if (branchAgents.Count > 0)
                    dag.AddParallelBranch(branchId, branchAgents);

                // Connect previous stage
                if (previousNodeId != null)
                    dag.AddEdge(previousNodeId, branchId);

                // Barrier after parallel branch
                var barrierId = $"barrier_{string.Join("_", stage)}";
                dag.AddBarrier(barrierId, new List<string> { branchId }, previousNodeId ?? branchId);
                previousNodeId = barrierId;
            }
        }

        return dag;
    }
}

/// <summary>Flow string parser — converts "A -> B, C" into execution stages.</summary>
public static class FlowParser
{
    /// <summary>
    /// Parse an einsum-style flow into ordered stages.
    /// Each stage is a list of agent names that execute together (parallel).
    /// Stages are separated by "->".
    /// Agents within a stage are separated by ",".
    /// </summary>
    public static List<List<string>> Parse(string flow)
    {
        if (string.IsNullOrWhiteSpace(flow))
            throw new ArgumentException("Flow cannot be empty", nameof(flow));

        var stages = new List<List<string>>();

        // Split by "->" to get stages
        var stageTokens = flow.Split("->", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var stage in stageTokens)
        {
            if (string.IsNullOrWhiteSpace(stage)) continue;

            var agents = stage.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                              .Select(a => a.Trim())
                              .Where(a => !string.IsNullOrEmpty(a))
                              .ToList();

            if (agents.Count > 0)
                stages.Add(agents);
        }

        if (stages.Count == 0)
            throw new ArgumentException($"Flow '{flow}' produced no valid stages");

        return stages;
    }
}
