namespace AGXIOME.Shared.Models;

/// <summary>Aggregated result of a completed workflow execution.</summary>
public record WorkflowResult(
    string WorkflowId,
    WorkflowStatus Status,
    IReadOnlyList<StepResult> StepResults,
    WorkflowMetadata? Metadata = null
);

/// <summary>Result of a single workflow step execution.</summary>
public record StepResult(
    string StepName,
    StepStatus Status,
    object? Output,
    TimeSpan ExecutionTime,
    string? ErrorMessage = null
);

/// <summary>Shared context passed through a workflow execution.</summary>
public record WorkflowContext(
    string? SessionId = null,
    Dictionary<string, object>? SharedState = null,
    CancellationToken CancellationToken = default
);

/// <summary>Execution metadata for a completed workflow run.</summary>
public record WorkflowMetadata(
    int StepsExecuted,
    TimeSpan TotalDuration,
    int TotalInferences
);
