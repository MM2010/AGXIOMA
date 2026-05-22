// RT-Repositories — typed repository contracts for the Runtime layer.
// Each interface maps 1-to-1 with a controller domain.
// Default implementation: InMemoryRepositories.cs (ConcurrentDictionary-backed).
// Production: swap with SQLite/Redis/PostgreSQL adapters via DI.

using AGXIOME.Runtime.Controllers;

namespace AGXIOME.Runtime.Repositories;

// ─── Agent ────────────────────────────────────────────────────────────────────

public interface IAgentRepository
{
    Task<AgentDto?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<AgentDto>> ListAsync(CancellationToken ct = default);
    Task<AgentDto> SaveAsync(AgentDto agent, CancellationToken ct = default);
    Task<AgentDto?> UpdateAsync(string id, AgentDto agent, CancellationToken ct = default);
    Task<bool> DeleteAsync(string id, CancellationToken ct = default);
    Task<bool> ExistsAsync(string id, CancellationToken ct = default);
}

// ─── Run ──────────────────────────────────────────────────────────────────────

public interface IRunRepository
{
    Task<RunDto?> GetByIdAsync(string runId, CancellationToken ct = default);
    Task<IReadOnlyList<RunDto>> ListAsync(string? agentId = null, CancellationToken ct = default);
    Task SaveAsync(RunDto run, CancellationToken ct = default);
    Task<RunDto?> UpdateAsync(string runId, RunDto run, CancellationToken ct = default);
    Task<bool> DeleteAsync(string runId, CancellationToken ct = default);
}

// ─── Session ──────────────────────────────────────────────────────────────────

public interface ISessionRepository
{
    Task<SessionDto?> GetByIdAsync(string sessionId, CancellationToken ct = default);
    Task<IReadOnlyList<SessionDto>> ListAsync(string? agentId = null, CancellationToken ct = default);
    Task<SessionDto> SaveAsync(SessionDto session, CancellationToken ct = default);
    Task<SessionDto?> CloseAsync(string sessionId, CancellationToken ct = default);
    Task<bool> DeleteAsync(string sessionId, CancellationToken ct = default);
}

// ─── Team ─────────────────────────────────────────────────────────────────────

public interface ITeamRepository
{
    Task<TeamDto?> GetByIdAsync(string teamId, CancellationToken ct = default);
    Task<IReadOnlyList<TeamDto>> ListAsync(CancellationToken ct = default);
    Task<TeamDto> SaveAsync(TeamDto team, CancellationToken ct = default);
    Task<TeamDto?> UpdateAsync(string teamId, TeamDto team, CancellationToken ct = default);
    Task<bool> DeleteAsync(string teamId, CancellationToken ct = default);
    Task<bool> ExistsAsync(string teamId, CancellationToken ct = default);
}

// ─── Workflow ─────────────────────────────────────────────────────────────────

public interface IWorkflowRepository
{
    Task<WorkflowDto?> GetByIdAsync(string workflowId, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowDto>> ListAsync(CancellationToken ct = default);
    Task<WorkflowDto> SaveAsync(WorkflowDto workflow, CancellationToken ct = default);
    Task<WorkflowDto?> UpdateAsync(string workflowId, WorkflowDto workflow, CancellationToken ct = default);
    Task<bool> DeleteAsync(string workflowId, CancellationToken ct = default);
    Task RecordHistoryAsync(string workflowId, object entry, CancellationToken ct = default);
    Task<IReadOnlyList<object>> GetHistoryAsync(string workflowId, CancellationToken ct = default);
}
