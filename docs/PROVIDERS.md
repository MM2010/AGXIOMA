# LLM Providers — AGXIOME

8 provider LLM supportati con interfaccia unificata, fallback chain, cost tracking.

---

## Provider disponibili

| Provider | Name | Streaming | Function Calling | Embedding |
|----------|------|-----------|-----------------|-----------|
| OpenAI | `"OpenAI"` | ✅ SSE | ✅ | ✅ 1536d |
| Anthropic | `"Anthropic"` | ✅ SSE | ✅ (tool_use) | ❌ |
| DeepSeek | `"DeepSeek"` | ✅ | ✅ | ✅ 1024d |
| Ollama | `"Ollama"` | ✅ | ✅ | ✅ config |
| Local (vLLM/TGI/llama.cpp) | `"Local"` | ✅ | ✅ | ✅ config |
| Azure OpenAI | `"AzureOpenAI"` | ✅ | ✅ | ✅ |
| Gemini | `"Gemini"` | ✅ | ✅ | ✅ 768d |
| Custom | `"Custom"` | ✅ | ✅ | ✅ |

## Configurazione

### OpenAI

```csharp
new LLMWIKYConfig
{
    Provider = "OpenAI",
    Model = "gpt-4o-mini",          // o gpt-4o, o1, o3, gpt-4.1
    ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
}
```

### Anthropic Claude

```csharp
new LLMWIKYConfig
{
    Provider = "Anthropic",
    Model = "claude-sonnet-4-20250514",
    ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
}
```

### DeepSeek

```csharp
new LLMWIKYConfig
{
    Provider = "DeepSeek",
    Model = "deepseek-v4-flash",    // o deepseek-chat, deepseek-reasoner
    ApiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY")
}
```

### Ollama (locale)

```csharp
new LLMWIKYConfig
{
    Provider = "Ollama",
    Model = "llama3.2",             // o deepseek-coder, mistral, phi4, qwen2.5
    Endpoint = "http://localhost:11434"
}
```

### Local (vLLM / TGI / llama.cpp)

```csharp
new LLMWIKYConfig
{
    Provider = "Local",
    Model = "llama-3.1-8b",
    Endpoint = "http://localhost:8080/v1"  // OpenAI-compatible
}
```

### Azure OpenAI

```csharp
new LLMWIKYConfig
{
    Provider = "AzureOpenAI",
    Model = "gpt-4o",
    Endpoint = "https://my-resource.openai.azure.com",
    ApiKey = "...",
    ApiVersion = "2024-08-01-preview"
}
```

### Gemini

```csharp
new LLMWIKYConfig
{
    Provider = "Gemini",
    Model = "gemini-2.0-flash",
    ApiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY")
}
```

### Custom (qualsiasi endpoint OpenAI-compatibile)

```csharp
new LLMWIKYConfig
{
    Provider = "Custom",
    Model = "my-model",
    Endpoint = "https://my-custom-endpoint.com/v1",
    ApiKey = "..."
}
```

## Fallback chain

Se il provider primario fallisce (timeout, 429, 500, 503), AGXIOME prova automaticamente i fallback:

```csharp
new LLMWIKYConfig
{
    Provider = "DeepSeek",
    Model = "deepseek-chat",
    ApiKey = "...",
    FallbackProviders = new()
    {
        new() { Provider = "OpenAI", Model = "gpt-4o-mini", ApiKey = "..." },
        new() { Provider = "Anthropic", Model = "claude-haiku-3-5", ApiKey = "..." }
    }
}
```

## Cost tracking

```csharp
var tracker = new ProviderCostTracker();
var cost = tracker.Estimate("OpenAI", "gpt-4o", inputTokens: 1000, outputTokens: 500);
// cost ≈ $0.0075
```

### Costi per provider

| Provider | Modello | Input ($/M token) | Output ($/M token) |
|----------|--------|-------------------|--------------------|
| OpenAI | gpt-4o | $2.50 | $10.00 |
| OpenAI | gpt-4o-mini | $0.15 | $0.60 |
| Anthropic | claude-sonnet-4 | $3.00 | $15.00 |
| Anthropic | claude-haiku-3-5 | $0.80 | $4.00 |
| DeepSeek | deepseek-chat | $0.27 | $1.10 |
| Ollama | * | $0 | $0 |
| Local | * | $0 | $0 |

## Resilienza provider

Ogni provider LLM ha integrazione automatica con `ResilientExecutor`:

- **429** → retry con backoff → loggato come `LLM_RateLimited`
- **500/502/503** → retry → circuito aperto dopo 5 fallimenti
- **Timeout** → retry → fallback provider
- **Tutto loggato** con tentativi, delay, durata
