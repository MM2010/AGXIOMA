# P15 — Human-in-the-Loop (HITL)

## File: `HITL/HITLManager.cs`

Namespace: `AGXIOME.Core.HITL`

## Classi

### `RequiresConfirmationAttribute`

```csharp
[AttributeUsage(AttributeTargets.Class)]
public class RequiresConfirmationAttribute : Attribute
{
    public string? Message { get; set; }
    public bool RequiresUserInput { get; set; }
    public bool ExternalExecution { get; set; }
}
```

### `HITLManager`

```csharp
public sealed class HITLManager
{
    private readonly ConcurrentDictionary<string, HITLRequirement> _pending = new();

    public HITLRequirement CreateRequirement(string runId, string agentId,
        string toolName, object payload, HITLRequirementType type)
    {
        var req = new HITLRequirement(runId, agentId, toolName, payload, type);
        _pending[req.Id] = req;
        return req;
    }

    public HITLRequirement? GetRequirement(string id)
        => _pending.GetValueOrDefault(id);

    public void Resolve(string id, bool approved, string? notes = null)
    {
        if (_pending.TryGetValue(id, out var req))
        {
            req.Resolve(approved, notes);
            _pending.TryRemove(id, out _);
        }
    }

    public IReadOnlyList<HITLRequirement> GetPendingForRun(string runId)
        => _pending.Values.Where(r => r.RunId == runId && r.Status == HITLStatus.Pending).ToList();
}
```

### `HITLRequirement`

```csharp
public sealed class HITLRequirement
{
    public string Id { get; }
    public string RunId { get; }
    public string AgentId { get; }
    public string ToolName { get; }
    public object Payload { get; }
    public HITLRequirementType Type { get; }
    public HITLStatus Status { get; private set; }
    public string? MemberAgentName { get; }  // Per team
    public bool NeedsConfirmation => Type == HITLRequirementType.Confirmation;
    public bool NeedsUserInput => Type == HITLRequirementType.UserInput;
    public bool IsExternalToolExecution => Type == HITLRequirementType.ExternalExecution;

    public void Confirm() => Resolve(true);
    public void Reject() => Resolve(false);
    public void Resolve(bool approved, string? notes = null)
    {
        Status = approved ? HITLStatus.Approved : HITLStatus.Rejected;
    }
}

public enum HITLRequirementType { Confirmation, UserInput, ExternalExecution, Audit }
public enum HITLStatus { Pending, Approved, Rejected, Expired }
```

### `ApprovalManager` (admin audit)

```csharp
public sealed class ApprovalManager : IApprovalManager
{
    public async Task<ApprovalRequest> RequestApprovalAsync(...)
    {
        // Salva in DB con audit trail persistente
        // Type: Standard (pausa) | Audit (logga e prosegue)
    }

    public async Task<ApprovalResolution> ResolveApprovalAsync(string id, bool approved, ...)
    {
        // Update DB record, logga risoluzione
    }
}

public record ApprovalRequest(string Id, string AgentId, string ToolName, object Payload,
    ApprovalStatus Status, ApprovalConfig Config, DateTime CreatedAt);
public enum ApprovalStatus { Pending, Approved, Rejected, Expired }
```

## Resume execution (Agno compat)

```csharp
// Dopo HITL, chiama:
var response = await agent.ContinueRunAsync(runId, requirements, ct);
// Riprende dal punto esatto di pausa
```

## Test

| Test | Descrizione |
|------|------------|
| Tool with RequiresConfirmation | Agent run paused |
| Confirm requirement | Run resumes, tool called |
| Reject requirement | Run resumes, tool skipped |
| External execution | Tool not called by agent |
| User input collection | Input captured before resume |
| Team member tool HITL | MemberAgentName populated |
| Approval audit trail | DB record created |
