# P01 — Model Providers (50+)

> **Priority**: 🔴 P0 | **Dipende da**: P09 (Core) | **Usato da**: P08 (Runtime)
> **Target**: 50+ provider (Agno ha ~35)

---

## Provider Esistenti (8) ✅

| # | Provider | Status | File |
|---|----------|--------|------|
| 1 | OpenAI | ✅ Done | `OpenAIProvider.cs` |
| 2 | Anthropic Claude | ✅ Done | `AnthropicProvider.cs` |
| 3 | Google Gemini | ✅ Done | `GeminiProvider.cs` |
| 4 | DeepSeek | ✅ Done | (via OpenAI compat) |
| 5 | Ollama | ✅ Done | `OllamaProvider.cs` |
| 6 | Azure OpenAI | ✅ Done | `AzureOpenAIProvider.cs` |
| 7 | Local (vLLM/TGI) | ✅ Done | `LocalProvider.cs` |
| 8 | Custom | ✅ Done | via factory |

---

## Provider da Implementare (42+)

### 🔴 Wave 1 — Cloud Enterprise (12)

| # | Provider | API Base | Note |
|---|----------|----------|------|
| M-01 | **Groq** | `https://api.groq.com/openai/v1` | OpenAI-compat, inferenza ultra-veloce |
| M-02 | **Mistral AI** | `https://api.mistral.ai/v1` | Mistral Large/Small/Tiny |
| M-03 | **xAI Grok** | `https://api.x.ai/v1` | Grok-2, Grok-3 |
| M-04 | **Cohere** | `https://api.cohere.ai/v1` | Command R+, embed, rerank |
| M-05 | **Perplexity** | `https://api.perplexity.ai` | Sonar, search-backed |
| M-06 | **Together AI** | `https://api.together.xyz/v1` | OpenAI-compat, 200+ modelli |
| M-07 | **Fireworks AI** | `https://api.fireworks.ai/inference/v1` | OpenAI-compat ottimizzato |
| M-08 | **Cerebras** | `https://api.cerebras.ai/v1` | Cerebras-GPT, inferenza wafer-scale |
| M-09 | **AWS Bedrock** | AWS SDK | Anthropic, Meta, Mistral, Cohere, AI21 via AWS |
| M-10 | **Azure AI Foundry** | `https://{endpoint}.ai.azure.com` | Modelli Microsoft + third-party |
| M-11 | **IBM WatsonX** | `https://{region}.ml.cloud.ibm.com` | Granite, Llama, Mixtral |
| M-12 | **SambaNova** | `https://api.sambanova.ai/v1` | Llama, Mixtral su hardware SN40L |

### 🟡 Wave 2 — Gateway & Aggregator (8)

| # | Provider | API Base | Note |
|---|----------|----------|------|
| M-13 | **OpenRouter** | `https://openrouter.ai/api/v1` | 200+ modelli, fallback automatico |
| M-14 | **LiteLLM** | proxy configurabile | Gateway universale per 100+ provider |
| M-15 | **AI/ML API** | `https://api.aimlapi.com` | Accesso unificato 200+ modelli |
| M-16 | **Novita AI** | `https://api.novita.ai/v3` | Llama, Mistral, DeepSeek serverless |
| M-17 | **DeepInfra** | `https://api.deepinfra.com/v1` | OpenAI-compat, modelli open-source |
| M-18 | **Replicate** | `https://api.replicate.com/v1` | 1000+ modelli community |
| M-19 | **Reka AI** | `https://api.reka.ai/v1` | REKA Core, Edge, Flash |
| M-20 | **Modal** | API Token | Serverless GPU per modelli custom |

### 🟡 Wave 3 — Local & On-Premise (7)

| # | Provider | API Base | Note |
|---|----------|----------|------|
| M-21 | **llama.cpp** | `http://localhost:8080/v1` | Server nativo C++ GGUF |
| M-22 | **LM Studio** | `http://localhost:1234/v1` | UI locale + OpenAI-compat |
| M-23 | **Ollama Responses** | `http://localhost:11434` | `POST /api/responses` (non chat) |
| M-24 | **LocalAI** | `http://localhost:8080` | OpenAI-compat auto-hosted |
| M-25 | **GPT4All** | `http://localhost:4891/v1` | Desktop LLM |
| M-26 | **text-gen-webui** | `http://localhost:5000/api` | Oobabooga |
| M-27 | **KoboldCPP** | `http://localhost:5001/api` | GGUF + story mode |

### 🟡 Wave 4 — Niche & Specialized (9)

| # | Provider | API Base | Note |
|---|----------|----------|------|
| M-28 | **HuggingFace Inference** | `https://api-inference.huggingface.co` | 200k+ modelli HF |
| M-29 | **HuggingFace TGI** | `http://localhost:8080` | Text Generation Inference self-hosted |
| M-30 | **AI21 Labs** | `https://api.ai21.com/studio/v1` | Jurassic-2, Jamba |
| M-31 | **Writer** | `https://api.writer.com/v1` | Palmyra, enterprise LLM |
| M-32 | **NVIDIA NIM** | `https://api.nvcf.nvidia.com` | Llama, Nemotron su GPU NVIDIA |
| M-33 | **Anyscale** | `https://api.endpoints.anyscale.com/v1` | Ray-backed endpoint |
| M-34 | **OctoAI** | `https://text.octoai.run/v1` | Inference ottimizzato |
| M-35 | **Lepton AI** | `https://{workspace}.lepton.ai/api` | Modelli open-source serverless |
| M-36 | **Friendli AI** | `https://api.friendli.ai/v1` | Inference serverless |

### 🟡 Wave 5 — Cina & Asia (8)

| # | Provider | API Base | Note |
|---|----------|----------|------|
| M-37 | **Moonshot (Kimi)** | `https://api.moonshot.cn/v1` | Kimi k1.5 |
| M-38 | **Zhipu AI (GLM)** | `https://open.bigmodel.cn/api/paas/v4` | GLM-4 |
| M-39 | **Baichuan** | `https://api.baichuan-ai.com/v1` | Baichuan 4 |
| M-40 | **ByteDance (Doubao)** | `https://ark.cn-beijing.volces.com/api` | Doubao-Pro |
| M-41 | **Alibaba (Qwen)** | `https://dashscope.aliyuncs.com/api/v1` | Qwen2.5, QwQ-32B |
| M-42 | **Baidu (ERNIE)** | `https://aip.baidubce.com/rpc/2.0/ai_custom` | ERNIE 4.0 |
| M-43 | **Tencent (Hunyuan)** | `https://api.hunyuan.cloud.tencent.com` | Hunyuan Turbo |
| M-44 | **01.AI (Yi)** | `https://api.lingyiwanwu.com/v1` | Yi-Lightning, Yi-Large |

### 🟢 Wave 6 — Emerging (6)

| # | Provider | API Base | Note |
|---|----------|----------|------|
| M-45 | **AI21 Jamba** | `https://api.ai21.com/studio/v1` | Jamba 1.5 (SSM-Transformer) |
| M-46 | **Snowflake Arctic** | `https://api.snowflake.ai/v1` | Enterprise SQL + LLM |
| M-47 | **Phind** | `https://api.phind.com/v1` | Search-augmented LLM |
| M-48 | **You.com** | `https://api.you.com/v1` | Web search + chat |
| M-49 | **Hyperbolic** | `https://api.hyperbolic.xyz/v1` | Inference decentralizzata |
| M-50 | **Featherless** | `https://api.featherless.ai/v1` | Serverless open models |

---

## Pattern di Implementazione

Ogni provider segue questo template:

```csharp
// Providers/XXXProvider.cs
public sealed class XXXProvider : ILLMProvider
{
    private readonly HttpClient _http;
    private readonly ProviderCostTracker _costs;
    private readonly ResilientExecutor _resilient;

    // Costruttore: prende URI da config o environment
    // InferAsync: POST /v1/chat/completions → ChatResult
    // StreamAsync: POST /v1/chat/completions?stream=true → IAsyncEnumerable<ChatDelta>
    // EmbedAsync: POST /v1/embeddings → EmbeddingResult
}
```

---

## Provider Cost Tracker — Estensione

Aggiungere costi per ogni nuovo provider in `ProviderCostTracker.cs`:

```csharp
public static class CostDatabase
{
    public static readonly Dictionary<string, ProviderCost> Costs = new()
    {
        ["gropp:llama-3.3-70b"] = new(0.59m, 0.79m),    // $/M input/output
        ["mistral:mistral-large"] = new(2.0m, 6.0m),
        ["cohere:command-r+"] = new(2.5m, 10.0m),
        // ... tutti i 50 provider
    };
}
```

---

## Provider Adapter Pattern

```csharp
public interface ILLMProvider
{
    string ProviderName { get; }
    Task<ChatResult> InferAsync(ChatRequest request, CancellationToken ct);
    IAsyncEnumerable<ChatDelta> StreamAsync(ChatRequest request, CancellationToken ct);
    Task<EmbeddingResult> EmbedAsync(EmbeddingRequest request, CancellationToken ct);
    bool SupportsStreaming { get; }
    bool SupportsEmbedding { get; }
}
```

---

## Test per Provider

```csharp
// Ogni provider ha:
[Fact] public void ProviderName_ReturnsCorrectName()
[Fact] public async Task InferAsync_ReturnsChatResult()
[Fact] public async Task StreamAsync_ReturnsDeltas()
[Fact] public async Task HttpError_ReturnsErrorCode()
[Fact] public async Task CostTracking_UpdatesCorrectly()
```

**Totale provider: 50+ | File: ~60 | Test: ~250**
