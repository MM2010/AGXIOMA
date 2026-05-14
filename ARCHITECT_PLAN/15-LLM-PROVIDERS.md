# 15 — LLM Providers

## Scopo

Provider abstraction unificata per tutti i modelli LLM supportati.
Anthropic, OpenAI, DeepSeek, Ollama, Local (HF/vLLM/TGI), Custom, Azure, Gemini.
Discovery, fallback chain, rate-limit adattivo, cost tracking.

## Provider hierarchy

```
ILLMProvider (interfaccia)
    │
    ├── OpenAIProvider      (GPT-4o, GPT-4, o1, o3, GPT-4.1)
    ├── AnthropicProvider   (Claude 4, Claude 3.5 Sonnet, Opus, Haiku)
    ├── DeepSeekProvider    (DeepSeek-V3, DeepSeek-R1, DeepSeek-Coder)
    ├── OllamaProvider      (llama3, deepseek-coder, mistral, phi4, qwen, gemma)
    ├── LocalProvider       (HuggingFace TGI, vLLM, llama.cpp server)
    ├── AzureOpenAIProvider (Azure-deployed OpenAI models)
    ├── GeminiProvider      (Gemini 1.5 Pro, Gemini 2.0 Flash)
    └── CustomProvider      (Any OpenAI-compatible endpoint)
```

## Provider configuration matrix

| Provider | `Name` | Default Model | Richiede ApiKey | Richiede Endpoint | Embedding Native |
|----------|--------|---------------|-----------------|-------------------|------------------|
| OpenAI | `OpenAI` | `gpt-4o` | ✅ | ❌ | ✅ (1536d) |
| Anthropic | `Anthropic` | `claude-sonnet-4-20250514` | ✅ | ❌ | ✅ (1024d) |
| DeepSeek | `DeepSeek` | `deepseek-chat` | ✅ | ❌ | ✅ (1024d) |
| Ollama | `Ollama` | `llama3` | ❌ | ✅ (http://localhost:11434) | ✅ (config) |
| Local (TGI) | `Local` | `mistral-7b` | ❌ | ✅ | ✅ (config) |
| Local (vLLM) | `Local` | `mixtral-8x22b` | ❌ | ✅ | ✅ (config) |
| Azure OpenAI | `AzureOpenAI` | `gpt-4o` | ✅ | ✅ (resource endpoint) | ✅ |
| Gemini | `Gemini` | `gemini-2.0-flash` | ✅ | ❌ | ✅ |
| Custom | `Custom` | — | opzionale | ✅ | opzionale |

## Provider implementations

### OpenAI Provider

```csharp
public class OpenAIProvider : ILLMProvider
{
    public string Name => "OpenAI";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: true,
        EmbeddingDimensions: 1536,
        MaxContextTokens: 128_000, // GPT-4o
        SupportedModels: new[] { "gpt-4o", "gpt-4o-mini", "gpt-4", "gpt-4-turbo",
                                 "o1", "o1-mini", "o3", "o3-mini", "gpt-4.1" }
    );

    private readonly OpenAIClient _client; // OpenAI SDK o HTTP diretto

    public OpenAIProvider(LLMWIKYConfig config)
    {
        _client = new OpenAIClient(config.ApiKey, new OpenAIClientOptions
        {
            Endpoint = config.Endpoint, // Custom endpoint
            Organization = config.OrganizationId
        });
    }

    public async Task<LLMWIKYResponse> CompleteAsync(
        LLMCompletionRequest request, CancellationToken ct)
    {
        var messages = BuildMessages(request);
        var tools = request.Tools?.Any() == true
            ? request.Tools.Select(t => t.ToToolDefinition()).ToList()
            : null;

        var chatOpts = new ChatCompletionOptions
        {
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            Tools = tools
        };

        var response = await _client.GetChatCompletionAsync(
            request.Model ?? "gpt-4o",
            messages, chatOpts, ct);

        return MapResponse(response);
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct)
    {
        var response = await _client.GetEmbeddingAsync(
            "text-embedding-3-small", // o config
            text, ct);
        return response.Data[0].Embedding;
    }
}
```

### Anthropic Provider

```csharp
public class AnthropicProvider : ILLMProvider
{
    public string Name => "Anthropic";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,  // Tool use API
        SupportsVision: true,
        EmbeddingDimensions: 1024,
        MaxContextTokens: 200_000,      // Claude 4
        SupportedModels: new[] {
            "claude-sonnet-4-20250514", "claude-3-5-sonnet-20241022",
            "claude-opus-4-20250514", "claude-3-opus-20240229",
            "claude-3-5-haiku-20241022", "claude-3-haiku-20240307" }
    );

    private readonly HttpClient _http;
    private readonly string _apiKey;

    public AnthropicProvider(LLMWIKYConfig config)
    {
        _apiKey = config.ApiKey!;
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("x-api-key", _apiKey);
        _http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    }

    // Implementazione chiamata Anthropic Messages API
    // Gestione tool_use content block per function calling
    // Streaming via Server-Sent Events
}
```

### DeepSeek Provider

```csharp
public class DeepSeekProvider : ILLMProvider
{
    public string Name => "DeepSeek";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: false,           // DeepSeek-V3 no vision
        EmbeddingDimensions: 1024,
        MaxContextTokens: 64_000,
        SupportedModels: new[] {
            "deepseek-chat",            // DeepSeek-V3
            "deepseek-reasoner",        // DeepSeek-R1
            "deepseek-coder" }
    );

    // API compatibile OpenAI → può riusare OpenAIProvider con endpoint custom!
    // deepseek.com API endpoint: https://api.deepseek.com/v1
}
```

### Ollama Provider

```csharp
public class OllamaProvider : ILLMProvider
{
    public string Name => "Ollama";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,   // Dall'Ollama 0.5+
        SupportsVision: true,            // Model-dependent
        EmbeddingDimensions: 768,        // Configurable
        MaxContextTokens: 8_192,         // Model-dependent
        SupportedModels: new[] {
            "llama3.1", "llama3.2", "deepseek-coder", "deepseek-r1",
            "mistral", "mixtral", "phi4", "qwen2.5", "gemma2",
            "codestral", "dolphin-mixtral" }
    );

    private readonly string _endpoint;  // http://localhost:11434

    public OllamaProvider(LLMWIKYConfig config)
    {
        _endpoint = config.Endpoint ?? "http://localhost:11434";
    }

    public async Task<LLMWIKYResponse> CompleteAsync(
        LLMCompletionRequest request, CancellationToken ct)
    {
        // Ollama API: POST /api/chat
        var response = await _http.PostAsJsonAsync(
            $"{_endpoint}/api/chat",
            new
            {
                model = request.Model,
                messages = request.Messages,
                stream = false,
                options = new
                {
                    temperature = request.Temperature,
                    num_predict = request.MaxTokens
                }
            }, ct);

        return MapResponse(response);
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct)
    {
        // Ollama API: POST /api/embeddings
        var response = await _http.PostAsJsonAsync(
            $"{_endpoint}/api/embeddings",
            new { model = request.Model ?? "nomic-embed-text", prompt = text }, ct);

        var data = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(ct);
        return data!.Embedding;
    }
}
```

### Local Provider (HuggingFace TGI / vLLM / llama.cpp)

```csharp
public class LocalProvider : ILLMProvider
{
    public string Name => "Local";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,      // TGI + vLLM supportano tool calling
        SupportsVision: false,              // Model-dependent
        EmbeddingDimensions: 768,           // Configurable
        MaxContextTokens: 8_192,            // Model-dependent
        SupportedModels: new[] {
            "llama-3.1-8b", "llama-3.1-70b", "mistral-7b", "mixtral-8x22b",
            "deepseek-coder-33b", "phi-4", "qwen-2.5-72b" }
    );

    private readonly string _endpoint;  // http://localhost:8080/v1 (OpenAI-compat!)
    private readonly OpenAIProvider _openaiCompat;

    public LocalProvider(LLMWIKYConfig config)
    {
        _endpoint = config.Endpoint ?? "http://localhost:8080/v1";
        // Riutilizza provider OpenAI con endpoint custom
        _openaiCompat = new OpenAIProvider(new LLMWIKYConfig
        {
            Endpoint = _endpoint,
            ApiKey = "not-needed", // Local server
            Model = config.Model
        });
    }

    // Delega a OpenAIProvider (API compatibile)
    public Task<LLMWIKYResponse> CompleteAsync(...) =>
        _openaiCompat.CompleteAsync(request, ct);
    public Task<float[]> EmbedAsync(...) =>
        _openaiCompat.EmbedAsync(text, ct);
}
```

### Custom Provider

```csharp
public class CustomProvider : ILLMProvider
{
    public string Name => "Custom";
    public LLMProviderCapabilities Capabilities { get; }
    private readonly OpenAIProvider _openaiCompat; // OpenAI-compat endpoint

    public CustomProvider(LLMWIKYConfig config)
    {
        Capabilities = new(
            SupportsStreaming: config.ProviderOptions.ContainsKey("streaming"),
            SupportsFunctionCalling: config.ProviderOptions.ContainsKey("function_calling"),
            SupportsVision: config.ProviderOptions.ContainsKey("vision"),
            EmbeddingDimensions: (int)(config.ProviderOptions.GetValueOrDefault("embedding_dims") ?? 768),
            MaxContextTokens: (int)(config.ProviderOptions.GetValueOrDefault("max_context") ?? 4096),
            SupportedModels: config.ProviderOptions
                .GetValueOrDefault("models") as string[] ?? new[] { "default" }
        );

        // Assume OpenAI-compatible API
        _openaiCompat = new OpenAIProvider(config);
    }
}
```

## Provider factory con auto-discovery

```csharp
public class LLMProviderFactory : ILLMProviderFactory
{
    private readonly Dictionary<string, Func<LLMWIKYConfig, ILLMProvider>> _registry = new()
    {
        ["OpenAI"]     = cfg => new OpenAIProvider(cfg),
        ["Anthropic"]  = cfg => new AnthropicProvider(cfg),
        ["DeepSeek"]   = cfg => new DeepSeekProvider(cfg),
        ["Ollama"]     = cfg => new OllamaProvider(cfg),
        ["Local"]      = cfg => new LocalProvider(cfg),
        ["AzureOpenAI"]= cfg => new AzureOpenAIProvider(cfg),
        ["Gemini"]     = cfg => new GeminiProvider(cfg),
        ["Custom"]     = cfg => new CustomProvider(cfg),
    };

    public void RegisterProvider(string name, Func<LLMWIKYConfig, ILLMProvider> factory)
        => _registry[name] = factory;

    public ILLMProvider CreateProvider(LLMWIKYConfig config)
    {
        if (!_registry.TryGetValue(config.Provider, out var factory))
            throw new ArgumentException($"Unknown provider: {config.Provider}");

        return factory(config);
    }

    public IReadOnlyList<ProviderDescriptor> ListAvailableProviders()
    {
        return _registry.Select(kv =>
        {
            var dummy = kv.Value(new LLMWIKYConfig());
            return new ProviderDescriptor(
                kv.Key,
                dummy.Capabilities.SupportedModels,
                dummy is not OllamaProvider and not LocalProvider,
                dummy is OllamaProvider or LocalProvider or CustomProvider,
                dummy is not AnthropicProvider // Anthropic embeddings via API separata
            );
        }).ToList();
    }
}
```

## Cost tracking per provider

```csharp
public class ProviderCostTracker
{
    private readonly Dictionary<string, ProviderCostConfig> _costs = new()
    {
        ["OpenAI-gpt-4o"] = new(2.50, 10.00),   // $/M input, $/M output
        ["OpenAI-gpt-4o-mini"] = new(0.15, 0.60),
        ["Anthropic-claude-sonnet-4"] = new(3.00, 15.00),
        ["Anthropic-claude-haiku-3.5"] = new(0.80, 4.00),
        ["DeepSeek-deepseek-chat"] = new(0.27, 1.10),
        ["DeepSeek-deepseek-reasoner"] = new(0.55, 2.19),
        ["Ollama-*"] = new(0, 0),                // Locale, gratuito
        ["Local-*"] = new(0, 0)                  // Locale, gratuito
    };

    public record ProviderCostConfig(
        double CostPerMillionInputTokens,
        double CostPerMillionOutputTokens
    );

    public decimal EstimateCost(string provider, string model,
        int inputTokens, int outputTokens)
    {
        var key = _costs.Keys.FirstOrDefault(k =>
            k == $"{provider}-{model}" || k == $"{provider}-*");
        if (key == null) return 0;

        var cfg = _costs[key];
        return (decimal)((inputTokens / 1_000_000.0 * cfg.CostPerMillionInputTokens)
                       + (outputTokens / 1_000_000.0 * cfg.CostPerMillionOutputTokens));
    }
}
```

## Provider fallback chain (config)

```json
{
  "LLMWIKY": {
    "Provider": "DeepSeek",
    "Model": "deepseek-chat",
    "ApiKey": "sk-...",
    "FallbackProviders": [
      {
        "Provider": "OpenAI",
        "Model": "gpt-4o-mini",
        "ApiKey": "sk-..."
      },
      {
        "Provider": "Anthropic",
        "Model": "claude-haiku-3-5",
        "ApiKey": "sk-ant-..."
      }
    ]
  }
}
```

## NFR provider

| Metrica | Target |
|---------|--------|
| Provider switch latency | < 50 ms |
| Fallback chain overhead | < 100 ms per fallback tentato |
| Cost estimation accuracy | ±5% |
| Provider discovery | < 10 ms |
| Provider factory throughput | > 10K creazioni/sec |
