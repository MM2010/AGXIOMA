# P05 — LLM Provider Implementations

## File: `Providers/{Name}Provider.cs`

Namespace: `AGXIOME.LLMWIKY.Providers`

## Provider Interface

```csharp
public interface ILLMProvider
{
    string Name { get; }
    LLMProviderCapabilities Capabilities { get; }
    Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest request, CancellationToken ct);
    IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest request, CancellationToken ct);
    Task<float[]> EmbedAsync(string text, CancellationToken ct);
    Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct);
    int EmbeddingDimensions { get; }
}

public record LLMCompletionRequest(
    string SystemPrompt,
    string UserPrompt,
    IReadOnlyList<AgentMessage>? History,
    double? Temperature,
    int? MaxTokens,
    string Model,
    IReadOnlyList<JsonObject>? Tools
);

public record LLMProviderCapabilities(
    bool SupportsStreaming,
    bool SupportsFunctionCalling,
    bool SupportsVision,
    int EmbeddingDimensions,
    int MaxContextTokens,
    IReadOnlyList<string> SupportedModels
);
```

## Provider Factory

```csharp
public static class LLMProviderFactory
{
    private static readonly Dictionary<string, Func<LLMWIKYConfig, ILLMProvider>> _registry = new()
    {
        ["OpenAI"]     = c => new OpenAIProvider(c),
        ["Anthropic"]  = c => new AnthropicProvider(c),
        ["DeepSeek"]   = c => new DeepSeekProvider(c),
        ["Ollama"]     = c => new OllamaProvider(c),
        ["Local"]      = c => new LocalProvider(c),
        ["AzureOpenAI"]= c => new AzureOpenAIProvider(c),
        ["Gemini"]     = c => new GeminiProvider(c),
        ["Custom"]     = c => new CustomProvider(c),
    };

    public static void Register(string name, Func<LLMWIKYConfig, ILLMProvider> factory)
        => _registry[name] = factory;

    public static ILLMProvider Create(LLMWIKYConfig config)
        => _registry.TryGetValue(config.Provider, out var factory)
            ? factory(config)
            : throw new ArgumentException($"Unknown provider: {config.Provider}");
}
```

## Provider Implementations

### `OpenAIProvider`

```
HTTP: POST {endpoint}/v1/chat/completions (o Responses API)
Headers: Authorization: Bearer {apiKey}
Body: model, messages, tools, temperature, stream
Response: choices[0].message.content + tool_calls
Embedding: POST {endpoint}/v1/embeddings → data[0].embedding
```

### `AnthropicProvider`

```
HTTP: POST https://api.anthropic.com/v1/messages
Headers: x-api-key, anthropic-version
Body: model, messages, tools, temperature, stream (SSE)
Response: content[] con type=text|tool_use
Embedding: NON supportata nativamente → fallback a altro provider
```

### `DeepSeekProvider`

```
HTTP: POST https://api.deepseek.com/v1/chat/completions
API compatibile OpenAI → riusa OpenAIProvider con endpoint custom
```

### `OllamaProvider`

```
HTTP: POST {endpoint}/api/chat (Ollama API)
Body: model, messages, stream, options
Response: message.content
Embedding: POST /api/embeddings
```

### `LocalProvider`

```
HTTP: endpoint OpenAI-compat (vLLM, TGI, llama.cpp)
Riutilizza OpenAIProvider con endpoint custom
```

### `AzureOpenAIProvider`

```
HTTP: POST {endpoint}/openai/deployments/{model}/chat/completions?api-version={version}
Headers: api-key
Riutilizza logica OpenAI con costruzione URL Azure
```

### `GeminiProvider`

```
HTTP: POST https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent
Headers: x-goog-api-key
Response: candidates[0].content.parts[0].text
```

### `CustomProvider`

```
Qualsiasi endpoint OpenAI-compatibile
Riutilizza OpenAIProvider con endpoint + apiKey custom
```

## Cost tracker

```csharp
public class ProviderCostTracker
{
    private static readonly Dictionary<string, (double Input, double Output)> _costs = new()
    {
        ["OpenAI-gpt-4o"]        = (2.50, 10.00),
        ["OpenAI-gpt-4o-mini"]   = (0.15, 0.60),
        ["Anthropic-claude-sonnet-4"] = (3.00, 15.00),
        ["DeepSeek-deepseek-chat"]    = (0.27, 1.10),
        ["Ollama-*"]             = (0, 0),
    };

    public decimal Estimate(string provider, string model, int inTokens, int outTokens)
    {
        var key = _costs.Keys.FirstOrDefault(k => k == $"{provider}-{model}" || k == $"{provider}-*");
        if (key == null) return 0;
        var (inputCost, outputCost) = _costs[key];
        return (decimal)(inTokens / 1_000_000.0 * inputCost + outTokens / 1_000_000.0 * outputCost);
    }
}
```

## Test

| Test | Provider | Cosa verifica |
|------|----------|--------------|
| OpenAI response | OpenAI (mock) | Content match |
| Anthropic tool call | Anthropic (mock) | ToolName corretto |
| DeepSeek streaming | DeepSeek (mock) | Chunk flusso |
| Ollama embedding | Ollama (mock) | Dimensioni |
| Fallback chain | OpenAi fail → Anthropic | Secondo provider chiamato |
| Cost estimation | Tutti | Costo > 0 per provider a pagamento |
