namespace AGXIOME.Orchestration.Models;

public enum OrchestratorTaskStatus
{
    Pending,
    Running,
    Done,
    Failed,
    Skipped,
    Cancelled
}

public enum OrchestratorPlanStatus
{
    Decomposing,
    Running,
    Paused,
    /// <summary>Plan is waiting for a human-in-the-loop approval before resuming execution.</summary>
    WaitingForHITL,
    Completed,
    Failed,
    Cancelled
}

/// <summary>Parallelism mode for a group of tasks sharing the same StepIndex.</summary>
public enum StepExecutionMode
{
    Sequential,  // one task at a time
    Parallel,    // all at once — Task.WhenAll
    RaceFirst,   // first to finish wins; others are cancelled
    Quorum       // N/2+1 completions required
}

/// <summary>Decision produced by AdvancePlanTool after evaluating step results.</summary>
public enum AdvanceDecision
{
    Advance,    // proceed to next step
    Complete,   // plan is done — emit final summary
    Fail,       // unrecoverable failure — cancel remaining jobs
    Retry       // re-spawn failed tasks in the same step
}
