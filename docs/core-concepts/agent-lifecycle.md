# Agent Lifecycle

> How an AGXIOME agent executes from request to response.

## Pipeline Overview

```
AgentRequest
    │
    ▼
┌──────────────────────────────────────────────────┐
│ 1. PRE-HOOKS                                      │
│    IAgentHook.ExecuteAsync() — BeforeRun          │
│    Can modify context, inject instructions        │
├──────────────────────────────────────────────────┤
│ 2. INPUT GUARDRAILS                               │
│    IGuardrail.ValidateAsync() — Input             │
│    Block / Warn / Transform                       │
│    PII detection, prompt injection, moderation    │
├──────────────────────────────────────────────────┤
│ 3. TOOL HOOKS (Before)                            │
│    IToolHook.BeforeExecuteAsync()                 │
│    Inspect/modify tool inputs                     │
├──────────────────────────────────────────────────┤
│ 4. LLM INFERENCE                                  │
│    ILLMProvider.CompleteAsync() / StreamAsync()   │
│    Function calling loop (N iterations)           │
│    Token counting, cost tracking                  │
├──────────────────────────────────────────────────┤
│ 5. TOOL HOOKS (After)                             │
│    IToolHook.AfterExecuteAsync()                  │
│    Inspect/modify tool results                    │
├──────────────────────────────────────────────────┤
│ 6. OUTPUT GUARDRAILS                              │
│    IGuardrail.ValidateAsync() — Output            │
│    Content validation, toxicity check             │
├──────────────────────────────────────────────────┤
│ 7. STRUCTURED OUTPUT (if configured)              │
│    IOutputParser.ParseAsync<T>()                  │
│    Deserialize JSON → typed object                │
├──────────────────────────────────────────────────┤
│ 8. LEARNING (if enabled)                          │
│    ILearningMachine.ProcessAsync()                │
│    Extract preferences, entity facts              │
├──────────────────────────────────────────────────┤
│ 9. POST-HOOKS                                     │
│    IAgentHook.ExecuteAsync() — AfterRun           │
│    Audit logging, metrics, notifications          │
├──────────────────────────────────────────────────┤
│ 10. RETURN                                        │
│    AgentResponse { Content, ToolCalls, RunId }    │
└──────────────────────────────────────────────────┘
```

## Agent States

```
Idle ──→ Running ──→ Completed
  │         │
  │         ├──→ Paused (HITL waiting)
  │         │       │
  │         │       └──→ ContinueRunAsync() ──→ Running
  │         │
  │         └──→ Failed
  │
  └──→ Disposed
```

## Run Methods

| Method | Returns | Use Case |
|--------|---------|----------|
| `RunAsync(request)` | `AgentResponse` | Single-turn chat |
| `RunAsync<T>(request)` | `T` | Structured output |
| `RunStreamAsync(request)` | `IAsyncEnumerable<AgentChunk>` | Real-time streaming |
| `RunEventStreamAsync(request)` | `IAsyncEnumerable<RunOutputEvent>` | Granular events |
| `ContinueRunAsync(runId, hitl)` | `AgentResponse` | Resume paused run |

## Auto-Loops

Controls how many LLM → Tool → LLM cycles the agent can perform:

```csharp
var agent = AgxiomeBuilder.Agent("agent")
    .WithAutoLoops(upperBound: 50)  // max 50 LLM calls per run
    .Build();
```

Each tool call by the LLM counts as one loop. After `upperBound` loops, the agent returns whatever it has generated so far.

## Cancellation

```csharp
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
var response = await agent.RunAsync(request, cts.Token);
// Throws OperationCanceledException after 30 seconds
```

## Thread Safety

- Multiple `RunAsync()` calls on the same agent instance are **supported concurrently**
- Configuration methods (`RegisterTool`, `AddGuardrail`, `AddHook`) should be called **before** first run
- Agent memory (`IAgentMemory`) is **thread-safe** (backed by `ConcurrentDictionary`)

## Next

- [AgxiomeBuilder API](agxiome-builder.md)
- [Teams & Swarms](../../teams/topologies.md)
