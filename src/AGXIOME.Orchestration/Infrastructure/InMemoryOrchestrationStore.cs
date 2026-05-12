using System.Collections.Concurrent;
using AGXIOME.Orchestration.Abstractions;
using AGXIOME.Orchestration.Models;

namespace AGXIOME.Orchestration.Infrastructure;

/// <summary>
/// In-memory IOrchestrationStore backed by ConcurrentDictionary.
/// No persistence — intended for tests and local dev only.
/// Thread-safe for concurrent read/write access.
/// </summary>
public sealed class InMemoryOrchestrationStore : IOrchestrationStore
{
    private readonly ConcurrentDictionary<Guid, PlanState> _plans = new();
    private readonly ConcurrentDictionary<Guid, TaskSpec> _specs = new();
    private readonly ConcurrentDictionary<Guid, TaskResult> _results = new();

    // ── Plan ──────────────────────────────────────────────────────────────

    public Task SavePlanAsync(PlanState plan, CancellationToken ct = default)
    {
        _plans[plan.PlanId] = plan;
        return Task.CompletedTask;
    }

    public Task<PlanState?> GetPlanAsync(Guid planId, CancellationToken ct = default)
    {
        _plans.TryGetValue(planId, out var plan);
        return Task.FromResult<PlanState?>(plan);
    }

    public Task UpdatePlanStatusAsync(Guid planId, OrchestratorPlanStatus status,
        string? finalSummary = null, CancellationToken ct = default)
    {
        if (_plans.TryGetValue(planId, out var existing))
        {
            _plans[planId] = existing with
            {
                Status = status,
                FinalSummary = finalSummary ?? existing.FinalSummary,
                CompletedAt = status is OrchestratorPlanStatus.Completed or
                              OrchestratorPlanStatus.Failed or
                              OrchestratorPlanStatus.Cancelled
                    ? DateTimeOffset.UtcNow
                    : existing.CompletedAt,
                UpdatedAt = DateTimeOffset.UtcNow
            };
        }
        return Task.CompletedTask;
    }

    public Task AdvancePlanStepAsync(Guid planId, int newStep,
        IReadOnlyDictionary<Guid, TaskResult> newResults, CancellationToken ct = default)
    {
        if (_plans.TryGetValue(planId, out var existing))
        {
            // Merge new results into the existing map
            var merged = new Dictionary<Guid, TaskResult>(existing.CompletedResults);
            foreach (var (k, v) in newResults)
                merged[k] = v;

            _plans[planId] = existing with
            {
                CurrentStep = newStep,
                Status = OrchestratorPlanStatus.Running,
                CompletedResults = merged,
                UpdatedAt = DateTimeOffset.UtcNow
            };
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PlanState>> GetActivePlansAsync(CancellationToken ct = default)
    {
        var active = _plans.Values
            .Where(p => p.Status is OrchestratorPlanStatus.Running or OrchestratorPlanStatus.Paused)
            .ToList();
        return Task.FromResult<IReadOnlyList<PlanState>>(active);
    }

    // ── Task spec ─────────────────────────────────────────────────────────

    public Task SaveTaskSpecAsync(TaskSpec spec, CancellationToken ct = default)
    {
        _specs[spec.TaskId] = spec;
        return Task.CompletedTask;
    }

    public Task<TaskSpec?> GetTaskSpecAsync(Guid taskId, CancellationToken ct = default)
    {
        _specs.TryGetValue(taskId, out var spec);
        return Task.FromResult<TaskSpec?>(spec);
    }

    public Task UpdateTaskStatusAsync(Guid taskId, OrchestratorTaskStatus status,
        CancellationToken ct = default)
    {
        if (_specs.TryGetValue(taskId, out var existing))
        {
            _specs[taskId] = existing with
            {
                Status = status,
                StartedAt = status == OrchestratorTaskStatus.Running
                    ? existing.StartedAt ?? DateTimeOffset.UtcNow
                    : existing.StartedAt
            };
        }
        return Task.CompletedTask;
    }

    // ── Results ───────────────────────────────────────────────────────────

    public Task SaveResultAsync(TaskResult result, CancellationToken ct = default)
    {
        _results[result.TaskId] = result;
        return Task.CompletedTask;
    }

    public Task<TaskResult?> GetResultAsync(Guid taskId, CancellationToken ct = default)
    {
        _results.TryGetValue(taskId, out var result);
        return Task.FromResult<TaskResult?>(result);
    }

    public Task<IReadOnlyList<TaskResult>> GetPlanResultsAsync(Guid planId,
        CancellationToken ct = default)
    {
        var results = _results.Values.Where(r => r.PlanId == planId).ToList();
        return Task.FromResult<IReadOnlyList<TaskResult>>(results);
    }

    /// <summary>Clears all data — for test isolation.</summary>
    public void Clear()
    {
        _plans.Clear();
        _specs.Clear();
        _results.Clear();
    }
}
