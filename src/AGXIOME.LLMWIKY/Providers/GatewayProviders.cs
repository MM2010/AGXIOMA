using AGXIOME.Shared.Config;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.LLMWIKY.Providers;

// ── M-13 — OpenRouter ────────────────────────────────────────────────────────
/// <summary>OpenRouter unified gateway. Routes to 200+ models from OpenAI, Anthropic,
/// Google, Meta, and others. Supports auto-fallback between providers.
/// Requires HTTP-Referer and X-Title headers for policy compliance.</summary>
public sealed class OpenRouterProvider : ILLMProvider
{
    public string Name => "OpenRouter";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: true,
        EmbeddingDimensions: 0,
        MaxContextTokens: 200_000,
        SupportedModels: ["meta-llama/llama-3.1-70b-instruct", "anthropic/claude-3-5-sonnet", "openai/gpt-4o", "google/gemini-2.0-flash-001"]);
    public int EmbeddingDimensions => 0;

    private readonly OpenAIProvider _inner;
    public OpenRouterProvider(LLMWIKYConfig config, IHttpClientFactory? httpClientFactory = null)
        => _inner = new OpenAIProvider(config with { Endpoint = "https://openrouter.ai/api/v1" }, httpClientFactory);

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotSupportedException("OpenRouter does not expose an embeddings endpoint.");
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => throw new NotSupportedException("OpenRouter does not expose an embeddings endpoint.");
}

// ── M-14 — LiteLLM ───────────────────────────────────────────────────────────
/// <summary>LiteLLM proxy gateway. Supports 100+ providers via a unified OpenAI-compatible
/// interface. Configure <c>Endpoint</c> to the LiteLLM proxy URL and <c>ApiKey</c>
/// to the proxy API key. All capabilities depend on the proxied upstream model.</summary>
public sealed class LiteLLMProvider : ILLMProvider
{
    public string Name => "LiteLLM";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: true,
        EmbeddingDimensions: 1536,  // Proxied upstream — dimension depends on configured model
        MaxContextTokens: 200_000,
        SupportedModels: ["gpt-4o", "claude-3-5-sonnet", "gemini-2.0-flash", "llama-3.1-70b"]);
    public int EmbeddingDimensions => 1536;

    private readonly OpenAIProvider _inner;
    public LiteLLMProvider(LLMWIKYConfig config, IHttpClientFactory? httpClientFactory = null)
        => _inner = new OpenAIProvider(config, httpClientFactory);

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => _inner.EmbedAsync(text, ct);
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => _inner.EmbedBatchAsync(texts, ct);
}

// ── M-15 — AI/ML API ─────────────────────────────────────────────────────────
/// <summary>AI/ML API unified model platform. Aggregates 200+ models.
/// OpenAI-compatible endpoint at api.aimlapi.com/v1.</summary>
public sealed class AIMLApiProvider : ILLMProvider
{
    public string Name => "AIMLApi";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: false,
        EmbeddingDimensions: 0,
        MaxContextTokens: 128_000,
        SupportedModels: ["gpt-4o", "claude-3-5-sonnet", "meta-llama/Meta-Llama-3.1-70B-Instruct-Turbo"]);
    public int EmbeddingDimensions => 0;

    private readonly OpenAIProvider _inner;
    public AIMLApiProvider(LLMWIKYConfig config, IHttpClientFactory? httpClientFactory = null)
        => _inner = new OpenAIProvider(config with { Endpoint = "https://api.aimlapi.com/v1" }, httpClientFactory);

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotSupportedException("AIMLApi: use a model-specific embedding endpoint.");
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => throw new NotSupportedException("AIMLApi: use a model-specific embedding endpoint.");
}

// ── M-16 — Novita AI ─────────────────────────────────────────────────────────
/// <summary>Novita AI serverless inference. Hosts Llama, DeepSeek, and open models.
/// OpenAI-compatible API at api.novita.ai/v3/openai.</summary>
public sealed class NovitaAIProvider : ILLMProvider
{
    public string Name => "NovitaAI";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: false,
        EmbeddingDimensions: 0,
        MaxContextTokens: 131_072,
        SupportedModels: ["meta-llama/llama-3.1-70b-instruct", "deepseek/deepseek-v3", "mistralai/mistral-7b-instruct"]);
    public int EmbeddingDimensions => 0;

    private readonly OpenAIProvider _inner;
    public NovitaAIProvider(LLMWIKYConfig config, IHttpClientFactory? httpClientFactory = null)
        => _inner = new OpenAIProvider(config with { Endpoint = "https://api.novita.ai/v3/openai" }, httpClientFactory);

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotSupportedException("NovitaAI does not support embeddings via this endpoint.");
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => throw new NotSupportedException("NovitaAI does not support embeddings via this endpoint.");
}

// ── M-17 — DeepInfra ─────────────────────────────────────────────────────────
/// <summary>DeepInfra serverless inference. Hosts open-source models with competitive pricing.
/// OpenAI-compatible at api.deepinfra.com/v1/openai. Supports embeddings.</summary>
public sealed class DeepInfraProvider : ILLMProvider
{
    public string Name => "DeepInfra";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: false,
        EmbeddingDimensions: 1024,
        MaxContextTokens: 131_072,
        SupportedModels: ["meta-llama/Meta-Llama-3.1-70B-Instruct", "mistralai/Mixtral-8x7B-Instruct-v0.1", "deepseek-ai/DeepSeek-V3"]);
    public int EmbeddingDimensions => 1024;

    private readonly OpenAIProvider _inner;
    public DeepInfraProvider(LLMWIKYConfig config, IHttpClientFactory? httpClientFactory = null)
        => _inner = new OpenAIProvider(config with { Endpoint = "https://api.deepinfra.com/v1/openai" }, httpClientFactory);

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => _inner.EmbedAsync(text, ct);
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => _inner.EmbedBatchAsync(texts, ct);
}

// ── M-18 — Replicate ─────────────────────────────────────────────────────────
/// <summary>Replicate community model platform. 1000+ models including Llama, image, and audio.
/// Chat models are accessed via OpenAI-compatible chat completions endpoint.</summary>
public sealed class ReplicateProvider : ILLMProvider
{
    public string Name => "Replicate";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: false,
        SupportsVision: false,
        EmbeddingDimensions: 0,
        MaxContextTokens: 131_072,
        SupportedModels: ["meta/meta-llama-3.1-405b-instruct", "mistralai/mixtral-8x7b-instruct-v0.1"]);
    public int EmbeddingDimensions => 0;

    private readonly OpenAIProvider _inner;
    public ReplicateProvider(LLMWIKYConfig config, IHttpClientFactory? httpClientFactory = null)
        => _inner = new OpenAIProvider(config with { Endpoint = "https://api.replicate.com/v1" }, httpClientFactory);

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotSupportedException("Replicate: use a dedicated embedding model endpoint.");
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => throw new NotSupportedException("Replicate: use a dedicated embedding model endpoint.");
}

// ── M-19 — Reka AI ───────────────────────────────────────────────────────────
/// <summary>Reka AI provider. Core, Flash, and Edge multimodal models with vision support.
/// OpenAI-compatible API at api.reka.ai/v1.</summary>
public sealed class RekaAIProvider : ILLMProvider
{
    public string Name => "RekaAI";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: true,
        EmbeddingDimensions: 0,
        MaxContextTokens: 128_000,
        SupportedModels: ["reka-core", "reka-flash", "reka-edge"]);
    public int EmbeddingDimensions => 0;

    private readonly OpenAIProvider _inner;
    public RekaAIProvider(LLMWIKYConfig config, IHttpClientFactory? httpClientFactory = null)
        => _inner = new OpenAIProvider(config with { Endpoint = "https://api.reka.ai/v1" }, httpClientFactory);

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotSupportedException("RekaAI does not support embeddings.");
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => throw new NotSupportedException("RekaAI does not support embeddings.");
}

// ── M-20 — Modal ─────────────────────────────────────────────────────────────
/// <summary>Modal serverless GPU provider. Custom model deployments exposed via
/// OpenAI-compatible endpoints. Configure <c>Endpoint</c> to the Modal function URL.</summary>
public sealed class ModalProvider : ILLMProvider
{
    public string Name => "Modal";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: false,
        EmbeddingDimensions: 0,
        MaxContextTokens: 131_072,
        SupportedModels: ["custom-deployed-model"]);
    public int EmbeddingDimensions => 0;

    private readonly OpenAIProvider _inner;
    public ModalProvider(LLMWIKYConfig config, IHttpClientFactory? httpClientFactory = null)
        => _inner = new OpenAIProvider(config, httpClientFactory);

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotSupportedException("Modal: configure an embedding deployment endpoint explicitly.");
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => throw new NotSupportedException("Modal: configure an embedding deployment endpoint explicitly.");
}
