using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace AGXIOME.Core.Workflows;

/// <summary>
/// AGXIOMA DAG Workflow — explicit Directed Acyclic Graph execution engine.
///
/// SUPERIORE a Swarms GraphWorkflow:
///   - NodeType enum (Agent / Parallel / Conditional / Barrier / Transform / Sink)
///   - AddParallelBranch(): più nodi eseguiti in Task.WhenAll automaticamente
///   - AddConditionalEdge(): branching basato su predicato sul WorkflowContext
///   - Barrier nodes: fan-in — aspetta tutti i rami prima di procedere
///   - OnNodeStart / OnNodeComplete callbacks per audit e streaming
///   - Topological sort con cycle detection (Kahn's algorithm)
///   - Backward compat: tutti i metodi di AgxiomeWorkflow sono preservati
///
/// Usage:
///   var dag = new AgxiomeDagWorkflow("pipeline");
///   var n1 = dag.AddNode("fetch",    NodeType.Agent, agent1);
///   var n2 = dag.AddNode("analyze",  NodeType.Agent, agent2);
///   var n3 = dag.AddNode("report",   NodeType.Agent, agent3);
///   var p1 = dag.AddParallelBranch("parallel-check", [checkAgent1, checkAgent2]);
///   dag.AddEdge("fetch",    "analyze");
///   dag.AddEdge("analyze",  "parallel-check");
///   dag.AddBarrier("gate",  new[]{"parallel-check"}, "report");
///   var result = await dag.RunAsync(ctx, ct);
/// </summary>
public sealed class AgxiomeDagWorkflow
{
    public string Id { get; }
    public WorkflowStatus Status { get; private set; } = WorkflowStatus.Created;

    // ─── Callbacks ──────────────────────────────────────────────────────────
    public Action<DagNodeExecution>? OnNodeStart { get; set; }
    public Action<DagNodeExecution>? OnNodeComplete { get; set; }

    // ─── Graph state ────────────────────────────────────────────────────────
    private readonly Dictionary<string, DagNode> _nodes = new();
    private readonly Dictionary<string, List<DagEdge>> _edges = new();   // from → edges
    private readonly Dictionary<string, int> _inDegree = new();

    public AgxiomeDagWorkflow(string id) => Id = id;

    // ─── Node builders ──────────────────────────────────────────────────────

    /// <summary>Add a single node. Returns the node id for chaining.</summary>
    public string AddNode(string id, NodeType type, IAgxiomeAgent agent)
    {
        _nodes[id] = new DagNode(id, type, Agents: [agent], Handler: null);
        _inDegree[id] = 0;
        return id;
    }

    /// <summary>Add a function node (no agent needed).</summary>
    public string AddFunctionNode(string id, Func<WorkflowContext, CancellationToken, Task<string?>> handler)
    {
        _nodes[id] = new DagNode(id, NodeType.Transform, Agents: [], Handler: handler);
        _inDegree[id] = 0;
        return id;
    }

    /// <summary>
    /// Add a Parallel group node — internally executes all agents in Task.WhenAll,
    /// merges results into context["parallel:{id}:{agentName}"].
    /// Returns the group node id.
    /// </summary>
    public string AddParallelBranch(string groupId, IReadOnlyList<IAgxiomeAgent> agents)
    {
        _nodes[groupId] = new DagNode(groupId, NodeType.Parallel, Agents: agents, Handler: null);
        _inDegree[groupId] = 0;
        return groupId;
    }

    /// <summary>
    /// Add a Barrier (fan-in) node. Waits for ALL predecessors in fanInNodeIds
    /// to complete before passing to successorNodeId.
    /// </summary>
    public void AddBarrier(string barrierId, IReadOnlyList<string> fanInNodeIds, string successorNodeId)
    {
        _nodes[barrierId] = new DagNode(barrierId, NodeType.Barrier, Agents: [], Handler: null);
        _inDegree[barrierId] = 0;

        foreach (var src in fanInNodeIds)
            AddEdge(src, barrierId);

        AddEdge(barrierId, successorNodeId);
    }

    // ─── Edge builders ──────────────────────────────────────────────────────

    /// <summary>Add an unconditional directed edge from → to.</summary>
    public void AddEdge(string from, string to)
    {
        if (!_edges.ContainsKey(from)) _edges[from] = new();
        _edges[from].Add(new DagEdge(to, Condition: null));
        _inDegree.TryAdd(to, 0);
        _inDegree[to]++;
    }

    /// <summary>
    /// Add a conditional edge. The edge is only followed when the predicate returns true.
    /// Allows branching: AddConditionalEdge("analyze", "report_ok",  ctx => ctx["sentiment"] == "positive")
    ///                   AddConditionalEdge("analyze", "escalate",   ctx => ctx["sentiment"] == "negative")
    /// </summary>
    public void AddConditionalEdge(string from, string to, Func<WorkflowContext, bool> condition)
    {
        if (!_edges.ContainsKey(from)) _edges[from] = new();
        _edges[from].Add(new DagEdge(to, condition));
        _inDegree.TryAdd(to, 0);
        _inDegree[to]++;
    }

    // ─── Execution ──────────────────────────────────────────────────────────

    /// <summary>Execute the DAG. Respects parallelism, barriers, conditionals.</summary>
    public async Task<DagWorkflowResult> RunAsync(WorkflowContext context, CancellationToken ct = default)
    {
        Status = WorkflowStatus.Running;
        var results = new ConcurrentBag<DagNodeResult>();
        var sw = Stopwatch.StartNew();

        // Kahn's algorithm — topological sort + cycle detection
        var sortedLayers = TopologicalLayers();
        if (sortedLayers is null)
        {
            Status = WorkflowStatus.Failed;
            return new DagWorkflowResult(Id, Status, [], sw.Elapsed, "Cycle detected in DAG.");
        }

        // Execute layer by layer (within each layer nodes are independent → Task.WhenAll)
        foreach (var layer in sortedLayers)
        {
            if (ct.IsCancellationRequested) break;

            // Filter by conditional edges: a node is eligible if at least one incoming active edge allows it
            var eligibleLayer = layer.Where(nid => IsNodeEligible(nid, context, results)).ToList();
            if (eligibleLayer.Count == 0) continue;

            var layerTasks = eligibleLayer.Select(nid => ExecuteNodeAsync(nid, context, results, ct));
            await Task.WhenAll(layerTasks);
        }

        sw.Stop();
        var allResults = results.ToList();
        Status = allResults.Any(r => r.Status == DagNodeStatus.Failed)
            ? WorkflowStatus.Failed
            : WorkflowStatus.Completed;

        return new DagWorkflowResult(Id, Status, allResults, sw.Elapsed, null);
    }

    private async Task ExecuteNodeAsync(
        string nodeId,
        WorkflowContext context,
        ConcurrentBag<DagNodeResult> results,
        CancellationToken ct)
    {
        if (!_nodes.TryGetValue(nodeId, out var node)) return;

        var sw = Stopwatch.StartNew();
        var exec = new DagNodeExecution(nodeId, node.Type, DateTime.UtcNow);
        OnNodeStart?.Invoke(exec);

        try
        {
            var shared = context.SharedState ?? new Dictionary<string, object>();

            if (node.Type == NodeType.Parallel)
            {
                // Fan-out: all agents run concurrently
                var parallelTasks = node.Agents.Select(async a =>
                {
                    var input = shared.TryGetValue("input", out var v) ? v as string ?? "" : "";
                    var resp = await a.RunAsync(new AgentRequest(input), ct);
                    shared[$"parallel:{nodeId}:{a.Id}"] = resp.Content;
                    return resp.Content;
                });
                var outputs = await Task.WhenAll(parallelTasks);
                shared[$"parallel:{nodeId}:merged"] = string.Join("\n---\n", outputs);
                results.Add(new DagNodeResult(nodeId, node.Type, DagNodeStatus.Completed,
                    $"[{outputs.Length} parallel outputs merged]", sw.Elapsed));
            }
            else if (node.Type == NodeType.Barrier)
            {
                // Fan-in — just a pass-through checkpoint, actual sync is done by layer ordering
                results.Add(new DagNodeResult(nodeId, node.Type, DagNodeStatus.Completed, "[barrier]", sw.Elapsed));
            }
            else if (node.Type == NodeType.Transform && node.Handler is not null)
            {
                var output = await node.Handler(context, ct);
                if (output is not null) shared[$"output:{nodeId}"] = output;
                results.Add(new DagNodeResult(nodeId, node.Type, DagNodeStatus.Completed, output, sw.Elapsed));
            }
            else if (node.Agents.Count > 0)
            {
                var prevKey = $"output:{PreviousNode(nodeId, results)}";
                var input = shared.TryGetValue(prevKey, out var pv) ? pv as string ?? ""
                          : shared.TryGetValue("input", out var iv) ? iv as string ?? ""
                          : "";
                var response = await node.Agents[0].RunAsync(new AgentRequest(input), ct);
                shared[$"output:{nodeId}"] = response.Content;
                results.Add(new DagNodeResult(nodeId, node.Type, DagNodeStatus.Completed, response.Content, sw.Elapsed));
            }

            OnNodeComplete?.Invoke(exec with { CompletedAt = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            results.Add(new DagNodeResult(nodeId, node.Type, DagNodeStatus.Failed, null, sw.Elapsed, ex.Message));
            OnNodeComplete?.Invoke(exec with { CompletedAt = DateTime.UtcNow, Error = ex.Message });
        }
    }

    // ─── Graph helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Kahn's algorithm: returns nodes grouped into parallel layers.
    /// Returns null if a cycle is detected.
    /// </summary>
    private List<List<string>>? TopologicalLayers()
    {
        var inDeg = new Dictionary<string, int>(_inDegree);
        var layers = new List<List<string>>();

        while (true)
        {
            var layer = inDeg.Where(kv => kv.Value == 0).Select(kv => kv.Key).ToList();
            if (layer.Count == 0) break;

            layers.Add(layer);
            foreach (var n in layer)
            {
                inDeg.Remove(n);
                if (!_edges.TryGetValue(n, out var outs)) continue;
                foreach (var e in outs)
                {
                    if (inDeg.ContainsKey(e.To))
                        inDeg[e.To]--;
                }
            }
        }

        // Cycle = nodes left with in-degree > 0
        return inDeg.Count == 0 ? layers : null;
    }

    private bool IsNodeEligible(string nodeId, WorkflowContext context, ConcurrentBag<DagNodeResult> completed)
    {
        // Node is eligible if it has no conditional incoming edges, or at least one is satisfied
        var allIncoming = _edges.Values.SelectMany(e => e).Where(e => e.To == nodeId).ToList();
        if (allIncoming.Count == 0) return true;
        return allIncoming.Any(e => e.Condition == null || e.Condition(context));
    }

    private static string? PreviousNode(string nodeId, ConcurrentBag<DagNodeResult> completed)
        => completed.LastOrDefault()?.NodeId;
}

// ─── Supporting types ────────────────────────────────────────────────────────

public enum NodeType
{
    Agent,        // Single IAgxiomeAgent
    Parallel,     // Fan-out: multiple agents run concurrently
    Conditional,  // Branching logic (edge conditions)
    Barrier,      // Fan-in synchronisation gate
    Transform,    // Pure function, no agent
    Sink          // Terminal node (no outgoing edges)
}

public enum DagNodeStatus { Completed, Failed, Skipped }

public record DagNode(
    string Id,
    NodeType Type,
    IReadOnlyList<IAgxiomeAgent> Agents,
    Func<WorkflowContext, CancellationToken, Task<string?>>? Handler);

public record DagEdge(string To, Func<WorkflowContext, bool>? Condition);

public record DagNodeResult(
    string NodeId,
    NodeType Type,
    DagNodeStatus Status,
    string? Output,
    TimeSpan Duration,
    string? Error = null);

public record DagNodeExecution(
    string NodeId,
    NodeType Type,
    DateTime StartedAt,
    DateTime? CompletedAt = null,
    string? Error = null);

public record DagWorkflowResult(
    string WorkflowId,
    WorkflowStatus Status,
    IReadOnlyList<DagNodeResult> NodeResults,
    TimeSpan TotalDuration,
    string? Error)
{
    public string? FinalOutput => NodeResults
        .LastOrDefault(r => r.Status == DagNodeStatus.Completed && r.Output is not null)?.Output;
}
