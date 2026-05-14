# P01 — Core Agent Engine

## File: `AgxiomeAgent.cs`

Namespace: `AGXIOME.Core`

## Classi

### `AgxiomeAgent : IAgxiomeAgent`

```csharp
public sealed class AgxiomeAgent : IAgxiomeAgent, IAsyncDisposable
{
    // Props
    public string Id { get; }
    public AgentStatus Status { get; private set; }
    public IAgentMemory Memory { get; }
    public ILearningMachine? Learning { get; private set; }
    public string? SessionId { get; private set; }
    public ISessionState SessionState { get; private set; }

    // Privati
    private readonly LLMWIKYEngine _engine;
    private readonly ToolRegistry _tools;
    private readonly List<IAgentHook> _hooks;
    private readonly List<IGuardrail> _guardrails;
    private readonly List<IAgentMiddleware> _middleware;
    private readonly AgentConfig _config;
    private readonly ILogger _log;

    // Costruttore
    internal AgxiomeAgent(string id, LLMWIKYEngine engine, AgentConfig config)
    {
        Id = id;
        _engine = engine;
        _config = config;
        _tools = new ToolRegistry();
        _hooks = new();
        _guardrails = new();
        _middleware = new();
        Memory = new AgentMemoryManager(config.Persistence);
        SessionState = new SessionState();
        Status = AgentStatus.Idle;
    }

    // Run (sync)
    public async Task<AgentResponse> RunAsync(AgentRequest request, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested) return CancelledResponse();
        if (Status == AgentStatus.Error) return ErrorResponse("Agent in error state");

        Status = AgentStatus.Thinking;
        var sw = Stopwatch.StartNew();

        try
        {
            // 1. Enrich request
            var enriched = await EnrichRequestAsync(request, ct);

            // 2. Pre-guardrails
            await ExecuteGuardrailsAsync(enriched.Prompt, GuardrailType.Input, ct);

            // 3. Pre-hooks
            await ExecuteHooksAsync(HookPoint.PreRun, enriched, ct);

            // 4. LLM loop
            var result = await ExecuteLLMLoopAsync(enriched, ct);

            // 5. Post-guardrails
            await ExecuteGuardrailsAsync(result.Content, GuardrailType.Output, ct);

            // 6. Post-hooks
            await ExecuteHooksAsync(HookPoint.PostRun, result, ct);

            // 7. Background hooks
            _ = Task.Run(() => ExecuteHooksAsync(HookPoint.BackgroundPost, result, CancellationToken.None), ct);

            // 8. Learning
            if (Learning != null)
                await Learning.ProcessAsync(Id, enriched.Prompt, result.Content, ct);

            // 9. Persist session
            await PersistSessionAsync(enriched, result, ct);

            Status = AgentStatus.Completed;
            return result;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Agent {Id} failed", Id);
            Status = AgentStatus.Error;
            return ErrorResponse(ex.Message);
        }
        finally
        {
            sw.Stop();
            RecordMetrics("agent.run", sw.ElapsedMilliseconds, Status);
        }
    }

    // Run with streaming
    public async IAsyncEnumerable<AgentChunk> RunStreamAsync(AgentRequest request, [EnumeratorCancellation] CancellationToken ct)
    {
        Status = AgentStatus.Streaming;
        // Stream chunks via _engine.InferStreamAsync()
        // Yield AgentChunk con supporto HITL pause
    }

    // Tool registration
    public void RegisterFunction<T>() where T : AgxiomeFunction
        => _tools.Register<T>();
    public void RegisterTool(IAgxiomeTool tool)
        => _tools.Register(tool);

    // Hooks
    public void AddHook(IAgentHook hook) => _hooks.Add(hook);
    public void AddToolHook(IToolHook hook) => _tools.AddToolHook(hook);

    // Guardrails
    public void AddGuardrail(IGuardrail g) => _guardrails.Add(g);

    // Learning
    public void EnableLearning(LearningConfig? cfg = null)
        => Learning = new LearningMachine(cfg ?? new LearningConfig());

    // Structured output
    public void SetOutputSchema<T>() where T : class { /* store type */ }
    public void SetInputSchema<T>() where T : class { /* store type */ }
}
```

## `AgentConfig`

```csharp
public record AgentConfig
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string? SystemPrompt { get; init; }
    public int MaxIterations { get; init; } = 20;
    public int MaxHistorySize { get; init; } = 50;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
    public LLMWIKYConfig ReasoningEngine { get; init; } = new();
    public StorageConfig Persistence { get; init; } = new();
    public bool EnableStreaming { get; init; } = false;
    public bool EnableRetry { get; init; } = true;
    public List<ToolRegistration> Tools { get; init; } = new();
    public List<SkillReference> Skills { get; init; } = new();
    public string? AgentTemplate { get; init; }
}
```

## Metodi interni chiave

### `ExecuteLLMLoopAsync`

```
Loop:
  1. Build prompt = SystemPrompt + History + Context
  2. Inferenza LLMWIKY → response
  3. Se response ha tool call:
     3a. Pre-tool hook
     3b. Check HITL (requires_confirmation → pausa)
     3c. Exec tool
     3d. Post-tool hook
     3e. Append result al loop
  4. Se response finale → return
  5. Se max_iterations → return with warning
```

### `EnrichRequestAsync`

```
1. Carica session state
2. Carica chat history (last N)
3. Inject dipendenze
4. Applica structured input schema (se presente)
5. Return enriched AgentRequest
```

## Test plan

| Test | Cosa verifica |
|------|--------------|
| Agent creation with minimal config | Non null, status Idle |
| RunAsync returns AgentResponse | Content non vuoto |
| Tool call loop max iterations | Status = Error dopo N loop |
| Guardrail blocks PII | InputCheckError thrown |
| Pre-hook validation | InputCheckError on bad input |
| Post-hook transforms output | Output modificato |
| Learning enabled saves memory | Learning.ProcessAsync called |
| Session state persists across runs | stato mantenuto |
| Cancellation | Task cancelled, status preserved |
| Concurrent tool execution | Multi tool call in parallel |
