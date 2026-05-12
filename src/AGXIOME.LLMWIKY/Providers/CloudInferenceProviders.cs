using AGXIOME.Shared.Config;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using System.Runtime.CompilerServices;

namespace AGXIOME.LLMWIKY.Providers;

// ── M-01 — Groq ──────────────────────────────────────────────────────────────
/// <summary>Groq LPU inference provider. OpenAI-compatible API at api.groq.com.
/// Specialises in high-throughput low-latency inference via custom LPU hardware.</summary>
public sealed class GroqProvider : ILLMProvider
{
    public string Name => "Groq";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: true,
        EmbeddingDimensions: 0,
        MaxContextTokens: 128_000,
        SupportedModels: ["llama-3.1-70b-versatile", "llama-3.1-8b-instant", "llama3-70b-8192", "mixtral-8x7b-32768", "gemma2-9b-it"]);
    public int EmbeddingDimensions => 0;

    private readonly OpenAIProvider _inner;
    public GroqProvider(LLMWIKYConfig config)
        => _inner = new OpenAIProvider(config with { Endpoint = "https://api.groq.com/openai/v1" });

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotSupportedException("Groq does not support embeddings.");
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => throw new NotSupportedException("Groq does not support embeddings.");
}

// ── M-02 — Mistral AI ─────────────────────────────────────────────────────────
/// <summary>Mistral AI provider. OpenAI-compatible API with native function calling.
/// Supports mistral-large, mistral-medium, open-mistral variants.</summary>
public sealed class MistralAIProvider : ILLMProvider
{
    public string Name => "MistralAI";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: false,
        EmbeddingDimensions: 1024,
        MaxContextTokens: 128_000,
        SupportedModels: ["mistral-large-latest", "mistral-medium-latest", "mistral-small-latest", "open-mistral-7b", "open-mixtral-8x7b"]);
    public int EmbeddingDimensions => 1024;

    private readonly OpenAIProvider _inner;
    public MistralAIProvider(LLMWIKYConfig config)
        => _inner = new OpenAIProvider(config with { Endpoint = "https://api.mistral.ai/v1" });

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => _inner.EmbedAsync(text, ct);
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => _inner.EmbedBatchAsync(texts, ct);
}

// ── M-03 — xAI Grok ──────────────────────────────────────────────────────────
/// <summary>xAI Grok provider. OpenAI-compatible endpoint. Supports Grok-2 with vision.</summary>
public sealed class XAIGrokProvider : ILLMProvider
{
    public string Name => "xAIGrok";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: true,
        EmbeddingDimensions: 0,
        MaxContextTokens: 131_072,
        SupportedModels: ["grok-2-latest", "grok-2-vision-1212", "grok-beta", "grok-2-1212"]);
    public int EmbeddingDimensions => 0;

    private readonly OpenAIProvider _inner;
    public XAIGrokProvider(LLMWIKYConfig config)
        => _inner = new OpenAIProvider(config with { Endpoint = "https://api.x.ai/v1" });

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotSupportedException("xAI Grok does not support embeddings.");
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => throw new NotSupportedException("xAI Grok does not support embeddings.");
}

// ── M-04 — Cohere ─────────────────────────────────────────────────────────────
/// <summary>Cohere Command provider via OpenAI-compatible compatibility endpoint.
/// Command R+ specialises in RAG and tool use scenarios.</summary>
public sealed class CohereProvider : ILLMProvider
{
    public string Name => "Cohere";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: false,
        EmbeddingDimensions: 1024,
        MaxContextTokens: 128_000,
        SupportedModels: ["command-r-plus", "command-r", "command-a-03-2025", "command-r7b-12-2024"]);
    public int EmbeddingDimensions => 1024;

    private readonly OpenAIProvider _inner;
    public CohereProvider(LLMWIKYConfig config)
        => _inner = new OpenAIProvider(config with { Endpoint = "https://api.cohere.com/compatibility/v1" });

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => _inner.EmbedAsync(text, ct);
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => _inner.EmbedBatchAsync(texts, ct);
}

// ── M-05 — Perplexity ────────────────────────────────────────────────────────
/// <summary>Perplexity Sonar provider. Search-augmented inference; no tool calling in base API.
/// Each call may perform live web retrieval before generating.</summary>
public sealed class PerplexityProvider : ILLMProvider
{
    public string Name => "Perplexity";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: false,
        SupportsVision: false,
        EmbeddingDimensions: 0,
        MaxContextTokens: 127_072,
        SupportedModels: ["llama-3.1-sonar-large-128k-online", "llama-3.1-sonar-small-128k-online", "llama-3.1-sonar-huge-128k-online"]);
    public int EmbeddingDimensions => 0;

    private readonly OpenAIProvider _inner;
    public PerplexityProvider(LLMWIKYConfig config)
        => _inner = new OpenAIProvider(config with { Endpoint = "https://api.perplexity.ai" });

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotSupportedException("Perplexity does not support embeddings.");
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => throw new NotSupportedException("Perplexity does not support embeddings.");
}

// ── M-06 — Together AI ───────────────────────────────────────────────────────
/// <summary>Together AI provider. 200+ open models via OpenAI-compatible API.
/// Supports batch inference and fine-tuned model hosting.</summary>
public sealed class TogetherAIProvider : ILLMProvider
{
    public string Name => "TogetherAI";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: false,
        EmbeddingDimensions: 0,
        MaxContextTokens: 128_000,
        SupportedModels: ["meta-llama/Meta-Llama-3.1-70B-Instruct-Turbo", "mistralai/Mixtral-8x7B-Instruct-v0.1", "Qwen/Qwen2.5-72B-Instruct-Turbo"]);
    public int EmbeddingDimensions => 0;

    private readonly OpenAIProvider _inner;
    public TogetherAIProvider(LLMWIKYConfig config)
        => _inner = new OpenAIProvider(config with { Endpoint = "https://api.together.xyz/v1" });

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotSupportedException("TogetherAI chat endpoint does not support embeddings.");
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => throw new NotSupportedException("TogetherAI chat endpoint does not support embeddings.");
}

// ── M-07 — Fireworks AI ──────────────────────────────────────────────────────
/// <summary>Fireworks AI provider. Fast inference on open models via OpenAI-compatible API.
/// Endpoint: api.fireworks.ai/inference/v1</summary>
public sealed class FireworksAIProvider : ILLMProvider
{
    public string Name => "FireworksAI";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: false,
        EmbeddingDimensions: 0,
        MaxContextTokens: 131_072,
        SupportedModels: ["accounts/fireworks/models/llama-v3p1-70b-instruct", "accounts/fireworks/models/mixtral-8x7b-instruct", "accounts/fireworks/models/deepseek-v3"]);
    public int EmbeddingDimensions => 0;

    private readonly OpenAIProvider _inner;
    public FireworksAIProvider(LLMWIKYConfig config)
        => _inner = new OpenAIProvider(config with { Endpoint = "https://api.fireworks.ai/inference/v1" });

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotSupportedException("FireworksAI does not support embeddings via this endpoint.");
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => throw new NotSupportedException("FireworksAI does not support embeddings via this endpoint.");
}

// ── M-08 — Cerebras ──────────────────────────────────────────────────────────
/// <summary>Cerebras wafer-scale inference provider. Ultra-fast Llama inference.
/// OpenAI-compatible API at api.cerebras.ai/v1</summary>
public sealed class CerebrasProvider : ILLMProvider
{
    public string Name => "Cerebras";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: false,
        EmbeddingDimensions: 0,
        MaxContextTokens: 128_000,
        SupportedModels: ["llama3.1-70b", "llama3.1-8b", "llama-3.3-70b"]);
    public int EmbeddingDimensions => 0;

    private readonly OpenAIProvider _inner;
    public CerebrasProvider(LLMWIKYConfig config)
        => _inner = new OpenAIProvider(config with { Endpoint = "https://api.cerebras.ai/v1" });

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotSupportedException("Cerebras does not support embeddings.");
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => throw new NotSupportedException("Cerebras does not support embeddings.");
}
