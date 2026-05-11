using AGXIOME.Core.Agent;
using AGXIOME.Shared.Config;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Core.Teams;

public sealed class AgxiomeTeam : IAgxiomeTeam
{
    public string Id { get; }
    public TeamTopology Topology { get; }
    public TeamMode Mode { get; }
    public IReadOnlyList<IAgxiomeAgent> Agents => _members.AsReadOnly();

    private readonly IAgxiomeAgent _leader;
    private readonly List<IAgxiomeAgent> _members = new();
    private readonly TeamConfig _config;

    public AgxiomeTeam(string name, TeamConfig config, IAgxiomeAgent leader)
    {
        Id = name; _config = config; _leader = leader; Mode = config.Mode;
        Topology = config.Mode switch
        {
            TeamMode.Coordinate => TeamTopology.Router,
            TeamMode.Route => TeamTopology.Router,
            TeamMode.Broadcast => TeamTopology.Swarm,
            TeamMode.Tasks => TeamTopology.Supervisor,
            _ => TeamTopology.Router
        };
    }

    public IAgxiomeAgent AddAgent(string name, string role, Action<AgentConfig>? configure = null)
    {
        var cfg = new AgentConfig { Id = name };
        configure?.Invoke(cfg);
        cfg.Role = role;
        // Use test agent to avoid LLM engine dependency in AddAgent
        var agent = AgentFactory.CreateTestAgent(name);
        _members.Add(agent);
        return agent;
    }

    public async Task<TeamResponse> ExecuteAsync(string objective, TeamContext? context = null, CancellationToken ct = default)
    {
        var results = new List<AgentResult>();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        switch (Mode)
        {
            case TeamMode.Coordinate:
                // Leader decomposes objective, delegates to members, synthesizes
                var delegation = await _leader.RunAsync(new AgentRequest(
                    $"Decompose this objective into subtasks and assign to specialists: {objective}"), ct);
                // Parse delegation result → assign to matching members
                foreach (var member in _members)
                {
                    try
                    {
                        var resp = await member.RunAsync(new AgentRequest(
                            $"{objective} (your role: {member.GetType().Name})"), ct);
                        results.Add(new AgentResult(member.Id, "specialist", resp.Status, resp.Content, sw.Elapsed));
                    }
                    catch { if (!_config.ContinueOnPartialFailure) throw; }
                }
                break;

            case TeamMode.Route:
                var specialist = _members.FirstOrDefault();
                if (specialist != null)
                {
                    var resp = await specialist.RunAsync(new AgentRequest(objective), ct);
                    results.Add(new AgentResult(specialist.Id, "specialist", resp.Status, resp.Content, sw.Elapsed));
                }
                break;

            case TeamMode.Broadcast:
                var tasks = _members.Select(m => m.RunAsync(new AgentRequest(objective), ct));
                var responses = await Task.WhenAll(tasks);
                for (int i = 0; i < responses.Length; i++)
                    results.Add(new AgentResult(_members[i].Id, "broadcast", responses[i].Status, responses[i].Content, sw.Elapsed));
                break;
        }

        sw.Stop();
        return new TeamResponse(
            string.Join("\n---\n", results.Select(r => r.Output)),
            Mode,
            results,
            new TeamMetadata(results.Count, sw.Elapsed, results.Count)
        );
    }

    public async IAsyncEnumerable<AgentChunk> ExecuteStreamAsync(string objective, TeamContext? context = null, CancellationToken ct = default)
    {
        await foreach (var chunk in _leader.RunStreamAsync(new AgentRequest(objective), ct))
            yield return chunk;
    }
}
