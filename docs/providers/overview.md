# Provider Overview

AGXIOME provides a **unified interface** across 50+ LLM backends. Every provider implements `ILLMProvider` with the same API — swap backends without changing application code.

## Architecture

```
Your App
   │
   ▼
IAgxiomeAgent
   │
   ▼
ILLMProvider  ←── Unified interface
   │
   ├── OpenAIProvider          (GPT-4o, GPT-4.1, o3, o1)
   ├── AnthropicProvider       (Claude 4, Sonnet, Haiku)
   ├── GeminiProvider          (Gemini 2.0 Flash, Pro)
   ├── DeepSeekProvider        (V3, R1)
   ├── GroqProvider            (Llama 3.3, Mixtral)
   ├── OllamaProvider          (Llama 3.2, Mistral, local)
   ├── ... 44 more providers
   └── OpenAICompatibleProvider (all OpenAI-compatible APIs)
```

## Provider Interface

```csharp
public interface ILLMProvider
{
    string Name { get; }                           // e.g. "openai:gpt-4o"
    LLMProviderCapabilities Capabilities { get; }  // streaming, embeddings, vision, function calling

    Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest request, CancellationToken ct);
    IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest request, CancellationToken ct);
    Task<float[]> EmbedAsync(string text, CancellationToken ct);
    Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct);
    int EmbeddingDimensions { get; }
}
```

## Completion Request

```csharp
var request = new LLMCompletionRequest
{
    Messages = new List<LLMMessage>
    {
        new("system", "You are a helpful assistant."),
        new("user", "What is the capital of France?")
    },
    Temperature = 0.7f,
    MaxTokens = 1024,
    Tools = new List<ToolDefinition> { /* function calling tools */ }
};
```

## Provider Capabilities

```csharp
public record LLMProviderCapabilities(
    bool SupportsStreaming,
    bool SupportsEmbeddings,
    bool SupportsFunctionCalling,
    bool SupportsVision,
    bool SupportsJsonMode,
    int MaxContextTokens
);
```

## Choosing a Provider

| Scenario | Recommended Provider |
|----------|---------------------|
| Best quality, cost no issue | OpenAI GPT-4o, Anthropic Claude 4 |
| Best quality, lower cost | DeepSeek V3, Groq Llama 3.3 |
| Local, offline, free | Ollama (Llama 3.2, Mistral) |
| Fastest inference | Groq (hardware-accelerated) |
| Compliance/air-gapped | Ollama, llama.cpp, LM Studio |
| Multilingual | DeepSeek, Alibaba Qwen, Mistral |
| Vision (image input) | OpenAI GPT-4o, Anthropic Claude, Gemini |

## Provider Factory

```csharp
// Dynamic creation from config string
var config = new LLMWIKYConfig
{
    Provider = "deepseek",
    Model = "deepseek-chat",
    ApiKey = "sk-..."
};

var factory = new LLMProviderFactory();
var provider = factory.CreateProvider(config);
```

## Fallback Chains

```csharp
var request = new LLMCompletionRequest
{
    Primary = new ProviderConfig { Provider = "deepseek" },
    Fallbacks = new[]
    {
        new ProviderConfig { Provider = "openai", Model = "gpt-4o-mini" },
        new ProviderConfig { Provider = "groq", Model = "llama-3.3-70b" }
    }
};
// If DeepSeek fails, tries OpenAI, then Groq
```

## Next

- [Cloud Providers](cloud.md) — OpenAI, Anthropic, Gemini, DeepSeek
- [Local Providers](local.md) — Ollama, llama.cpp, LM Studio
- [Fallback & Resilience](fallback.md)
