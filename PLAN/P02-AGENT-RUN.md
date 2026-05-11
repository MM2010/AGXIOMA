# P02 — Agent Run + Streaming

## File: `AgentExecutor.cs`

Namespace: `AGXIOME.Core.Execution`

## Classi

### `AgentExecutor`

```csharp
public sealed class AgentExecutor
{
    private readonly AgxiomeAgent _agent;

    // Run + streaming in un unico executor
    public async Task<AgentResponse> RunAsync(
        AgentRequest request, CancellationToken ct)
    {
        // Delega al chunker, bufferizza, return
    }

    public IAsyncEnumerable<AgentChunk> RunStreamAsync(
        AgentRequest request, CancellationToken ct)
    {
        // 1. Pre-validate + guardrail
        // 2. Stream LLMWIKY chunks
        // 3. Se tool call: pausa stream, exec tool, resume stream
        // 4. Se HITL: yield Paused chunk, wait resume
    }
}
```

### `AgentResponse` / `AgentRequest` / `AgentChunk`

```csharp
public record AgentRequest(
    string Prompt,
    AgentContext? Context = null,
    IReadOnlyList<AgentMessage>? History = null,
    AgentRequestOptions? Options = null
);

public record AgentResponse(
    string Content,
    AgentStatus Status,
    IReadOnlyList<ToolCallResult>? ToolResults,
    AgentMetadata Metadata
);

public record AgentChunk(
    string Content,
    string? ToolCallId = null,
    bool IsFinal = false,
    bool IsPaused = false,
    IReadOnlyList<HITLRequirement>? ActiveRequirements = null
);
```

### `RunOutputEvent` (Agno compat)

```csharp
public enum RunEvent { RunStart, RunContent, ToolCallStart, ToolCallEnd, RunPaused, RunCompleted, RunError }

public record RunOutputEvent(
    RunEvent Event,
    string? Content = null,
    ToolCallResult? ToolCall = null,
    HITLRequirement? Requirement = null
);
```

## Streaming patterns

### Sync stream
```csharp
foreach (var chunk in agent.RunStreamAsync(request))
    Process(chunk);
```

### Event stream
```csharp
await foreach (var evt in agent.RunEventStreamAsync(request))
{
    if (evt.Event == RunEvent.RunPaused)
        // Handle HITL
    else if (evt.Event == RunEvent.RunContent)
        Display(evt.Content);
}
```

### Background + resume (Agno compat)
```csharp
var run = await agent.RunAsync(request, background: true);
// run.RunId → polling
var resumed = await agent.ResumeRunAsync(run.RunId, ct);
```

## HITL pause durante streaming

```csharp
// Quando tool richiede conferma:
yield return new AgentChunk("", IsPaused: true,
    ActiveRequirements: new[] { requirement });

// Il chiamante risolve:
requirement.Confirm();
// Chiama continue:
var response = await agent.ContinueRunAsync(runId, requirements, ct);
```

## Test

| Test | Descrizione |
|------|------------|
| Stream produces chunks | Almeno 1 chunk prima del final |
| Stream respects cancellation | Throw dopo cancel |
| Event stream events order | RunStart → Content → RunCompleted |
| HITL pause in stream | Chunk con IsPaused true |
| Run + background resume | Stesso output di Run normale |
