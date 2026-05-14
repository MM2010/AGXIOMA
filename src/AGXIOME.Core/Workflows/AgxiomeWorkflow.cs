using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Core.Workflows;

public sealed class AgxiomeWorkflow : IAgxiomeWorkflow
{
    public string Id { get; }
    public WorkflowStatus Status { get; private set; } = WorkflowStatus.Created;

    private readonly Dictionary<string, IWorkflowStep> _steps = new();
    private readonly Dictionary<string, List<(string To, Func<WorkflowContext, bool>? Condition)>> _transitions = new();

    public AgxiomeWorkflow(string name) => Id = name;

    public IWorkflowStep AddStep(string name, Func<WorkflowContext, Task> handler)
    {
        var step = new FunctionWorkflowStep(name, handler);
        _steps[name] = step;
        return step;
    }

    public IWorkflowStep AddAgentStep(string name, IAgxiomeAgent agent)
    {
        var step = new AgentWorkflowStep(name, agent);
        _steps[name] = step;
        return step;
    }

    public void AddTransition(string from, string to, Func<WorkflowContext, bool>? condition = null)
    {
        if (!_transitions.ContainsKey(from)) _transitions[from] = new();
        _transitions[from].Add((to, condition));
    }

    public async Task<WorkflowResult> ExecuteAsync(WorkflowContext context, CancellationToken ct = default)
    {
        Status = WorkflowStatus.Running;
        var results = new List<StepResult>();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Topological sort from entry steps (no incoming edges)
        var entrySteps = _steps.Keys.Where(s => !_transitions.Values.Any(t => t.Any(x => x.To == s))).ToList();
        var visited = new HashSet<string>();
        var queue = new Queue<string>(entrySteps);

        while (queue.Count > 0 && !ct.IsCancellationRequested)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current)) continue;
            if (!_steps.TryGetValue(current, out var step)) continue;

            var stepSw = System.Diagnostics.Stopwatch.StartNew();
            StepResult? result = null;
            try
            {
                var stepResult = await step.ExecuteAsync(context, ct);
                result = stepResult with { StepName = current };
            }
            catch (Exception ex)
            {
                result = new StepResult(current, StepStatus.Failed, null, stepSw.Elapsed, ex.Message);
            }
            results.Add(result);

            // Follow transitions
            if (_transitions.TryGetValue(current, out var transitions))
            {
                foreach (var (next, condition) in transitions)
                {
                    if (condition == null || condition(context))
                        queue.Enqueue(next);
                }
            }
        }

        sw.Stop();
        Status = results.Any(r => r.Status == StepStatus.Failed) ? WorkflowStatus.Failed : WorkflowStatus.Completed;
        return new WorkflowResult(Id, Status, results, new WorkflowMetadata(results.Count, sw.Elapsed, results.Count));
    }
}

public sealed class FunctionWorkflowStep : IWorkflowStep
{
    public string Name { get; }
    public StepType Type => StepType.Function;
    private readonly Func<WorkflowContext, Task> _handler;

    public FunctionWorkflowStep(string name, Func<WorkflowContext, Task> handler)
    {
        Name = name; _handler = handler;
    }

    public async Task<StepResult> ExecuteAsync(WorkflowContext ctx, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await _handler(ctx);
            sw.Stop();
            return new StepResult(Name, StepStatus.Completed, null, sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new StepResult(Name, StepStatus.Failed, null, sw.Elapsed, ex.Message);
        }
    }
}

public sealed class AgentWorkflowStep : IWorkflowStep
{
    public string Name { get; }
    public StepType Type => StepType.Agent;
    private readonly IAgxiomeAgent _agent;

    public AgentWorkflowStep(string name, IAgxiomeAgent agent) { Name = name; _agent = agent; }

    public async Task<StepResult> ExecuteAsync(WorkflowContext ctx, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var response = await _agent.RunAsync(new AgentRequest(ctx.SharedState?.GetValueOrDefault("prompt")?.ToString() ?? "continue"), ct);
            sw.Stop();
            return new StepResult(Name, response.Status == AgentStatus.Completed ? StepStatus.Completed : StepStatus.Failed,
                response.Content, sw.Elapsed);
        }
        catch (Exception ex) { sw.Stop(); return new StepResult(Name, StepStatus.Failed, null, sw.Elapsed, ex.Message); }
    }
}
