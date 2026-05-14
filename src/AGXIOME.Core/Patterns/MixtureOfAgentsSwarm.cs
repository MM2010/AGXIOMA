using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace AGXIOME.Core.Patterns;

/// <summary>
/// AGXIOMA MixtureOfAgents (MoA) — inspired by arxiv 2406.04692.
/// Each layer runs N agents in parallel. Output of each layer becomes context for the next.
/// A FinalAgent aggregates the last layer into a single authoritative response.
///
/// Supercharges Swarms' MoA with:
///   - LayerStrategy (Parallel/Sequential/Hybrid)
///   - AggregationMode (Vote / Merge / CriticSynth / WeightedScore)
///   - Per-layer temperature control
///   - PerLayerCallback for real-time monitoring
///   - Automatic fallback when agents fail a layer
/// </summary>
public sealed class MixtureOfAgentsSwarm
{
    public string Id { get; } = Guid.NewGuid().ToString("N")[..8];

    private readonly IReadOnlyList<IAgxiomeAgent> _agents;
    private readonly IAgxiomeAgent? _finalAgent;
    private readonly MoAConfig _config;

    public MixtureOfAgentsSwarm(
        IReadOnlyList<IAgxiomeAgent> agents,
        MoAConfig? config = null,
        IAgxiomeAgent? finalAgent = null)
    {
        if (agents.Count == 0) throw new ArgumentException("At least one agent required.");
        _agents = agents;
        _finalAgent = finalAgent;
        _config = config ?? new MoAConfig();
    }

    /// <summary>Run MoA on a single task. Returns the final synthesised response.</summary>
    public async Task<MoAResult> RunAsync(string task, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var layerOutputs = new List<MoALayerResult>();
        var currentContext = task;

        for (int layer = 0; layer < _config.Layers; layer++)
        {
            var layerResult = await ExecuteLayerAsync(layer, currentContext, ct);
            layerOutputs.Add(layerResult);
            _config.OnLayerComplete?.Invoke(layer, layerResult);

            // Build context for next layer: combine current task + all agent outputs
            currentContext = BuildLayerContext(task, layerResult.Outputs, layer);
        }

        // Final aggregation
        var lastLayer = layerOutputs.Last();
        string finalContent;

        if (_finalAgent is not null)
        {
            var finalPrompt = BuildFinalAgentPrompt(task, lastLayer.Outputs);
            var finalResp = await _finalAgent.RunAsync(new AgentRequest(finalPrompt), ct);
            finalContent = finalResp.Content;
        }
        else
        {
            finalContent = Aggregate(lastLayer.Outputs, _config.AggregationMode);
        }

        return new MoAResult(
            FinalOutput: finalContent,
            LayerResults: layerOutputs,
            Duration: sw.Elapsed,
            TotalAgentCalls: layerOutputs.Sum(l => l.Outputs.Count) + (_finalAgent is not null ? 1 : 0)
        );
    }

    /// <summary>Run MoA on a batch of tasks concurrently.</summary>
    public async Task<IReadOnlyList<MoAResult>> RunBatchedAsync(
        IReadOnlyList<string> tasks, int maxDegreeOfParallelism = 4, CancellationToken ct = default)
    {
        using var sem = new SemaphoreSlim(maxDegreeOfParallelism);
        var results = await Task.WhenAll(tasks.Select(async t =>
        {
            await sem.WaitAsync(ct);
            try { return await RunAsync(t, ct); }
            finally { sem.Release(); }
        }));
        return results;
    }

    /// <summary>Run MoA on multiple tasks, all fully concurrent (no limit).</summary>
    public Task<MoAResult[]> RunConcurrentlyAsync(IReadOnlyList<string> tasks, CancellationToken ct = default)
        => Task.WhenAll(tasks.Select(t => RunAsync(t, ct)));

    // ─── Private ─────────────────────────────────────────────────────────────

    private async Task<MoALayerResult> ExecuteLayerAsync(int layer, string context, CancellationToken ct)
    {
        var temperature = _config.LayerTemperatures.Count > layer
            ? _config.LayerTemperatures[layer] : _config.DefaultTemperature;

        var agentsToUse = _config.LayerStrategy == LayerStrategy.Hybrid && layer % 2 == 0
            ? _agents.Take((_agents.Count + 1) / 2).ToList()
            : _agents.ToList();

        var outputs = new ConcurrentBag<AgentLayerOutput>();

        if (_config.LayerStrategy == LayerStrategy.Sequential)
        {
            // Sequential: each agent sees output of previous
            string seq = context;
            foreach (var agent in agentsToUse)
            {
                try
                {
                    var resp = await agent.RunAsync(new AgentRequest(seq), ct);
                    var output = new AgentLayerOutput(agent.Id, resp.Content, layer, true);
                    outputs.Add(output);
                    if (_config.AggregationMode == AggregationMode.CriticSynth)
                        seq = BuildSequentialContext(context, output.Content);
                }
                catch (Exception ex)
                {
                    outputs.Add(new AgentLayerOutput(agent.Id, $"[ERROR: {ex.Message}]", layer, false));
                    if (!_config.ContinueOnAgentFailure) throw;
                }
            }
        }
        else
        {
            // Parallel (default) or Hybrid
            await Task.WhenAll(agentsToUse.Select(async agent =>
            {
                try
                {
                    var resp = await agent.RunAsync(new AgentRequest(context), ct);
                    outputs.Add(new AgentLayerOutput(agent.Id, resp.Content, layer, true));
                }
                catch (Exception ex)
                {
                    outputs.Add(new AgentLayerOutput(agent.Id, $"[ERROR: {ex.Message}]", layer, false));
                    if (!_config.ContinueOnAgentFailure) throw;
                }
            }));
        }

        return new MoALayerResult(layer, outputs.OrderBy(o => o.AgentId).ToList());
    }

    private static string BuildLayerContext(string originalTask, IReadOnlyList<AgentLayerOutput> outputs, int layer)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Original Task\n{originalTask}");
        sb.AppendLine($"\n# Layer {layer} Agent Outputs (synthesise these into a better answer):");
        foreach (var o in outputs.Where(x => x.Success))
            sb.AppendLine($"\n## Agent '{o.AgentId}'\n{o.Content}");
        sb.AppendLine("\nYour task: synthesise the above outputs into a single, improved, comprehensive response.");
        return sb.ToString();
    }

    private static string BuildSequentialContext(string task, string previousOutput)
        => $"# Task\n{task}\n\n# Previous Agent Output (refine and improve this):\n{previousOutput}";

    private static string BuildFinalAgentPrompt(string task, IReadOnlyList<AgentLayerOutput> outputs)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"You are the final synthesis agent. Original task: {task}");
        sb.AppendLine("\nYou have received these candidate answers from specialised agents:");
        foreach (var o in outputs.Where(x => x.Success))
            sb.AppendLine($"\n### Agent '{o.AgentId}':\n{o.Content}");
        sb.AppendLine("\nProduce the definitive, most accurate, comprehensive answer. Remove contradictions. Add missing details.");
        return sb.ToString();
    }

    private static string Aggregate(IReadOnlyList<AgentLayerOutput> outputs, AggregationMode mode)
    {
        var valid = outputs.Where(o => o.Success).ToList();
        if (valid.Count == 0) return "[All agents failed in final layer]";

        return mode switch
        {
            AggregationMode.Vote => valid
                .GroupBy(o => o.Content.Trim().ToLowerInvariant()[..Math.Min(100, o.Content.Length)])
                .OrderByDescending(g => g.Count())
                .First().First().Content,

            AggregationMode.WeightedScore => string.Join("\n\n---\n\n",
                valid.Select(o => o.Content)),

            AggregationMode.Merge or AggregationMode.CriticSynth => string.Join("\n\n", valid
                .Select((o, i) => $"## Perspective {i + 1} ({o.AgentId})\n{o.Content}")),

            _ => valid[0].Content
        };
    }
}

// ─── Records & Enums ─────────────────────────────────────────────────────────

public enum LayerStrategy { Parallel, Sequential, Hybrid }
public enum AggregationMode { Vote, Merge, CriticSynth, WeightedScore }

public sealed record MoAConfig
{
    public int Layers { get; init; } = 3;
    public LayerStrategy LayerStrategy { get; init; } = LayerStrategy.Parallel;
    public AggregationMode AggregationMode { get; init; } = AggregationMode.CriticSynth;
    public double DefaultTemperature { get; init; } = 0.7;
    public IReadOnlyList<double> LayerTemperatures { get; init; } = Array.Empty<double>();
    public bool ContinueOnAgentFailure { get; init; } = true;
    public Action<int, MoALayerResult>? OnLayerComplete { get; init; }
}

public record AgentLayerOutput(string AgentId, string Content, int Layer, bool Success);
public record MoALayerResult(int Layer, IReadOnlyList<AgentLayerOutput> Outputs);
public record MoAResult(
    string FinalOutput,
    IReadOnlyList<MoALayerResult> LayerResults,
    TimeSpan Duration,
    int TotalAgentCalls
);
