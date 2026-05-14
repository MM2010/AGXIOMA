using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Core.Teams;

/// <summary>
/// SocialAlgorithmExecutor — converts a custom callable into team execution.
/// The callable receives objective + list of agents and returns a TeamResponse.
/// Parity with Swarms: SocialAlgorithm(callable) where callable decides routing.
/// </summary>
public sealed class SocialAlgorithmExecutor
{
    private readonly Func<string, IReadOnlyList<IAgxiomeAgent>, CancellationToken, Task<TeamResponse>> _callable;

    public SocialAlgorithmExecutor(Func<string, IReadOnlyList<IAgxiomeAgent>, CancellationToken, Task<TeamResponse>> callable)
    {
        _callable = callable ?? throw new ArgumentNullException(nameof(callable));
    }

    /// <summary>Execute the custom social algorithm.</summary>
    public async Task<TeamResponse> ExecuteAsync(string objective, IReadOnlyList<IAgxiomeAgent> agents, CancellationToken ct = default)
    {
        return await _callable(objective, agents, ct);
    }
}

/// <summary>Pre-built social algorithms.</summary>
public static class SocialAlgorithms
{
    /// <summary>Round-robin: each agent speaks in sequence, building on the last.</summary>
    public static async Task<TeamResponse> RoundRobin(string objective, IReadOnlyList<IAgxiomeAgent> agents, CancellationToken ct = default)
    {
        var results = new List<AgentResult>();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        string? context = objective;

        foreach (var agent in agents)
        {
            var prompt = $"Previous context: {context}\n\nContinue working on: {objective}";
            var resp = await agent.RunAsync(new AgentRequest(prompt), ct);
            results.Add(new AgentResult(agent.Id, "round_robin", resp.Status, resp.Content, sw.Elapsed));
            context = resp.Content;
        }

        sw.Stop();
        return new TeamResponse(
            string.Join("\n---\n", results.Select(r => r.Output)),
            TeamMode.SocialAlgorithm,
            results,
            new TeamMetadata(results.Count, sw.Elapsed, results.Count)
        );
    }

    /// <summary>Debate: agents argue different positions, then vote.</summary>
    public static async Task<TeamResponse> Debate(string objective, IReadOnlyList<IAgxiomeAgent> agents, CancellationToken ct = default)
    {
        var results = new List<AgentResult>();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Round 1: each agent takes a position
        var round1 = agents.Select(a => a.RunAsync(new AgentRequest($"Take a position on: {objective}"), ct)).ToArray();
        var responses = await Task.WhenAll(round1);
        for (int i = 0; i < responses.Length; i++)
            results.Add(new AgentResult(agents[i].Id, "debate_round1", responses[i].Status, responses[i].Content, sw.Elapsed));

        // Round 2: agents respond to each other
        var allViews = string.Join("\n\n", results.Select(r => r.Output));
        var round2 = agents.Select(a => a.RunAsync(new AgentRequest($"Given these views:\n{allViews}\n\nProvide your rebuttal or synthesis."), ct)).ToArray();
        var responses2 = await Task.WhenAll(round2);
        for (int i = 0; i < responses2.Length; i++)
            results.Add(new AgentResult(agents[i].Id, "debate_round2", responses2[i].Status, responses2[i].Content, sw.Elapsed));

        sw.Stop();
        return new TeamResponse(
            string.Join("\n\n===DEBATE===\n\n", results.Select(r => $"[{r.Role}] {r.Output}")),
            TeamMode.SocialAlgorithm,
            results,
            new TeamMetadata(results.Count, sw.Elapsed, agents.Count)
        );
    }
}
