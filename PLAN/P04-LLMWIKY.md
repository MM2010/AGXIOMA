# P04 — LLMWIKY Engine

## File: `LLMWIKYEngine.cs`

Namespace: `AGXIOME.LLMWIKY`

## Classi

### `LLMWIKYEngine : ILLMWIKYEngine`

```csharp
public sealed class LLMWIKYEngine : ILLMWIKYEngine
{
    private readonly ILLMProvider _provider;
    private readonly ILLMProvider? _fallbackProvider;
    private readonly LLMWIKYConfig _config;
    private readonly ProviderCostTracker _costTracker;

    public LLMWIKYEngine(LLMWIKYConfig config)
    {
        _config = config;
        _provider = LLMProviderFactory.Create(config);
        _fallbackProvider = config.FallbackProviders?.FirstOrDefault() is { } fb
            ? LLMProviderFactory.Create(fb) : null;
        _costTracker = new ProviderCostTracker();
    }

    public async Task<LLMWIKYResponse> InferAsync(
        LLMWIKYRequest request, CancellationToken ct)
    {
        try
        {
            return await _provider.CompleteAsync(request, ct);
        }
        catch (Exception ex) when (_fallbackProvider != null
            && ex is TimeoutException or HttpRequestException)
        {
            Log.Warning("Primary provider failed, fallback to {Name}", _fallbackProvider.Name);
            return await _fallbackProvider.CompleteAsync(request, ct);
        }
    }

    public IAsyncEnumerable<LLMWIKYChunk> InferStreamAsync(
        LLMWIKYRequest request, CancellationToken ct)
        => _provider.CompleteStreamAsync(request, ct);

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct)
        => await _provider.EmbedAsync(text, ct);

    public async Task<float[][]> EmbedBatchAsync(
        IReadOnlyList<string> texts, CancellationToken ct)
        => await _provider.EmbedBatchAsync(texts, ct);
}
```

### `LLMWIKYRequest` / `LLMWIKYResponse` / `LLMWIKYChunk`

```csharp
public record LLMWIKYRequest(
    string SystemPrompt,
    string UserPrompt,
    IReadOnlyList<AgentMessage>? History = null,
    double? Temperature = 0.7,
    int? MaxTokens = 4096,
    IReadOnlyList<JsonObject>? Tools = null
);

public record LLMWIKYResponse(
    string Content,
    string? ToolCallId = null,
    string? ToolName = null,
    string? ToolArguments = null,
    LLMWIKYUsage Usage = default
);

public record LLMWIKYChunk(
    string Content,
    bool IsToolCall = false,
    string? ToolCallId = null,
    bool IsFinal = false
);
```

### `LLMWIKYConfig`

```csharp
public record LLMWIKYConfig
{
    public string Provider { get; init; } = "OpenAI";
    public string Model { get; init; } = "gpt-4o";
    public string? ApiKey { get; init; }
    public string? Endpoint { get; init; }
    public string? ApiVersion { get; init; }
    public string? OrganizationId { get; init; }
    public double DefaultTemperature { get; init; } = 0.7;
    public int MaxTokens { get; init; } = 4096;
    public int MaxRetries { get; init; } = 3;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(60);
    public Dictionary<string, object> ProviderOptions { get; init; } = new();
    public bool EnableAdaptiveRateLimit { get; init; } = true;
    public int MaxTokensPerMinute { get; init; } = 100_000;
    public int MaxRequestsPerMinute { get; init; } = 500;
    public List<LLMWIKYConfig>? FallbackProviders { get; init; }
    public bool EnableCostTracking { get; init; } = true;
}
```

## Function calling flow

```
1. Tool registrato → JSON schema via ToJsonSchema()
2. LLMWIKYRequest.Tools = schemas
3. LLM risponde con tool_call o text
4. Engine match function by name
5. Deserialize args → execute → append result
6. Nuova inferenza con tool result nel contesto
```

## Reasoning (Agno compat)

```csharp
// 3 modalità:
public enum ReasoningMode
{
    None,               // No reasoning
    ReasoningModel,     // Usa modello nativo (DeepSeek-R1, O3)
    ReasoningAgent,     // reasoning=True → chain-of-thought via prompt
    ReasoningTools      // think() + analyze() tool
}

public record ReasoningConfig(
    ReasoningMode Mode = ReasoningMode.None,
    string? ReasoningModel = null,  // Provider diverso per reasoning
    bool ShowFullReasoning = false
);
```

## Test

| Test | Descrizione |
|------|------------|
| InferAsync returns content | Response.Content non vuoto |
| Tool call detected | ToolName non null |
| Fallback su timeout | Secondo provider chiamato |
| Streaming produce chunk | Almeno 1 chunk |
| Embedding dimensions corrette | Length match config |
