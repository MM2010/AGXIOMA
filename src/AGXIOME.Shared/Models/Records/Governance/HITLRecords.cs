namespace AGXIOME.Shared.Models;

/// <summary>A pending human-in-the-loop pause that blocks agent execution until resolved.</summary>
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

/// <summary>An approval request raised by an agent tool requiring human sign-off.</summary>
public record ApprovalRequest(
    string Id,
    string AgentId,
    string ToolName,
    object Payload,
    ApprovalStatus Status,
    ApprovalConfig? Config = null,
    DateTime CreatedAt = default
);

/// <summary>Configuration for an approval workflow including timeout and audit requirements.</summary>
public record ApprovalConfig(
    TimeSpan? Timeout = null,
    bool RequiresAuditTrail = true,
    string? NotifyEndpoint = null,
    List<string>? RequiredApprovers = null
);

/// <summary>Resolution of an approval request (approved or rejected) with optional notes.</summary>
public record ApprovalResolution(bool Approved, string? Notes = null);
