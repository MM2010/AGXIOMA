using AGXIOME.Shared.Models;

namespace AGXIOME.Shared.Interfaces;

/// <summary>
/// DAG-based workflow engine. Steps are connected by transitions with optional conditions.
/// Supports both code steps (lambda handlers) and agent steps (delegating to agents).
/// </summary>
public interface IAgxiomeWorkflow
{
    string Id { get; }
    WorkflowStatus Status { get; }
    IWorkflowStep AddStep(string name, Func<WorkflowContext, Task> handler);
    IWorkflowStep AddAgentStep(string name, IAgxiomeAgent agent);
    void AddTransition(string from, string to, Func<WorkflowContext, bool>? condition = null);
    Task<WorkflowResult> ExecuteAsync(WorkflowContext context, CancellationToken ct = default);
}

/// <summary>A single step within a workflow. Can be a code step or agent step.</summary>
public interface IWorkflowStep
{
    string Name { get; }
    StepType Type { get; }
    Task<StepResult> ExecuteAsync(WorkflowContext ctx, CancellationToken ct = default);
}
