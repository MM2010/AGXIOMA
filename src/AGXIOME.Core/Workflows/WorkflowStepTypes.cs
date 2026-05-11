using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Core.Workflows;

/// <summary>Conditional branch: executes if_true or if_else based on evaluator.</summary>
public sealed class ConditionalBranch : IWorkflowStep
{
    public string Name { get; }
    public StepType Type => StepType.Condition;

    private readonly Func<WorkflowContext, bool> _evaluator;
    private readonly IWorkflowStep? _ifTrue;
    private readonly IWorkflowStep? _ifElse;

    public ConditionalBranch(string name, Func<WorkflowContext, bool> evaluator, IWorkflowStep? ifTrue = null, IWorkflowStep? ifElse = null)
    {
        Name = name; _evaluator = evaluator; _ifTrue = ifTrue; _ifElse = ifElse;
    }

    public async Task<StepResult> ExecuteAsync(WorkflowContext ctx, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var condition = _evaluator(ctx);
            var step = condition ? _ifTrue : _ifElse;
            if (step != null)
                return await step.ExecuteAsync(ctx, ct);
            sw.Stop();
            return new StepResult(Name, StepStatus.Completed, $"Condition: {condition}", sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new StepResult(Name, StepStatus.Failed, null, sw.Elapsed, ex.Message);
        }
    }
}

/// <summary>Parallel execution: runs all child steps concurrently.</summary>
public sealed class ParallelStep : IWorkflowStep
{
    public string Name { get; }
    public StepType Type => StepType.Parallel;

    private readonly IReadOnlyList<IWorkflowStep> _steps;

    public ParallelStep(string name, params IWorkflowStep[] steps) { Name = name; _steps = steps; }

    public async Task<StepResult> ExecuteAsync(WorkflowContext ctx, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var results = await Task.WhenAll(_steps.Select(s => s.ExecuteAsync(ctx, ct)));
            sw.Stop();
            return new StepResult(Name, StepStatus.Completed,
                string.Join("\n---\n", results.Select(r => r.Output?.ToString() ?? "")), sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new StepResult(Name, StepStatus.Failed, null, sw.Elapsed, ex.Message);
        }
    }
}

/// <summary>Loop: iterates a body step until max iterations or condition met.</summary>
public sealed class LoopStep : IWorkflowStep
{
    public string Name { get; }
    public StepType Type => StepType.Loop;

    private readonly IWorkflowStep _body;
    private readonly int _maxIterations;
    private readonly Func<WorkflowContext, bool>? _endCondition;

    public LoopStep(string name, IWorkflowStep body, int maxIterations = 10, Func<WorkflowContext, bool>? endCondition = null)
    {
        Name = name; _body = body; _maxIterations = maxIterations; _endCondition = endCondition;
    }

    public async Task<StepResult> ExecuteAsync(WorkflowContext ctx, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var outputs = new List<string>();

        for (int i = 0; i < _maxIterations && !ct.IsCancellationRequested; i++)
        {
            if (_endCondition?.Invoke(ctx) == true) break;
            var result = await _body.ExecuteAsync(ctx, ct);
            outputs.Add($"[Iteration {i + 1}]: {result.Output}");
        }

        sw.Stop();
        return new StepResult(Name, StepStatus.Completed, string.Join("\n", outputs), sw.Elapsed);
    }
}

/// <summary>Router: selects next step based on evaluator function.</summary>
public sealed class RouterStep : IWorkflowStep
{
    public string Name { get; }
    public StepType Type => StepType.Router;

    private readonly Func<WorkflowContext, string> _selector;
    private readonly Dictionary<string, IWorkflowStep> _routes;

    public RouterStep(string name, Func<WorkflowContext, string> selector, Dictionary<string, IWorkflowStep> routes)
    {
        Name = name; _selector = selector; _routes = routes;
    }

    public async Task<StepResult> ExecuteAsync(WorkflowContext ctx, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var route = _selector(ctx);
            if (_routes.TryGetValue(route, out var step))
                return await step.ExecuteAsync(ctx, ct);
            sw.Stop();
            return new StepResult(Name, StepStatus.Completed, $"Route '{route}' not found, skipped", sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new StepResult(Name, StepStatus.Failed, null, sw.Elapsed, ex.Message);
        }
    }
}

/// <summary>CEL-style condition evaluator for workflow routing.</summary>
public sealed class CELConditionEvaluator
{
    public bool Evaluate(string expression, WorkflowContext ctx, string? stepOutput = null)
    {
        // Simple expression parser: input.Contains("..."), session_state.key < N, etc.
        if (expression.Contains("Contains(") && stepOutput != null)
        {
            var start = expression.IndexOf('"') + 1;
            var end = expression.LastIndexOf('"');
            if (start > 0 && end > start)
            {
                var needle = expression[start..end];
                return stepOutput.Contains(needle, StringComparison.OrdinalIgnoreCase);
            }
        }

        if (expression.StartsWith("session_state.") && ctx.SharedState != null)
        {
            var key = expression.Split('.')[1].Split(' ')[0];
            return ctx.SharedState.ContainsKey(key);
        }

        return false;
    }
}
