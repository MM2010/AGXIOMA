# Hooks
[Lifecycle hooks](core-concepts/agent-lifecycle.md) · [Guardrails](guardrails.md) · [HITL](hitl.md)

Hooks fire at specific points in the agent lifecycle. Use them for observability, audit logging, and custom logic.

## Hook Points

| Point | When | Use Case |
|-------|------|----------|
| `BeforeRun` | Before agent execution | Inject context, validate state |
| `AfterRun` | After agent completes | Log results, emit metrics |
| `OnError` | Agent throws exception | Alerting, error tracking |
| `OnStreamChunk` | Each streaming chunk | Real-time monitoring |
| `BeforeToolCall` | Before tool execution | Validate tool inputs |
| `AfterToolCall` | After tool execution | Log tool usage |

## Custom Hook

```csharp
public class AuditHook : IAgentHook
{
    public string Name => "audit";
    public HookPoint Point => HookPoint.AfterRun;
    public int Order => 0;
    public bool RunInBackground => false;

    public async Task ExecuteAsync(HookContext context, CancellationToken ct)
    {
        await File.AppendAllTextAsync("audit.log",
            $"[{DateTime.UtcNow:O}] Agent={context.AgentId} " +
            $"Status={context.Status} Duration={context.Duration}ms\n");
    }
}

agent.AddHook(new AuditHook());
```

## Tool Hooks

```csharp
public class ToolMetricsHook : IToolHook
{
    public Task BeforeExecuteAsync(ToolContext ctx, CancellationToken ct)
    {
        ctx.Metadata["start"] = DateTime.UtcNow;
        return Task.CompletedTask;
    }

    public async Task AfterExecuteAsync(ToolContext ctx, ToolResult result, CancellationToken ct)
    {
        var start = (DateTime)ctx.Metadata["start"];
        var duration = (DateTime.UtcNow - start).TotalMilliseconds;
        Console.WriteLine($"Tool {ctx.ToolName}: {duration}ms, Success={result.Success}");
    }
}

agent.AddToolHook(new ToolMetricsHook());
```

## Next
- [Guardrails](guardrails.md) — Content validation
- [HITL](hitl.md) — Human-in-the-Loop
