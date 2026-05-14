# P14 — Hooks System

## File: `Hooks/Hooks.cs`

Namespace: `AGXIOME.Core.Hooks`

## Classi

### `IAgentHook` / `HookPoint`

```csharp
public interface IAgentHook
{
    string Name { get; }
    HookPoint Point { get; }
    int Order { get; }
    bool RunInBackground { get; }
    Task ExecuteAsync(HookContext context, CancellationToken ct);
}

public enum HookPoint
{
    PreRun,           // Dopo caricamento sessione, prima LLM
    PostRun,          // Dopo risposta, prima return
    PreTool,          // Prima di ogni tool call
    PostTool,         // Dopo ogni tool call
    StreamChunk,      // Ogni chunk in streaming
    BackgroundPost    // Async, non bloccante
}

public record HookContext(
    string AgentId,
    string RunId,
    HookPoint Point,
    object? Input = null,
    object? Output = null,
    Exception? Error = null,
    ISessionState? SessionState = null
);

// Hook parameters injection (Agno compat):
// run_input / run_output / agent / session / run_context / user_id / debug_mode
public class HookParameterInjector
{
    // Riflessione: se hook ha parametro RunInput → inietta
    // Se ha parametro Agent → inietta istanza agente
}
```

### IToolHook

```csharp
public interface IToolHook
{
    Task BeforeExecuteAsync(ToolContext ctx, CancellationToken ct);
    Task AfterExecuteAsync(ToolContext ctx, ToolResult result, CancellationToken ct);
}
```

### @hook decorator pattern (C# attribute)

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class HookAttribute : Attribute
{
    public bool RunInBackground { get; set; }
    public HookPoint Point { get; set; } = HookPoint.PostRun;
}
```

## Test

| Test | Descrizione |
|------|------------|
| Pre-hook modifies input | Input cambiato prima LLM |
| Post-hook validates output | OutputCheckError |
| Background hook non bloccante | Response prima del hook |
| Tool hook pre/post | Chiamati prima/dopo tool |
| Hook ordering | Ordine rispettato |
