using AGXIOME.Shared.Models;
using AGXIOME.Shared.Config;

namespace AGXIOME.Shared.Interfaces;

public interface IAgxiomeTeam
{
    string Id { get; }
    TeamTopology Topology { get; }
    TeamMode Mode { get; }
    IReadOnlyList<IAgxiomeAgent> Agents { get; }
    IAgxiomeAgent AddAgent(string name, string role, Action<AgentConfig>? configure = null);
    Task<TeamResponse> ExecuteAsync(string objective, TeamContext? context = null, CancellationToken ct = default);
    IAsyncEnumerable<Models.AgentChunk> ExecuteStreamAsync(string objective, TeamContext? context = null, CancellationToken ct = default);
}
