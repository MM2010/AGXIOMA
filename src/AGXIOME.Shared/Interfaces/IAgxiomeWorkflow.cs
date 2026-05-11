using AGXIOME.Shared.Models;

namespace AGXIOME.Shared.Interfaces;

public interface IAgxiomeWorkflow
{
    string Id { get; }
    WorkflowStatus Status { get; }
    IWorkflowStep AddStep(string name, Func<WorkflowContext, Task> handler);
    IWorkflowStep AddAgentStep(string name, IAgxiomeAgent agent);
    void AddTransition(string from, string to, Func<WorkflowContext, bool>? condition = null);
    Task<WorkflowResult> ExecuteAsync(WorkflowContext context, CancellationToken ct = default);
}

public interface IWorkflowStep
{
    string Name { get; }
    StepType Type { get; }
    Task<StepResult> ExecuteAsync(WorkflowContext ctx, CancellationToken ct = default);
}
