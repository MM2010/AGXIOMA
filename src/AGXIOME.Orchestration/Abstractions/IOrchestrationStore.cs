using AGXIOME.Orchestration.Models;

namespace AGXIOME.Orchestration.Abstractions;

/// <summary>
/// Canonical persistent storage for plans, task specs, and results.
/// Implementable on Postgres, SQLite, MongoDB, CosmosDB, DynamoDB, etc.
/// Null is returned for not-found entities — no exceptions for missing records.
/// </summary>
public interface IOrchestrationStore
{
    // ── Plan ──────────────────────────────────────────────────────────────
    Task SavePlanAsync(PlanState plan, CancellationToken ct = default);
    Task<PlanState?> GetPlanAsync(Guid planId, CancellationToken ct = default);
    Task UpdatePlanStatusAsync(Guid planId, OrchestratorPlanStatus status,
        string? finalSummary = null, CancellationToken ct = default);

    /// <summary>
    /// Atomically advances CurrentStep and merges new results into CompletedResults.
    /// One write per step transition — keeps audit trail intact.
    /// </summary>
    Task AdvancePlanStepAsync(Guid planId, int newStep,
        IReadOnlyDictionary<Guid, TaskResult> newResults, CancellationToken ct = default);

    /// <summary>Returns all plans with status Running or Paused — used for crash recovery.</summary>
    Task<IReadOnlyList<PlanState>> GetActivePlansAsync(CancellationToken ct = default);

    // ── Task spec ─────────────────────────────────────────────────────────
    Task SaveTaskSpecAsync(TaskSpec spec, CancellationToken ct = default);
    Task<TaskSpec?> GetTaskSpecAsync(Guid taskId, CancellationToken ct = default);
    Task UpdateTaskStatusAsync(Guid taskId, OrchestratorTaskStatus status,
        CancellationToken ct = default);

    // ── Results ───────────────────────────────────────────────────────────
    Task SaveResultAsync(TaskResult result, CancellationToken ct = default);
    Task<TaskResult?> GetResultAsync(Guid taskId, CancellationToken ct = default);
    Task<IReadOnlyList<TaskResult>> GetPlanResultsAsync(Guid planId,
        CancellationToken ct = default);
}
