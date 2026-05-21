namespace AGXIOME.Shared.Models;

// ─────────────────────────────────────────────────────────────────────────────
// Background job / runtime types
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Category of background job scheduled on the AGXIOME runtime.</summary>
public enum BackgroundJobType
{
    RunExecution = 0,
    SessionCleanup = 1,
    TokenRotation = 2,
    KnowledgeIngest = 3,
}

/// <summary>Execution status of a background job instance.</summary>
public enum BackgroundJobStatus
{
    Queued = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4,
    Retrying = 5,
}

/// <summary>Strategy applied when a paused or failed run is resumed.</summary>
public enum ResumeStrategy
{
    Restart = 0,
    ContinueFromCheckpoint = 1,
    SkipFailed = 2,
}
