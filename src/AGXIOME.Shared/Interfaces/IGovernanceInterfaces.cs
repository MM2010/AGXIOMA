using AGXIOME.Shared.Models;

namespace AGXIOME.Shared.Interfaces;

public interface IGuardrail
{
    string Name { get; }
    GuardrailType Type { get; }
    GuardrailSeverity Severity { get; }
    Task<GuardrailResult> ValidateAsync(string content, GuardrailContext ctx, CancellationToken ct = default);
}

public interface IAgentHook
{
    string Name { get; }
    HookPoint Point { get; }
    int Order { get; }
    bool RunInBackground { get; }
    Task ExecuteAsync(HookContext context, CancellationToken ct = default);
}

public interface IToolHook
{
    Task BeforeExecuteAsync(Models.ToolContext ctx, CancellationToken ct = default);
    Task AfterExecuteAsync(Models.ToolContext ctx, Models.ToolResult result, CancellationToken ct = default);
}

public interface IAgentMiddleware
{
    Task InvokeAsync(AgentContext context, Func<Task> next);
}

public interface IApprovalManager
{
    Task<ApprovalRequest> RequestApprovalAsync(string agentId, string toolName, object payload, ApprovalConfig config, CancellationToken ct = default);
    Task<ApprovalResolution> ResolveApprovalAsync(string approvalId, bool approved, string? notes = null, CancellationToken ct = default);
    Task<IReadOnlyList<ApprovalRequest>> ListPendingAsync(string? agentId = null, CancellationToken ct = default);
}
