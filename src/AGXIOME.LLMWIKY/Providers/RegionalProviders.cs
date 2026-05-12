using AGXIOME.Shared.Config;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.LLMWIKY.Providers;

// ── M-45 — AI21 Jamba ─────────────────────────────────────────────────────────
/// <summary>AI21 Jamba SSM-Transformer provider. Jamba 1.5 is a hybrid attention/Mamba model
/// with 256k context. OpenAI-compatible API at api.ai21.com/studio/v1.</summary>
public sealed class AI21JambaProvider : ILLMProvider
{
    public string Name => "AI21Jamba";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: false,
        EmbeddingDimensions: 0,
        MaxContextTokens: 256_000,
        SupportedModels: ["jamba-1.5-large", "jamba-1.5-mini"]);
    public int EmbeddingDimensions => 0;

    private readonly OpenAIProvider _inner;
    public AI21JambaProvider(LLMWIKYConfig config)
        => _inner = new OpenAIProvider(config with { Endpoint = "https://api.ai21.com/studio/v1" });

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotSupportedException("AI21Jamba does not support embeddings via the chat endpoint.");
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => throw new NotSupportedException("AI21Jamba does not support embeddings via the chat endpoint.");
}

// ── M-46 — Snowflake Arctic ───────────────────────────────────────────────────
/// <summary>Snowflake Arctic enterprise SQL+LLM provider. Configure <c>Endpoint</c> to your
/// Snowflake Cortex endpoint. Arctic-Instruct is optimised for enterprise instruction following.</summary>
public sealed class SnowflakeArcticProvider : ILLMProvider
{
    public string Name => "SnowflakeArctic";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: false,
        SupportsVision: false,
        EmbeddingDimensions: 0,
        MaxContextTokens: 4_096,
        SupportedModels: ["snowflake-arctic-instruct", "arctic-embed-m", "arctic-embed-l"]);
    public int EmbeddingDimensions => 0;

    private readonly OpenAIProvider _inner;
    public SnowflakeArcticProvider(LLMWIKYConfig config)
        => _inner = new OpenAIProvider(config);

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotSupportedException("SnowflakeArctic: configure a Cortex EMBED endpoint.");
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => throw new NotSupportedException("SnowflakeArctic: configure a Cortex EMBED endpoint.");
}

// ── M-47 — Phind ──────────────────────────────────────────────────────────────
/// <summary>Phind search-augmented developer LLM provider.
/// API endpoint at api.phind.com/agent. Phind-70B is fine-tuned for coding tasks.</summary>
public sealed class PhindProvider : ILLMProvider
{
    public string Name => "Phind";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: false,
        SupportsVision: false,
        EmbeddingDimensions: 0,
        MaxContextTokens: 16_384,
        SupportedModels: ["Phind-70B", "Phind-34B-Python-v1", "Phind-CodeLlama-34B-v2"]);
    public int EmbeddingDimensions => 0;

    private readonly OpenAIProvider _inner;
    public PhindProvider(LLMWIKYConfig config)
        => _inner = new OpenAIProvider(config with { Endpoint = "https://api.phind.com/agent/" });

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotSupportedException("Phind does not support embeddings.");
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => throw new NotSupportedException("Phind does not support embeddings.");
}

// ── M-48 — You.com ────────────────────────────────────────────────────────────
/// <summary>You.com web search + chat provider. Responses grounded in live web results.
/// OpenAI-compatible API at chat-api.you.com/v1.</summary>
public sealed class YouComProvider : ILLMProvider
{
    public string Name => "YouCom";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: false,
        EmbeddingDimensions: 0,
        MaxContextTokens: 128_000,
        SupportedModels: ["you-claude-3-5-sonnet", "you-gpt-4o", "you-mistral-large"]);
    public int EmbeddingDimensions => 0;

    private readonly OpenAIProvider _inner;
    public YouComProvider(LLMWIKYConfig config)
        => _inner = new OpenAIProvider(config with { Endpoint = "https://chat-api.you.com/v1" });

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotSupportedException("YouCom does not support embeddings.");
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => throw new NotSupportedException("YouCom does not support embeddings.");
}

// ── M-49 — Hyperbolic ─────────────────────────────────────────────────────────
/// <summary>Hyperbolic decentralised inference marketplace. Hosts Llama, Mistral, and
/// open-source models. OpenAI-compatible API at api.hyperbolic.xyz/v1.</summary>
public sealed class HyperbolicProvider : ILLMProvider
{
    public string Name => "Hyperbolic";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: false,
        EmbeddingDimensions: 0,
        MaxContextTokens: 131_072,
        SupportedModels: ["meta-llama/Meta-Llama-3.1-70B-Instruct", "NovaSky-AI/Sky-T1-32B-Preview", "deepseek-ai/DeepSeek-V3"]);
    public int EmbeddingDimensions => 0;

    private readonly OpenAIProvider _inner;
    public HyperbolicProvider(LLMWIKYConfig config)
        => _inner = new OpenAIProvider(config with { Endpoint = "https://api.hyperbolic.xyz/v1" });

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotSupportedException("Hyperbolic does not support embeddings via the chat endpoint.");
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => throw new NotSupportedException("Hyperbolic does not support embeddings via the chat endpoint.");
}

// ── M-50 — Featherless ────────────────────────────────────────────────────────
/// <summary>Featherless AI serverless open-model inference.
/// OpenAI-compatible API at api.featherless.ai/v1. Hundreds of open-source models.</summary>
public sealed class FeatherlessProvider : ILLMProvider
{
    public string Name => "Featherless";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: false,
        EmbeddingDimensions: 0,
        MaxContextTokens: 131_072,
        SupportedModels: ["meta-llama/Meta-Llama-3.1-70B-Instruct", "mistralai/Mistral-7B-Instruct-v0.3", "Qwen/Qwen2.5-72B-Instruct"]);
    public int EmbeddingDimensions => 0;

    private readonly OpenAIProvider _inner;
    public FeatherlessProvider(LLMWIKYConfig config)
        => _inner = new OpenAIProvider(config with { Endpoint = "https://api.featherless.ai/v1" });

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotSupportedException("Featherless does not support embeddings via the chat endpoint.");
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => throw new NotSupportedException("Featherless does not support embeddings via the chat endpoint.");
}
