# LLM Providers — AGXIOME

**50+ LLM provider** con interfaccia unificata, fallback chain automatico, cost tracking, streaming SSE.

---

## Provider disponibili

### Cloud — Big Labs

| Provider | Token | Streaming | Function Calling | Embedding |
|----------|-------|-----------|-----------------|-----------|
| OpenAI | `"OpenAI"` | ✅ SSE | ✅ | ✅ 1536d |
| Anthropic | `"Anthropic"` | ✅ SSE | ✅ tool_use | ❌ |
| Google Gemini | `"Gemini"` | ✅ SSE | ✅ | ✅ 768d |
| Azure OpenAI | `"AzureOpenAI"` | ✅ SSE | ✅ | ✅ |
| AWS Bedrock | `"Bedrock"` | ✅ | ✅ | ✅ |
| Azure AI Foundry | `"AzureFoundry"` | ✅ | ✅ | ✅ |

### Cloud — Independent / Specialized

| Provider | Token | Streaming | Function Calling |
|----------|-------|-----------|-----------------|
| DeepSeek | `"DeepSeek"` | ✅ | ✅ |
| Mistral AI | `"Mistral"` | ✅ | ✅ |
| xAI Grok | `"xAI"` / `"xaigrok"` | ✅ | ✅ |
| Cohere | `"Cohere"` | ✅ | ✅ |
| Perplexity | `"Perplexity"` | ✅ | ❌ |
| Groq | `"Groq"` | ✅ | ✅ |
| Together AI | `"Together"` | ✅ | ✅ |
| Cerebras | `"Cerebras"` | ✅ | ✅ |
| Fireworks AI | `"FireworksAI"` | ✅ | ✅ |
| Sambanova | `"Sambanova"` | ✅ | ✅ |
| OpenRouter | `"OpenRouter"` | ✅ | ✅ |
| LiteLLM | `"LiteLLM"` | ✅ | ✅ |
| HuggingFace | `"HuggingFace"` | ✅ | ❌ |

### Asian Providers

| Provider | Token | Streaming |
|----------|-------|-----------|
| Moonshot / Kimi | `"Moonshot"` | ✅ |
| Zhipu AI | `"Zhipu"` | ✅ |
| Baichuan | `"Baichuan"` | ✅ |
| ByteDance | `"ByteDance"` | ✅ |
| Alibaba Qwen | `"Qwen"` | ✅ |
| Baidu Ernie | `"Ernie"` | ✅ |
| Tencent Hunyuan | `"Hunyuan"` | ✅ |
| 01.AI | `"Yi"` | ✅ |

### Local / Self-hosted

| Provider | Prerequisito |
|----------|-------------|
| Ollama | `ollama serve` in esecuzione |
| llama.cpp | server HTTP su porta 8080 |
| LM Studio | server HTTP su porta 1234 |
| LocalAI | container/servizio LocalAI |
| GPT4All | server HTTP GPT4All |
| text-gen-webui | `--api` flag |
| KoboldCPP | server KoboldCPP |
| vLLM / TGI | endpoint OpenAI-compatible |

### OpenAI-Compatible (qualsiasi)

Qualsiasi endpoint che implementa l'API OpenAI (`/v1/chat/completions`):

```csharp
new LLMWIKYConfig
{
    Provider = "Custom",
    Model    = "my-model",
    Endpoint = "https://my-endpoint.com/v1",
    ApiKey   = "..."
}
```

---

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
    Model    = "deepseek-v4-flash",    // deepseek-chat | deepseek-reasoner
    ApiKey   = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY")
}
```

### Mistral AI

```csharp
new LLMWIKYConfig
{
    Provider = "Mistral",
    Model    = "mistral-large-latest",    // mistral-small-latest | mixtral-8x7b
    ApiKey   = Environment.GetEnvironmentVariable("MISTRAL_API_KEY")
}
```

### xAI Grok

```csharp
new LLMWIKYConfig
{
    Provider = "xAI",
    Model    = "grok-3",
    ApiKey   = Environment.GetEnvironmentVariable("XAI_API_KEY")
}
```

### Groq

```csharp
new LLMWIKYConfig
{
    Provider = "Groq",
    Model    = "llama-3.3-70b-versatile",
    ApiKey   = Environment.GetEnvironmentVariable("GROQ_API_KEY")
}
```

### Fireworks AI

```csharp
new LLMWIKYConfig
{
    Provider = "FireworksAI",
    Model    = "accounts/fireworks/models/llama-v3p1-70b-instruct",
    ApiKey   = Environment.GetEnvironmentVariable("FIREWORKS_API_KEY")
}
```

### Ollama (locale)

```csharp
new LLMWIKYConfig
{
    Provider  = "Ollama",
    Model     = "llama3.2",     // deepseek-coder | mistral | phi4 | qwen2.5
    Endpoint  = "http://localhost:11434"
}
```

### Azure OpenAI

```csharp
new LLMWIKYConfig
{
    Provider   = "AzureOpenAI",
    Model      = "gpt-4o",
    Endpoint   = "https://my-resource.openai.azure.com",
    ApiKey     = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY"),
    ApiVersion = "2024-08-01-preview"
}
```

### Google Gemini

```csharp
new LLMWIKYConfig
{
    Provider = "Gemini",
    Model    = "gemini-2.0-flash",
    ApiKey   = Environment.GetEnvironmentVariable("GOOGLE_API_KEY")
}
```

---

## Fallback chain

Se il provider primario fallisce (timeout, 429, 500, 503), AGXIOME prova automaticamente i fallback:

```csharp
new LLMWIKYConfig
{
    Provider = "DeepSeek",
    Model    = "deepseek-chat",
    ApiKey   = "...",
    FallbackProviders = new()
    {
        new() { Provider = "OpenAI",    Model = "gpt-4o-mini",      ApiKey = "..." },
        new() { Provider = "Anthropic", Model = "claude-haiku-3-5", ApiKey = "..." }
    }
}
```

---

## Cost tracking

```csharp
var tracker = new ProviderCostTracker();
var cost = tracker.Estimate("OpenAI", "gpt-4o", inputTokens: 1000, outputTokens: 500);
// cost ≈ $0.0075
```

### Costi di riferimento

| Provider | Modello | Input ($/M token) | Output ($/M token) |
|----------|--------|-------------------|--------------------|
| OpenAI | gpt-4o | $2.50 | $10.00 |
| OpenAI | gpt-4o-mini | $0.15 | $0.60 |
| Anthropic | claude-sonnet-4 | $3.00 | $15.00 |
| Anthropic | claude-haiku-3-5 | $0.80 | $4.00 |
| DeepSeek | deepseek-chat | $0.27 | $1.10 |
| Mistral | mistral-large-latest | $2.00 | $6.00 |
| xAI | grok-3 | $3.00 | $15.00 |
| Groq | llama-3.3-70b | $0.59 | $0.79 |
| Fireworks AI | llama-v3p1-70b | $0.90 | $0.90 |
| Ollama | * | $0 | $0 |
| Local / Self-hosted | * | $0 | $0 |

---

## Resilienza provider

Ogni provider LLM ha integrazione automatica con `ResilientExecutor`:

- **429** → retry con backoff esponenziale → loggato come `LLM_RateLimited`
- **500/502/503** → retry → circuito aperto dopo 5 fallimenti consecutivi
- **Timeout** → retry → escalation al fallback provider
- Tutti gli eventi sono loggati con tentativi, delay e durata totale

---

## Model Compatibility Map

`ModelCompatibilityMap` fornisce lookup statico `model-string → (provider, status)`:

```csharp
var (provider, status) = ModelCompatibilityMap.Resolve("gpt-4o");
// provider = LLMProviderType.OpenAI, status = Supported

var (provider2, status2) = ModelCompatibilityMap.Resolve("mixtral-8x7b-instruct");
// provider2 = LLMProviderType.Mistral, status2 = Deprecated
```

Stati possibili: `Supported`, `Preview`, `Deprecated`, `Unknown`.
