# HITL — Human-in-the-Loop

[Guardrails](guardrails.md) · [Hooks](hooks.md) · [RBAC](rbac.md)

HITL pauses agent execution when a tool or action requires human confirmation.

## Flow

```
Agent runs → Tool marked [RequiresConfirmation]
    │
    ▼
Agent pauses → ApprovalRequest created
    │
    ▼
Human reviews → Approves / Rejects
    │
    ▼
Agent resumes → Continues or aborts
```

## Mark a Function for Confirmation

```csharp
agent.RegisterFunction<DeleteFileFunction>();
agent.RequireConfirmationFor<DeleteFileFunction>();
```

## Approval Manager

```csharp
var approval = new PostgresApprovalManager(connectionString);

// Request approval
var request = await approval.RequestApprovalAsync(
    agentId: "agent-1",
    toolName: "delete_file",
    payload: new { path = "/important/file.txt" },
    config: new ApprovalConfig { TTL = TimeSpan.FromHours(1) });

// Human resolves
var resolution = await approval.ResolveApprovalAsync(
    approvalId: request.Id,
    approved: true,
    notes: "Deletion confirmed by admin");

// List pending
var pending = await approval.ListPendingAsync(agentId: "agent-1");
```

## Approval Config

```csharp
public record ApprovalConfig
{
    public TimeSpan TTL { get; init; } = TimeSpan.FromHours(1);
    public List<string> RequiredApprovers { get; init; } = new();
    public string? Reason { get; init; }  // Shown to the approver
}
```

## Continue After HITL

```csharp
// Agent was paused, human resolved the requirement
var hitlRequirements = new List<HITLRequirement>
{
    new("delete-file-approval", HITLRequirementStatus.Approved)
};

var response = await agent.ContinueRunAsync(runId, hitlRequirements);
```

## Next
- [Guardrails](guardrails.md)
- [Audit Trail](audit.md)
