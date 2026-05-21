namespace AGXIOME.Shared.Models;

/// <summary>A recurring cron-based schedule that triggers workflow or plan execution.</summary>
public record Schedule(
    string Id,
    string Name,
    string WorkflowId,
    string CronExpression,
    SchedulePayload? Payload = null,
    ScheduleStatus Status = ScheduleStatus.Active,
    DateTime CreatedAt = default,
    DateTime? LastRunAt = null,
    DateTime? NextRunAt = null
);

/// <summary>Input payload attached to a schedule trigger.</summary>
public record SchedulePayload(
    Dictionary<string, object>? Input = null,
    string? SessionId = null,
    // P17: when set, the schedule triggers IPlanOrchestrator.StartAsync with this request string
    string? PlanRequest = null
);

/// <summary>Record of a single schedule run execution.</summary>
public record ScheduleRun(
    string ScheduleId,
    string RunId,
    ScheduleRunStatus Status,
    DateTime StartedAt,
    DateTime? CompletedAt = null,
    string? ErrorMessage = null
);
