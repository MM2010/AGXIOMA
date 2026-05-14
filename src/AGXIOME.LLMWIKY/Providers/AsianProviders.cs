using AGXIOME.Shared.Config;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.LLMWIKY.Providers;

// ── M-37 — Moonshot (Kimi) ────────────────────────────────────────────────────
/// <summary>Moonshot AI Kimi provider. Long-context Chinese/English LLM.
/// OpenAI-compatible API at api.moonshot.cn/v1. Supports up to 128k context.</summary>
public sealed class MoonshotKimiProvider : ILLMProvider
{
    public string Name => "MoonshotKimi";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: false,
        EmbeddingDimensions: 0,
        MaxContextTokens: 128_000,
        SupportedModels: ["moonshot-v1-8k", "moonshot-v1-32k", "moonshot-v1-128k"]);
    public int EmbeddingDimensions => 0;

    private readonly OpenAIProvider _inner;
    public MoonshotKimiProvider(LLMWIKYConfig config, IHttpClientFactory? httpClientFactory = null)
        => _inner = new OpenAIProvider(config with { Endpoint = "https://api.moonshot.cn/v1" }, httpClientFactory);

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotSupportedException("MoonshotKimi does not support embeddings via the chat endpoint.");
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => throw new NotSupportedException("MoonshotKimi does not support embeddings via the chat endpoint.");
}

// ── M-38 — Zhipu AI (GLM) ─────────────────────────────────────────────────────
/// <summary>Zhipu AI GLM-4 provider. Advanced Chinese/English LLM with vision support.
/// OpenAI-compatible API at open.bigmodel.cn/api/paas/v4.</summary>
public sealed class ZhipuGLMProvider : ILLMProvider
{
    public string Name => "ZhipuGLM";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: true,
        EmbeddingDimensions: 0,
        MaxContextTokens: 128_000,
        SupportedModels: ["glm-4", "glm-4-flash", "glm-4v", "glm-3-turbo"]);
    public int EmbeddingDimensions => 0;

    private readonly OpenAIProvider _inner;
    public ZhipuGLMProvider(LLMWIKYConfig config, IHttpClientFactory? httpClientFactory = null)
        => _inner = new OpenAIProvider(config with { Endpoint = "https://open.bigmodel.cn/api/paas/v4" }, httpClientFactory);

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotSupportedException("ZhipuGLM: use the dedicated embedding model endpoint.");
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => throw new NotSupportedException("ZhipuGLM: use the dedicated embedding model endpoint.");
}

// ── M-39 — Baichuan ───────────────────────────────────────────────────────────
/// <summary>Baichuan AI provider. Baichuan 4 Chinese/English enterprise LLM.
/// OpenAI-compatible API at api.baichuan-ai.com/v1.</summary>
public sealed class BaichuanProvider : ILLMProvider
{
    public string Name => "Baichuan";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: false,
        EmbeddingDimensions: 0,
        MaxContextTokens: 32_768,
        SupportedModels: ["Baichuan4", "Baichuan3-Turbo", "Baichuan3-Turbo-128k"]);
    public int EmbeddingDimensions => 0;

    private readonly OpenAIProvider _inner;
    public BaichuanProvider(LLMWIKYConfig config, IHttpClientFactory? httpClientFactory = null)
        => _inner = new OpenAIProvider(config with { Endpoint = "https://api.baichuan-ai.com/v1" }, httpClientFactory);

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotSupportedException("Baichuan does not support embeddings via the chat endpoint.");
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => throw new NotSupportedException("Baichuan does not support embeddings via the chat endpoint.");
}

// ── M-40 — ByteDance (Doubao) ─────────────────────────────────────────────────
/// <summary>ByteDance Doubao provider (Volcano Engine ARK).
/// OpenAI-compatible API at ark.cn-beijing.volces.com/api/v3. Doubao-Pro models.</summary>
public sealed class ByteDanceDoubaoProvider : ILLMProvider
{
    public string Name => "ByteDanceDoubao";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: false,
        EmbeddingDimensions: 0,
        MaxContextTokens: 128_000,
        SupportedModels: ["doubao-pro-32k", "doubao-pro-128k", "doubao-lite-32k"]);
    public int EmbeddingDimensions => 0;

    private readonly OpenAIProvider _inner;
    public ByteDanceDoubaoProvider(LLMWIKYConfig config, IHttpClientFactory? httpClientFactory = null)
        => _inner = new OpenAIProvider(config with { Endpoint = "https://ark.cn-beijing.volces.com/api/v3" }, httpClientFactory);

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotSupportedException("ByteDanceDoubao does not support embeddings via this endpoint.");
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => throw new NotSupportedException("ByteDanceDoubao does not support embeddings via this endpoint.");
}

// ── M-41 — Alibaba (Qwen) ─────────────────────────────────────────────────────
/// <summary>Alibaba DashScope Qwen provider. Qwen2.5 and QwQ reasoning models.
/// OpenAI-compatible endpoint at dashscope.aliyuncs.com/compatible-mode/v1.</summary>
public sealed class AlibabaQwenProvider : ILLMProvider
{
    public string Name => "AlibabaQwen";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: true,
        EmbeddingDimensions: 0,
        MaxContextTokens: 131_072,
        SupportedModels: ["qwen2.5-72b-instruct", "qwen-max", "qwen-turbo", "qwq-32b-preview", "qwen-vl-plus"]);
    public int EmbeddingDimensions => 0;

    private readonly OpenAIProvider _inner;
    public AlibabaQwenProvider(LLMWIKYConfig config, IHttpClientFactory? httpClientFactory = null)
        => _inner = new OpenAIProvider(config with { Endpoint = "https://dashscope.aliyuncs.com/compatible-mode/v1" }, httpClientFactory);

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotSupportedException("AlibabaQwen: use the text-embedding-v3 endpoint for embeddings.");
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => throw new NotSupportedException("AlibabaQwen: use the text-embedding-v3 endpoint for embeddings.");
}

// ── M-42 — Baidu (ERNIE) ─────────────────────────────────────────────────────
/// <summary>Baidu ERNIE provider. ERNIE 4.0 via OpenAI-compatible access key mode.
/// Configure <c>Endpoint</c> to the Baidu access token URL and <c>ApiKey</c> to the token.
/// Endpoint example: https://aip.baidubce.com/rpc/2.0/ai_custom/v1/wenxinworkshop</summary>
public sealed class BaiduERNIEProvider : ILLMProvider
{
    public string Name => "BaiduERNIE";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: false,
        EmbeddingDimensions: 0,
        MaxContextTokens: 128_000,
        SupportedModels: ["ERNIE-4.0-8K", "ERNIE-4.0-Turbo-8K", "ERNIE-3.5-8K", "ERNIE-Speed-8K"]);
    public int EmbeddingDimensions => 0;

    private readonly OpenAIProvider _inner;
    public BaiduERNIEProvider(LLMWIKYConfig config, IHttpClientFactory? httpClientFactory = null)
        => _inner = new OpenAIProvider(config, httpClientFactory);

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotSupportedException("BaiduERNIE: use the dedicated ERNIE embedding API.");
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => throw new NotSupportedException("BaiduERNIE: use the dedicated ERNIE embedding API.");
}

// ── M-43 — Tencent (Hunyuan) ─────────────────────────────────────────────────
/// <summary>Tencent Hunyuan provider. Hunyuan-Turbo multilingual LLM.
/// OpenAI-compatible API at api.hunyuan.cloud.tencent.com/v1.</summary>
public sealed class TencentHunyuanProvider : ILLMProvider
{
    public string Name => "TencentHunyuan";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: false,
        EmbeddingDimensions: 0,
        MaxContextTokens: 256_000,
        SupportedModels: ["hunyuan-turbos-latest", "hunyuan-turbo", "hunyuan-standard", "hunyuan-lite"]);
    public int EmbeddingDimensions => 0;

    private readonly OpenAIProvider _inner;
    public TencentHunyuanProvider(LLMWIKYConfig config, IHttpClientFactory? httpClientFactory = null)
        => _inner = new OpenAIProvider(config with { Endpoint = "https://api.hunyuan.cloud.tencent.com/v1" }, httpClientFactory);

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotSupportedException("TencentHunyuan: use the dedicated Hunyuan embedding endpoint.");
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => throw new NotSupportedException("TencentHunyuan: use the dedicated Hunyuan embedding endpoint.");
}

// ── M-44 — 01.AI (Yi) ────────────────────────────────────────────────────────
/// <summary>01.AI Yi model provider. Yi-Lightning and Yi-Large series.
/// OpenAI-compatible API at api.lingyiwanwu.com/v1.</summary>
public sealed class YiAIProvider : ILLMProvider
{
    public string Name => "YiAI";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: true,
        EmbeddingDimensions: 0,
        MaxContextTokens: 200_000,
        SupportedModels: ["yi-lightning", "yi-large", "yi-medium", "yi-vision"]);
    public int EmbeddingDimensions => 0;

    private readonly OpenAIProvider _inner;
    public YiAIProvider(LLMWIKYConfig config, IHttpClientFactory? httpClientFactory = null)
        => _inner = new OpenAIProvider(config with { Endpoint = "https://api.lingyiwanwu.com/v1" }, httpClientFactory);

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotSupportedException("YiAI does not support embeddings via the chat endpoint.");
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => throw new NotSupportedException("YiAI does not support embeddings via the chat endpoint.");
}
