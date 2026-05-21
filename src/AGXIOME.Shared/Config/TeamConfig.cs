using AGXIOME.Shared.Models;

namespace AGXIOME.Shared.Config;

/// <summary>Configuration for a multi-agent team, controlling routing, timeouts, and failure policy.</summary>
public record TeamConfig
{
    public string Name { get; init; } = "";
    public TeamMode Mode { get; init; } = TeamMode.Coordinate;
    public int MaxIterations { get; init; } = 20;
    public TimeSpan AgentTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan GlobalTimeout { get; init; } = TimeSpan.FromSeconds(120);
    public bool ContinueOnPartialFailure { get; init; } = true;
    public int MaxRetriesPerAgent { get; init; } = 3;
    public bool CacheCallables { get; init; } = true;
}
