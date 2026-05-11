# 08 — LLMWIKY Engine

## Scopo

Motore di ragionamento primario di AGXIOME. Embedding, inference, function calling.
Integrato nativamente con PostgreSQL per RAG a livello database.

## Architettura LLMWIKY

```
Agent Request
    │
    ▼
┌─────────────────────┐
│  LLMWIKY Engine     │
│  ┌───────────────┐  │
│  │  Session      │  │  → Context window assembly
│  │  Manager      │  │  → History injection
│  └───────┬───────┘  │
│  ┌───────┴───────┐  │
│  │  Inference    │  │  → LLM call (OpenAI / Anthropic / local)
│  │  Provider     │  │  → Streaming support
│  └───────┬───────┘  │
│  ┌───────┴───────┐  │
│  │  Embedding    │  │  → text → vector
│  │  Provider     │  │  → batch embedding
│  └───────┬───────┘  │
│  ┌───────┴───────┐  │
│  │  Function     │  │  → JSON schema generation
│  │  Calling      │  │  → Tool result injection
│  └───────────────┘  │
└─────────────────────┘
    │
    ▼
┌─────────────────────┐
│  PostgreSQL         │
│  (pgvector, memory) │
└─────────────────────┘
```

## Contratti

```csharp
public interface ILLMWIKYEngine
{
    // Inference
    Task<LLMWIKYResponse> InferAsync(
        LLMWIKYRequest request, CancellationToken ct);
    IAsyncEnumerable<LLMWIKYChunk> InferStreamAsync(
        LLMWIKYRequest request, CancellationToken ct);

    // Embedding
    Task<float[]> EmbedAsync(string text, CancellationToken ct);
    Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct);

    // Function calling
    void RegisterTools(IReadOnlyList<JsonObject> toolSchemas);
}

public record LLMWIKYRequest(
    string SystemPrompt,
    string UserPrompt,
    IReadOnlyList<AgentMessage>? History = null,
    double? Temperature = 0.7,
    int? MaxTokens = 4096,
    bool EnableTools = true
);

public record LLMWIKYResponse(
    string Content,
    string? ToolCallId = null,
    string? ToolName = null,
    string? ToolArguments = null, // JSON string
    LLMWIKYUsage Usage = default
);

public record LLMWIKYChunk(
    string Content,
    bool IsToolCall = false,
    string? ToolCallId = null,
    bool IsFinal = false
);

public record LLMWIKYUsage(
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens
);
```

## Provider supportati

### Mappa completa provider

| Provider | Inference | Embedding | Streaming | Function Calling | Vision | Cost Model |
|----------|-----------|-----------|-----------|-----------------|--------|------------|
| **OpenAI** (GPT-4o, GPT-4, o1, o3, GPT-4.1) | ✅ | ✅ 1536d | ✅ | ✅ | ✅ | Pay-per-token |
| **Anthropic** (Claude 4, Claude 3.5 Sonnet, Opus) | ✅ | ✅ 1024d | ✅ | ✅ | ✅ | Pay-per-token |
| **DeepSeek** (DeepSeek-V3, DeepSeek-R1) | ✅ | ✅ 1024d | ✅ | ✅ | ❌ | Pay-per-token |
| **Ollama** (locale: llama3, deepseek-coder, mistral, phi4) | ✅ | ✅ config | ✅ | ✅ | ✅ (modello dip.) | 0 (hosting proprio) |
| **Local** (HuggingFace, llama.cpp, vLLM, TGI) | ✅ | ✅ config | ✅ | ✅ | dipende | 0 (hosting proprio) |
| **Custom** (endpoint OpenAI-compatibile) | ✅ | ✅ config | ✅ | ✅ | dipende | Variabile |
| **Azure OpenAI** | ✅ | ✅ | ✅ | ✅ | ✅ | Pay-per-token |
| **Google Gemini** | ✅ | ✅ | ✅ | ✅ | ✅ | Pay-per-token |

### Provider abstraction — contratto unico

```csharp
public interface ILLMProvider
{
    string Name { get; }                              // "OpenAI", "Anthropic", "DeepSeek", "Ollama"
    LLMProviderCapabilities Capabilities { get; }

    Task<LLMWIKYResponse> CompleteAsync(
        LLMCompletionRequest request, CancellationToken ct);

    IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(
        LLMCompletionRequest request, CancellationToken ct);

    Task<float[]> EmbedAsync(string text, CancellationToken ct);
    Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct);

    int EmbeddingDimensions { get; }
}

public record LLMProviderCapabilities(
    bool SupportsStreaming,
    bool SupportsFunctionCalling,
    bool SupportsVision,
    int EmbeddingDimensions,
    int MaxContextTokens,
    IReadOnlyList<string> SupportedModels
);
```

## Configurazione

```csharp
public class LLMWIKYConfig
{
    // Provider selection
    public string Provider { get; set; } = "OpenAI";
    public string Model { get; set; } = "gpt-4o";
    public string? ApiKey { get; set; }              // Da env/secret store
    public string? Endpoint { get; set; }             // Custom endpoint (Ollama, Local, Custom)
    public string? ApiVersion { get; set; }           // Per Azure
    public string? OrganizationId { get; set; }       // Per OpenAI organizzazioni

    // Inference params
    public double DefaultTemperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 4096;
    public int EmbeddingDimensions { get; set; } = 1536;
    public int MaxRetries { get; set; } = 3;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);

    // Provider-specific options (pass-through al provider nativo)
    public Dictionary<string, object> ProviderOptions { get; set; } = new();

    // Rate-limit adattivo
    public bool EnableAdaptiveRateLimit { get; set; } = true;
    public int MaxTokensPerMinute { get; set; } = 100_000;
    public int MaxRequestsPerMinute { get; set; } = 500;

    // Fallback chain (provider alternativi se primario fallisce)
    public List<LLMWIKYConfig>? FallbackProviders { get; set; }

    // Cost tracking
    public bool EnableCostTracking { get; set; } = true;
    public double? CostPerInputToken { get; set; }    // Override manuale
    public double? CostPerOutputToken { get; set; }   // Override manuale
}
```

## Provider Factory & Auto-discovery

```csharp
public interface ILLMProviderFactory
{
    ILLMProvider CreateProvider(LLMWIKYConfig config);
    ILLMProvider CreateProvider(string providerName, string model);
    IReadOnlyList<ProviderDescriptor> ListAvailableProviders();
    void RegisterProvider(string name, Func<LLMWIKYConfig, ILLMProvider> factory);
}

public record ProviderDescriptor(
    string Name,
    IReadOnlyList<string> SupportedModels,
    bool RequiresApiKey,
    bool SupportsLocalEndpoint,
    bool IsEmbeddingSupported
);

// Auto-discovery: scansiona assembly per implementazioni ILLMProvider registrate
// Provider built-in: OpenAI, Anthropic, DeepSeek, Ollama, AzureOpenAI, Gemini
// Provider custom: registrati via ILLMProviderFactory.RegisterProvider()
```

## Fallback chain

```csharp
public class FallbackLLMProvider : ILLMProvider
{
    private readonly IReadOnlyList<ILLMProvider> _chain;

    public async Task<LLMWIKYResponse> CompleteAsync(
        LLMCompletionRequest request, CancellationToken ct)
    {
        List<Exception> exceptions = new();

        foreach (var provider in _chain)
        {
            try
            {
                return await provider.CompleteAsync(request, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.Warning("Provider {Provider} failed: {Error}",
                    provider.Name, ex.Message);
                exceptions.Add(ex);
                continue; // Prossimo provider
            }
        }

        throw new AggregateException(
            "All LLM providers in fallback chain failed.", exceptions);
    }
}

// Config example:
// Primario: DeepSeek-V3 (costo basso)
// Fallback: GPT-4o (costo medio, più capace)
// Fallback2: Claude 4 (costo alto, massima qualità)
```

## Function calling flow

```
1. Tool registrato → JSON schema autogenerato (ToJsonSchema())
2. LLMWIKY request include tools array nel payload
3. LLM risponde con:
   - Text response (final)
   - Tool call (name + arguments)
4. AGXIOME:
   - Match function by name
   - Deserialize arguments (System.Text.Json)
   - Execute function
   - Append result al contesto
5. Nuova inference con tool result included
```

## Embedding per RAG

```csharp
public async Task<RagContext> BuildRagContextAsync(
    string query, string agentId, CancellationToken ct)
{
    // 1. Embed query
    var queryEmbedding = await engine.EmbedAsync(query, ct);

    // 2. Semantic search su PostgreSQL
    var chunks = await knowledgeBase.SearchAsync(query,
        new KnowledgeSearchOptions { TopK = 10, MinScore = 0.65 });

    // 3. Build context
    var context = new StringBuilder();
    foreach (var chunk in chunks)
        context.AppendLine($"--- Source: {chunk.DocumentId} ---\n{chunk.Content}\n");

    return new RagContext(query, context.ToString(), chunks);
}
```

## LLMWIKY Session cache

- Risultati embedding: hash-based, TTL 1h (Redis)
- Prompt template: cache con sliding expiration
- Tool schema: generato una volta, cached per agent type

## Esempio uso

```csharp
var engine = new LLMWIKYEngine(new LLMWIKYConfig
{
    Provider = "OpenAI",
    Model = "gpt-4o",
    ApiKey = Environment.GetEnvironmentVariable("LLMWIKY_API_KEY"),
    EnableAdaptiveRateLimit = true
});

engine.RegisterTools(new[] {
    typeof(StockPriceTool).ToJsonSchema(),
    typeof(DatabaseQueryTool).ToJsonSchema()
});

var response = await engine.InferAsync(new LLMWIKYRequest(
    SystemPrompt: "Sei un analista finanziario esperto.",
    UserPrompt: "Qual è il prezzo attuale di MSFT e AAPL?",
    History: previousMessages,
    Temperature: 0.3
));

// LLMWIKY decide se usare tool o rispondere direttamente
if (response.ToolName == "get_stock_price")
{
    var result = await new StockPriceTool().ExecuteAsync(ct);
    // Continua inference con result...
}
```

## Confronto Agno → AGXIOME

| Feature Agno | AGXIOME LLMWIKY | Delta |
|-------------|----------------|-------|
| `llm` model | `LLMWIKYConfig.Model` | Provider-agnostic |
| `embedder` | `EmbedAsync()` | Built-in, non separato |
| `tools` | `RegisterTools()` + autoschema | Reflection C#, non dict |
| Streaming | `InferStreamAsync()` | `IAsyncEnumerable` |
| Rate-limit | Adaptive, configurabile | Automatico |
