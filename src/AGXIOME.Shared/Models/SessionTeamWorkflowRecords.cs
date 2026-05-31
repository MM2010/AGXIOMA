/// <summary>Session, team, and workflow records: Session with history, TeamContext/Response, WorkflowContext, StepResult/Type, WorkflowResult.</summary>
using AGXIOME.Shared.Interfaces;

namespace AGXIOME.Shared.Models;

public record Session(
    string Id,
    string UserId,
    string ComponentId,
    SessionType Type,
    string? Name,
    Dictionary<string, object>? State,
    Dictionary<string, string>? Metadata,
    DateTime CreatedAt,
    DateTime? UpdatedAt = null
);

public record SessionOptions(
    string? Name = null,
    Dictionary<string, string>? Metadata = null,
    bool EnableHistory = true,
    int MaxHistoryMessages = 100,
    bool EnableSummary = false
);

public record RunContext(
    string UserId,
    string SessionId,
    ISessionState SessionState,
    Dictionary<string, object>? Dependencies = null,
    Dictionary<string, object>? Metadata = null,
    Dictionary<string, string>? KnowledgeFilters = null
);

public record TeamResponse(
    string Content,
    TeamMode ModeUsed,
    IReadOnlyList<AgentResult> AgentResults,
    TeamMetadata? Metadata = null
);

public record AgentResult(
    string AgentId,
    string Role,
    AgentStatus Status,
    string? Output,
    TimeSpan ExecutionTime
);

public record TeamContext(
    string? SessionId = null,
    Dictionary<string, object>? SharedState = null,
    object? GlobalInput = null
);

public record TeamMetadata(
    int MembersUsed,
    TimeSpan TotalDuration,
    int TotalToolCalls
);

public record WorkflowResult(
    string WorkflowId,
    WorkflowStatus Status,
    IReadOnlyList<StepResult> StepResults,
    WorkflowMetadata? Metadata = null
);

public record StepResult(
    string StepName,
    StepStatus Status,
    object? Output,
    TimeSpan ExecutionTime,
    string? ErrorMessage = null
);

public record WorkflowContext(
    string? SessionId = null,
    Dictionary<string, object>? SharedState = null,
    CancellationToken CancellationToken = default
);

public record WorkflowMetadata(
    int StepsExecuted,
    TimeSpan TotalDuration,
    int TotalInferences
);

public record RunOutputEvent(
    RunEvent Event,
    string? Content = null,
    ToolCallResult? ToolCall = null,
    HITLRequirement? Requirement = null
);
