using AGXIOME.Shared.Interfaces;

namespace AGXIOME.Shared.Models;

public record GuardrailContext(string AgentId, string? SessionId, string? UserId);
public record GuardrailResult(bool IsValid, string? Reason = null, GuardrailSeverity? OverrideSeverity = null);

public record HookContext(
    string AgentId,
    string RunId,
    HookPoint Point,
    object? Input = null,
    object? Output = null,
    Exception? Error = null,
    ISessionState? SessionState = null
);

public record HITLRequirement(
    string Id,
    string RunId,
    string AgentId,
    string ToolName,
    object Payload,
    HITLRequirementType Type,
    HITLStatus Status = HITLStatus.Pending,
    string? MemberAgentName = null,
    // Optional orchestration context — populated when HITL is triggered from an orchestrated plan task.
    // Enables HITLManager.Resolve() to resume the paused orchestrator task via IHitlCompletionSource.
    Guid? OrchestratorTaskId = null,
    Guid? OrchestratorPlanId = null
);

public record ApprovalRequest(
    string Id,
    string AgentId,
    string ToolName,
    object Payload,
    ApprovalStatus Status,
    ApprovalConfig? Config = null,
    DateTime CreatedAt = default
);

public record ApprovalConfig(
    TimeSpan? Timeout = null,
    bool RequiresAuditTrail = true,
    string? NotifyEndpoint = null,
    List<string>? RequiredApprovers = null
);

public record ApprovalResolution(bool Approved, string? Notes = null);

public record AuditEvent(
    string Id,
    string EventType,
    string ActorId,
    string? TargetId,
    string Action,
    object? Payload,
    string? ClientIp,
    bool Success,
    string? FailureReason,
    DateTime Timestamp
);

public record AuditQuery(
    string? EventType = null,
    string? ActorId = null,
    DateTime? From = null,
    DateTime? To = null,
    int Limit = 100
);

public record MemoryEntry(
    string Id,
    string UserId,
    string Content,
    IReadOnlyList<string> Topics,
    DateTime CreatedAt,
    DateTime? UpdatedAt = null
);

public record UserProfile(
    string UserId,
    string? Name,
    string? PreferredName,
    Dictionary<string, object> CustomFields
);

public record EntityMemory(
    string Id,
    string EntityId,
    EntityMemoryType Type,
    string Content,
    string? RelatedEntityId = null,
    string? RelationshipType = null,
    DateTime CreatedAt = default
);

public record SessionContext(
    string SessionId,
    LearningSessionMode Mode,
    string? Summary,
    List<Goal>? Goals = null,
    DateTime UpdatedAt = default
);

public record Goal(
    string Description,
    GoalStatus Status,
    List<string>? Steps = null,
    int? Progress = null
);

public enum GoalStatus { NotStarted, InProgress, Completed, Blocked }

public record LearnedKnowledge(
    string Id,
    string Content,
    float[]? Embedding,
    IReadOnlyList<string> Topics,
    DateTime CreatedAt = default
);

public record DecisionLogEntry(
    string Id,
    string AgentId,
    string Decision,
    string Reasoning,
    Dictionary<string, object>? Context = null,
    bool Successful = true,
    DateTime CreatedAt = default
);

public record CultureEntry(
    string Id,
    string Title,
    string Content,
    string Category,
    string? Source = null,
    int Priority = 0,
    DateTime CreatedAt = default,
    DateTime? UpdatedAt = null
);

/// <summary>Reference to a skill for agent configuration.</summary>
public record SkillRef(string SkillId, string? Source = null, Dictionary<string, object>? Params = null);

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

public record SchedulePayload(
    Dictionary<string, object>? Input = null,
    string? SessionId = null,
    // P17: when set, the schedule triggers IPlanOrchestrator.StartAsync with this request string
    string? PlanRequest = null
);

public record ScheduleRun(
    string ScheduleId,
    string RunId,
    ScheduleRunStatus Status,
    DateTime StartedAt,
    DateTime? CompletedAt = null,
    string? ErrorMessage = null
);

public record XLinkEnvelope(
    XLinkHeader Header,
    string Checksum,
    byte[] Payload,
    byte[]? Signature = null
);

public record XLinkHeader(
    string Version,
    string MessageId,
    string Source,
    long Timestamp,
    string? CorrelationId = null,
    string? Target = null,
    string ContentType = "application/msgpack",
    string? Compression = null,
    int TtlMs = 5000,
    string? Route = null
);

public record XLinkOptions(
    bool RequireChecksum = true,
    bool RequireSignature = false,
    CompressionLevel Compression = CompressionLevel.None,
    int? TtlMs = null,
    bool FireAndForget = false,
    int MaxRetries = 3
);
