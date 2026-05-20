using AGXIOME.Shared.Config;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.LlmWiky.Providers;

// ── M-30 — AI21 Labs ─────────────────────────────────────────────────────────
/// <summary>AI21 Labs provider. Jamba SSM-Transformer and Jurassic-2 series.
/// OpenAI-compatible API at api.ai21.com/studio/v1.</summary>
public sealed class AI21LabsProvider : ILLMProvider
{
    public string Name => "AI21Labs";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: false,
        EmbeddingDimensions: 0,
        MaxContextTokens: 256_000,
        SupportedModels: ["jamba-1.5-large", "jamba-1.5-mini", "j2-ultra", "j2-mid"]);
    public int EmbeddingDimensions => 0;

    private readonly OpenAIProvider _inner;
    public AI21LabsProvider(LLMWIKYConfig config, IHttpClientFactory? httpClientFactory = null)
        => _inner = new OpenAIProvider(config with { Endpoint = "https://api.ai21.com/studio/v1" }, httpClientFactory);

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotSupportedException("AI21Labs does not support embeddings via chat endpoint.");
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => throw new NotSupportedException("AI21Labs does not support embeddings via chat endpoint.");
}

// ── M-31 — Writer ─────────────────────────────────────────────────────────────
/// <summary>Writer Palmyra enterprise LLM provider.
/// OpenAI-compatible API at api.writer.com/v1. Purpose-built for enterprise content workflows.</summary>
public sealed class WriterProvider : ILLMProvider
{
    public string Name => "Writer";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: false,
        EmbeddingDimensions: 0,
        MaxContextTokens: 128_000,
        SupportedModels: ["palmyra-x-004", "palmyra-x-003-instruct", "palmyra-med", "palmyra-fin"]);
    public int EmbeddingDimensions => 0;

    private readonly OpenAIProvider _inner;
    public WriterProvider(LLMWIKYConfig config, IHttpClientFactory? httpClientFactory = null)
        => _inner = new OpenAIProvider(config with { Endpoint = "https://api.writer.com/v1" }, httpClientFactory);

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotSupportedException("Writer does not support embeddings via the chat endpoint.");
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => throw new NotSupportedException("Writer does not support embeddings via the chat endpoint.");
}

// ── M-32 — NVIDIA NIM ─────────────────────────────────────────────────────────
/// <summary>NVIDIA NIM inference microservices. Access Llama, Nemotron, and optimised
/// NVIDIA-hosted models via OpenAI-compatible API.
/// Default endpoint: integrate.api.nvidia.com/v1</summary>
public sealed class NvidiaNIMProvider : ILLMProvider
{
    public string Name => "NvidiaNIM";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: false,
        EmbeddingDimensions: 0,
        MaxContextTokens: 131_072,
        SupportedModels: ["nvidia/llama-3.1-nemotron-70b-instruct", "meta/llama-3.1-70b-instruct", "mistralai/mistral-7b-instruct-v0.3"]);
    public int EmbeddingDimensions => 0;

    private readonly OpenAIProvider _inner;
    public NvidiaNIMProvider(LLMWIKYConfig config, IHttpClientFactory? httpClientFactory = null)
        => _inner = new OpenAIProvider(config with { Endpoint = config.Endpoint ?? "https://integrate.api.nvidia.com/v1" }, httpClientFactory);

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotSupportedException("NvidiaNIM: use dedicated NIM embedding containers for embeddings.");
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => throw new NotSupportedException("NvidiaNIM: use dedicated NIM embedding containers for embeddings.");
}

// ── M-33 — Anyscale ───────────────────────────────────────────────────────────
/// <summary>Anyscale Ray-backed inference endpoint for open-source models.
/// OpenAI-compatible API at api.endpoints.anyscale.com/v1.</summary>
public sealed class AnyscaleProvider : ILLMProvider
{
    public string Name => "Anyscale";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: false,
        EmbeddingDimensions: 0,
        MaxContextTokens: 131_072,
        SupportedModels: ["meta-llama/Meta-Llama-3.1-70B-Instruct", "mistralai/Mixtral-8x7B-Instruct-v0.1"]);
    public int EmbeddingDimensions => 0;

    private readonly OpenAIProvider _inner;
    public AnyscaleProvider(LLMWIKYConfig config, IHttpClientFactory? httpClientFactory = null)
        => _inner = new OpenAIProvider(config with { Endpoint = "https://api.endpoints.anyscale.com/v1" }, httpClientFactory);

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotSupportedException("Anyscale does not support embeddings via the chat endpoint.");
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => throw new NotSupportedException("Anyscale does not support embeddings via the chat endpoint.");
}

// ── M-34 — OctoAI ─────────────────────────────────────────────────────────────
/// <summary>OctoAI optimised inference platform. OpenAI-compatible at text.octoai.run/v1.
/// Supports Llama, Mixtral, and fine-tuned variants with hardware-accelerated serving.</summary>
public sealed class OctoAIProvider : ILLMProvider
{
    public string Name => "OctoAI";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: false,
        EmbeddingDimensions: 0,
        MaxContextTokens: 131_072,
        SupportedModels: ["meta-llama-3.1-70b-instruct", "mixtral-8x7b-instruct-fp16", "hermes-2-pro-llama-3-8b"]);
    public int EmbeddingDimensions => 0;

    private readonly OpenAIProvider _inner;
    public OctoAIProvider(LLMWIKYConfig config, IHttpClientFactory? httpClientFactory = null)
        => _inner = new OpenAIProvider(config with { Endpoint = "https://text.octoai.run/v1" }, httpClientFactory);

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotSupportedException("OctoAI does not expose an embeddings endpoint.");
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => throw new NotSupportedException("OctoAI does not expose an embeddings endpoint.");
}

// ── M-35 — Lepton AI ─────────────────────────────────────────────────────────
/// <summary>Lepton AI serverless inference platform.
/// Endpoint is workspace-specific: https://{workspace}.lepton.run/api/v1.
/// Configure <c>LLMWIKYConfig.Endpoint</c> to the workspace URL.</summary>
public sealed class LeptonAIProvider : ILLMProvider
{
    public string Name => "LeptonAI";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: false,
        EmbeddingDimensions: 0,
        MaxContextTokens: 131_072,
        SupportedModels: ["llama3-1-70b", "mixtral-8x7b", "mistral-7b"]);
    public int EmbeddingDimensions => 0;

    private readonly OpenAIProvider _inner;
    public LeptonAIProvider(LLMWIKYConfig config, IHttpClientFactory? httpClientFactory = null)
        => _inner = new OpenAIProvider(config, httpClientFactory);

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotSupportedException("LeptonAI does not support embeddings via this endpoint.");
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => throw new NotSupportedException("LeptonAI does not support embeddings via this endpoint.");
}

// ── M-36 — Friendli AI ────────────────────────────────────────────────────────
/// <summary>Friendli AI serverless inference. Efficient GPU inference for open-source models.
/// OpenAI-compatible API at inference.friendli.ai/v1.</summary>
public sealed class FriendliAIProvider : ILLMProvider
{
    public string Name => "FriendliAI";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: false,
        EmbeddingDimensions: 0,
        MaxContextTokens: 131_072,
        SupportedModels: ["meta-llama-3.1-70b-instruct", "mixtral-8x7b-instruct-v01", "deepseek-coder-v2-instruct"]);
    public int EmbeddingDimensions => 0;

    private readonly OpenAIProvider _inner;
    public FriendliAIProvider(LLMWIKYConfig config, IHttpClientFactory? httpClientFactory = null)
        => _inner = new OpenAIProvider(config with { Endpoint = "https://inference.friendli.ai/v1" }, httpClientFactory);

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotSupportedException("FriendliAI does not support embeddings via the inference endpoint.");
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => throw new NotSupportedException("FriendliAI does not support embeddings via the inference endpoint.");
}
