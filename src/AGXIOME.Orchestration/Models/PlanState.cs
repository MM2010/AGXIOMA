namespace AGXIOME.Orchestration.Models;

/// <summary>
/// Canonical state of a full orchestration plan.
/// This is the single source of truth — can reconstruct all context from scratch.
/// </summary>
public sealed record PlanState
{
    public required Guid PlanId { get; init; }
    public required string OriginalRequest { get; init; }
    public required string SessionId { get; init; }

    /// <summary>All tasks ordered by StepIndex. Tasks sharing the same StepIndex may run in parallel.</summary>
    public IReadOnlyList<TaskSpec> Tasks { get; init; } = [];

    public int CurrentStep { get; init; }
    public OrchestratorPlanStatus Status { get; init; } = OrchestratorPlanStatus.Decomposing;

    /// <summary>TaskId → completed result map; written atomically by AdvancePlanTool.</summary>
    public IReadOnlyDictionary<Guid, TaskResult> CompletedResults { get; init; }
        = new Dictionary<Guid, TaskResult>();

    public string? FinalSummary { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;

    // ── Computed helpers (no extra state) ─────────────────────────────────

    /// <summary>Tasks belonging to the current step (may be > 1 for parallel steps).</summary>
    public IReadOnlyList<TaskSpec> CurrentStepTasks =>
        Tasks.Where(t => t.StepIndex == CurrentStep).ToList();

    /// <summary>True when every task in the current step has a result entry.</summary>
    public bool IsCurrentStepComplete =>
        CurrentStepTasks.Count > 0 &&
        CurrentStepTasks.All(t => CompletedResults.ContainsKey(t.TaskId));

    /// <summary>Maximum step index defined in the plan.</summary>
    public int MaxStepIndex =>
        Tasks.Count == 0 ? 0 : Tasks.Max(t => t.StepIndex);

    /// <summary>True when the current step is the last one.</summary>
    public bool IsLastStep => CurrentStep >= MaxStepIndex;
}
