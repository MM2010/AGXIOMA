// RT-Repositories — ConcurrentDictionary-backed in-memory implementations.
// Thread-safe for multi-threaded ASP.NET Core request handling.
// Register as singletons so state persists across requests within a process.
// For persistent storage swap these registrations with a SQLite/Redis/Postgres adapter.

using System.Collections.Concurrent;
using AGXIOME.Runtime.Controllers;

namespace AGXIOME.Runtime.Repositories;

// ─── Agent ────────────────────────────────────────────────────────────────────

public sealed class InMemoryAgentRepository : IAgentRepository
{
    private readonly ConcurrentDictionary<string, AgentDto> _store = new();

    public Task<AgentDto?> GetByIdAsync(string id, CancellationToken ct = default) =>
        Task.FromResult(_store.TryGetValue(id, out var a) ? a : null);

    public Task<IReadOnlyList<AgentDto>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<AgentDto>>(_store.Values.ToList());

    public Task<AgentDto> SaveAsync(AgentDto agent, CancellationToken ct = default)
    {
        _store[agent.Id] = agent;
        return Task.FromResult(agent);
    }

    public Task<AgentDto?> UpdateAsync(string id, AgentDto agent, CancellationToken ct = default)
    {
        if (!_store.ContainsKey(id)) return Task.FromResult<AgentDto?>(null);
        _store[id] = agent;
        return Task.FromResult<AgentDto?>(agent);
    }

    public Task<bool> DeleteAsync(string id, CancellationToken ct = default) =>
        Task.FromResult(_store.TryRemove(id, out _));

    public Task<bool> ExistsAsync(string id, CancellationToken ct = default) =>
        Task.FromResult(_store.ContainsKey(id));
}

// ─── Run ──────────────────────────────────────────────────────────────────────

public sealed class InMemoryRunRepository : IRunRepository
{
    private readonly ConcurrentDictionary<string, RunDto> _store = new();

    public Task<RunDto?> GetByIdAsync(string runId, CancellationToken ct = default) =>
        Task.FromResult(_store.TryGetValue(runId, out var r) ? r : null);

    public Task<IReadOnlyList<RunDto>> ListAsync(string? agentId = null, CancellationToken ct = default)
    {
        var result = agentId == null
            ? _store.Values.ToList()
            : _store.Values.Where(r => r.AgentId == agentId).ToList();
        return Task.FromResult<IReadOnlyList<RunDto>>(result);
    }

    public Task SaveAsync(RunDto run, CancellationToken ct = default)
    {
        _store[run.RunId] = run;
        return Task.CompletedTask;
    }

    public Task<RunDto?> UpdateAsync(string runId, RunDto run, CancellationToken ct = default)
    {
        if (!_store.ContainsKey(runId)) return Task.FromResult<RunDto?>(null);
        _store[runId] = run;
        return Task.FromResult<RunDto?>(run);
    }

    public Task<bool> DeleteAsync(string runId, CancellationToken ct = default) =>
        Task.FromResult(_store.TryRemove(runId, out _));
}

// ─── Session ──────────────────────────────────────────────────────────────────

public sealed class InMemorySessionRepository : ISessionRepository
{
    private readonly ConcurrentDictionary<string, SessionDto> _store = new();

    public Task<SessionDto?> GetByIdAsync(string sessionId, CancellationToken ct = default) =>
        Task.FromResult(_store.TryGetValue(sessionId, out var s) ? s : null);

    public Task<IReadOnlyList<SessionDto>> ListAsync(string? agentId = null, CancellationToken ct = default)
    {
        var result = agentId == null
            ? _store.Values.ToList()
            : _store.Values.Where(s => s.AgentId == agentId).ToList();
        return Task.FromResult<IReadOnlyList<SessionDto>>(result);
    }

    public Task<SessionDto> SaveAsync(SessionDto session, CancellationToken ct = default)
    {
        _store[session.SessionId] = session;
        return Task.FromResult(session);
    }

    public Task<SessionDto?> CloseAsync(string sessionId, CancellationToken ct = default)
    {
        if (!_store.TryGetValue(sessionId, out var s)) return Task.FromResult<SessionDto?>(null);
        var closed = s with { IsActive = false };
        _store[sessionId] = closed;
        return Task.FromResult<SessionDto?>(closed);
    }

    public Task<bool> DeleteAsync(string sessionId, CancellationToken ct = default) =>
        Task.FromResult(_store.TryRemove(sessionId, out _));
}

// ─── Team ─────────────────────────────────────────────────────────────────────

public sealed class InMemoryTeamRepository : ITeamRepository
{
    private readonly ConcurrentDictionary<string, TeamDto> _store = new();

    public Task<TeamDto?> GetByIdAsync(string teamId, CancellationToken ct = default) =>
        Task.FromResult(_store.TryGetValue(teamId, out var t) ? t : null);

    public Task<IReadOnlyList<TeamDto>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<TeamDto>>(_store.Values.ToList());

    public Task<TeamDto> SaveAsync(TeamDto team, CancellationToken ct = default)
    {
        _store[team.TeamId] = team;
        return Task.FromResult(team);
    }

    public Task<TeamDto?> UpdateAsync(string teamId, TeamDto team, CancellationToken ct = default)
    {
        if (!_store.ContainsKey(teamId)) return Task.FromResult<TeamDto?>(null);
        _store[teamId] = team;
        return Task.FromResult<TeamDto?>(team);
    }

    public Task<bool> DeleteAsync(string teamId, CancellationToken ct = default) =>
        Task.FromResult(_store.TryRemove(teamId, out _));

    public Task<bool> ExistsAsync(string teamId, CancellationToken ct = default) =>
        Task.FromResult(_store.ContainsKey(teamId));
}

// ─── Workflow ─────────────────────────────────────────────────────────────────

public sealed class InMemoryWorkflowRepository : IWorkflowRepository
{
    private readonly ConcurrentDictionary<string, WorkflowDto> _store = new();
    private readonly ConcurrentDictionary<string, List<object>> _history = new();

    public Task<WorkflowDto?> GetByIdAsync(string workflowId, CancellationToken ct = default) =>
        Task.FromResult(_store.TryGetValue(workflowId, out var w) ? w : null);

    public Task<IReadOnlyList<WorkflowDto>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<WorkflowDto>>(_store.Values.ToList());

    public Task<WorkflowDto> SaveAsync(WorkflowDto workflow, CancellationToken ct = default)
    {
        _store[workflow.WorkflowId] = workflow;
        return Task.FromResult(workflow);
    }

    public Task<WorkflowDto?> UpdateAsync(string workflowId, WorkflowDto workflow, CancellationToken ct = default)
    {
        if (!_store.ContainsKey(workflowId)) return Task.FromResult<WorkflowDto?>(null);
        _store[workflowId] = workflow;
        return Task.FromResult<WorkflowDto?>(workflow);
    }

    public Task<bool> DeleteAsync(string workflowId, CancellationToken ct = default) =>
        Task.FromResult(_store.TryRemove(workflowId, out _));

    public Task RecordHistoryAsync(string workflowId, object entry, CancellationToken ct = default)
    {
        _history.GetOrAdd(workflowId, _ => []).Add(entry);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<object>> GetHistoryAsync(string workflowId, CancellationToken ct = default)
    {
        var list = _history.TryGetValue(workflowId, out var h) ? (IReadOnlyList<object>)h : [];
        return Task.FromResult(list);
    }
}
