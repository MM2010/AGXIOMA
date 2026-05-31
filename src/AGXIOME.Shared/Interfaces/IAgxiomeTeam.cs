using AGXIOME.Shared.Models;
using AGXIOME.Shared.Config;

namespace AGXIOME.Shared.Interfaces;

/// <summary>
/// Multi-agent team coordinating multiple <see cref="IAgxiomeAgent"/> instances
/// under a shared topology and execution mode.
/// </summary>
/// <remarks>
/// <b>Topology</b>: Router picks the best agent. Swarm runs all in parallel.
/// Pipeline chains agents sequentially. Supervisor delegates to sub-agents.
/// <b>Mode</b>: Coordinate (collaborative), Route (single dispatch), Broadcast (all, best wins).
/// </remarks>
public interface IAgxiomeTeam
{
    string Id { get; }
    TeamTopology Topology { get; }
    TeamMode Mode { get; }
    IReadOnlyList<IAgxiomeAgent> Agents { get; }
    IAgxiomeAgent AddAgent(string name, string role, Action<AgentConfig>? configure = null);
    Task<TeamResponse> ExecuteAsync(string objective, TeamContext? context = null, CancellationToken ct = default);
    IAsyncEnumerable<AgentChunk> ExecuteStreamAsync(string objective, TeamContext? context = null, CancellationToken ct = default);
}
