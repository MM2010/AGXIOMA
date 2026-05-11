# P07 — Workflow Engine

## File: `WorkflowEngine.cs`

Namespace: `AGXIOME.Core.Workflows`

## Classi

### `AgxiomeWorkflow : IAgxiomeWorkflow`

```csharp
public sealed class AgxiomeWorkflow : IAgxiomeWorkflow
{
    public string Id { get; }
    public WorkflowStatus Status { get; private set; }

    // DAG: adjacency list
    private readonly Dictionary<string, IWorkflowStep> _steps = new();
    private readonly Dictionary<string, List<(string To, Func<WorkflowContext, bool>?)>> _transitions = new();

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
        _transitions.GetOrAdd(from, _ => new()).Add((to, condition));
    }

    public async Task<WorkflowResult> ExecuteAsync(WorkflowContext ctx, CancellationToken ct)
    {
        // Topological sort → execute in order
        // Condizioni: skip if false
        // Parallel: Task.WhenAll
        // Loop: max_iterations o end condition
        // HITL: pausa su step con confirmation
    }
}
```

### Step types

```csharp
public interface IWorkflowStep
{
    string Name { get; }
    StepType Type { get; }
    Task<StepResult> ExecuteAsync(WorkflowContext ctx, CancellationToken ct);
}

public enum StepType { Agent, Function, Condition, Parallel, Loop, Router, Human, Delay }

public class AgentWorkflowStep : IWorkflowStep { /* Esegue un agente */ }
public class FunctionWorkflowStep : IWorkflowStep { /* Esegue una funzione C# */ }
public class ConditionalBranch : IWorkflowStep { /* If/else su condizione */ }
public class ParallelStep : IWorkflowStep { /* Fan-out N step */ }
public class LoopStep : IWorkflowStep { /* Iterazione */ }
public class RouterStep : IWorkflowStep { /* Sceglie step successivo */ }
```

### Custom Function Executor (Agno compat)

```csharp
public class CustomExecutor
{
    // Step con executor custom:
    // StepInput → StepOutput standardizzati
    public async Task<StepOutput> ExecuteAsync(StepInput input, CancellationToken ct)
    {
        // input.Input: contenuto dallo step precedente
        // input.AdditionalData: metadata extra
        return new StepOutput($"Processed: {input.Input}");
    }
}

public record StepInput(string? Input, Dictionary<string, object>? AdditionalData = null);
public record StepOutput(string Content, object? Data = null, bool Stop = false);
```

## CEL Expressions (workflow conditions)

```csharp
// Condizioni stile Agno CEL:
// - input.Contains("urgent")
// - previous_step_outputs["classifier"].Contains("technical")
// - session_state.retry_count < 3
// - step_choices[0]
// Implementazione: parser regex + reflection su contesto

public class CELConditionEvaluator
{
    public bool Evaluate(string expression, WorkflowContext ctx)
    {
        // Rimpiazza variabili: input → ctx.Input
        // Valuta: contains, comparison, boolean
    }
}
```

## Test

| Test | Cosa verifica |
|------|--------------|
| Sequential execution | Steps in ordine |
| Conditional skip | Step saltato se condizione false |
| Parallel execution | Steps eseguiti concurrently |
| Loop max iterations | Stop dopo N |
| CEL condition evaluation | true/false corretto |
| Workflow with HITL step | Pausa e resume |
| Nested workflow | Inner eseguito come step |
