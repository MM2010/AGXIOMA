// MU-06 — Control Plane UI Service
// Provides aggregated dashboard data for the web UI. Read-only projection layer.
// Feeds the web dashboard: agents, teams, runs, knowledge summary.

namespace AGXIOME.Runtime.UI;

// ── Projection records ───────────────────────────────────────────────────────

public sealed record AgentStatus(
    string AgentId, string Name, string State,
    int ActiveRuns, DateTimeOffset LastActivity);

public sealed record RunSummary(
    string RunId, string AgentId, string Status,
    DateTimeOffset StartedAt, DateTimeOffset? EndedAt);

public sealed record TeamStatus(
    string TeamId, string Name,
    IReadOnlyList<string> AgentIds, int ActiveCoordinations);

public sealed record KnowledgeSummary(
    int CollectionCount, long TotalDocuments, long TotalChunks);

public sealed record DashboardSummary(
    int TotalAgents, int ActiveRuns, int TotalTeams, int TotalWorkflows,
    KnowledgeSummary Knowledge, DateTimeOffset GeneratedAt);

// ── Service contract ─────────────────────────────────────────────────────────

public interface IControlPlaneService
{
    /// <summary>Returns aggregated dashboard summary for the UI landing page.</summary>
    Task<DashboardSummary> GetDashboardSummaryAsync(CancellationToken ct = default);

    /// <summary>Returns current status of all registered agents.</summary>
    Task<IReadOnlyList<AgentStatus>> GetAgentStatusesAsync(CancellationToken ct = default);

    /// <summary>Returns list of runs currently in Running/Pending state.</summary>
    Task<IReadOnlyList<RunSummary>> GetActiveRunsAsync(CancellationToken ct = default);

    /// <summary>Returns team status projections.</summary>
    Task<IReadOnlyList<TeamStatus>> GetTeamStatusesAsync(CancellationToken ct = default);

    /// <summary>Returns aggregate knowledge store stats.</summary>
    Task<KnowledgeSummary> GetKnowledgeSummaryAsync(CancellationToken ct = default);
}

// ── Default implementation (in-memory / stub for dev, replace with Redis read-model) ─

public sealed class ControlPlaneService : IControlPlaneService
{
    // In-memory stubs — production reads from Redis cached projections.
    private readonly List<AgentStatus> _agents   = [];
    private readonly List<RunSummary>  _runs     = [];
    private readonly List<TeamStatus>  _teams    = [];
    private KnowledgeSummary           _knowledge = new(0, 0, 0);

    // Allow injection of test data
    public void SeedAgent(AgentStatus s)   => _agents.Add(s);
    public void SeedRun(RunSummary r)      => _runs.Add(r);
    public void SeedTeam(TeamStatus t)     => _teams.Add(t);
    public void SeedKnowledge(KnowledgeSummary k) => _knowledge = k;

    public Task<DashboardSummary> GetDashboardSummaryAsync(CancellationToken ct = default)
    {
        var summary = new DashboardSummary(
            TotalAgents:    _agents.Count,
            ActiveRuns:     _runs.Count(r => r.Status is "Running" or "Pending"),
            TotalTeams:     _teams.Count,
            TotalWorkflows: 0,
            Knowledge:      _knowledge,
            GeneratedAt:    DateTimeOffset.UtcNow);
        return Task.FromResult(summary);
    }

    public Task<IReadOnlyList<AgentStatus>> GetAgentStatusesAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AgentStatus>>(_agents.AsReadOnly());

    public Task<IReadOnlyList<RunSummary>> GetActiveRunsAsync(CancellationToken ct = default)
    {
        var active = _runs.Where(r => r.Status is "Running" or "Pending").ToList();
        return Task.FromResult<IReadOnlyList<RunSummary>>(active);
    }

    public Task<IReadOnlyList<TeamStatus>> GetTeamStatusesAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TeamStatus>>(_teams.AsReadOnly());

    public Task<KnowledgeSummary> GetKnowledgeSummaryAsync(CancellationToken ct = default)
        => Task.FromResult(_knowledge);
}
