using AGXIOME.Shared.Config;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.LLMWIKY.Providers;

// ── M-28 — HuggingFace Inference API ─────────────────────────────────────────
/// <summary>HuggingFace Inference API provider. Access 200k+ community models.
/// OpenAI-compatible endpoint at api-inference.huggingface.co/v1.
/// Use <c>LLMWIKYConfig.Model</c> to specify the HuggingFace model repo ID.</summary>
public sealed class HuggingFaceInferenceProvider : ILLMProvider
{
    public string Name => "HuggingFaceInference";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: false,
        SupportsVision: false,
        EmbeddingDimensions: 0,
        MaxContextTokens: 131_072,
        SupportedModels: ["meta-llama/Meta-Llama-3.1-70B-Instruct", "mistralai/Mistral-7B-Instruct-v0.3", "Qwen/Qwen2.5-72B-Instruct"]);
    public int EmbeddingDimensions => 0;

    private readonly OpenAIProvider _inner;
    public HuggingFaceInferenceProvider(LLMWIKYConfig config)
        => _inner = new OpenAIProvider(config with { Endpoint = "https://api-inference.huggingface.co/v1" });

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotSupportedException("HuggingFaceInference: use a dedicated feature-extraction endpoint for embeddings.");
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => throw new NotSupportedException("HuggingFaceInference: use a dedicated feature-extraction endpoint for embeddings.");
}

// ── M-29 — HuggingFace TGI ───────────────────────────────────────────────────
/// <summary>HuggingFace Text Generation Inference (TGI) self-hosted provider.
/// OpenAI-compatible endpoint exposed by TGI v1.4+. Configure <c>Endpoint</c> to the
/// TGI container URL (e.g., http://localhost:8080/v1) and <c>Model</c> to the served model.</summary>
public sealed class HuggingFaceTGIProvider : ILLMProvider
{
    public string Name => "HuggingFaceTGI";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: false,
        EmbeddingDimensions: 0,
        MaxContextTokens: 131_072,
        SupportedModels: ["meta-llama/Meta-Llama-3.1-70B-Instruct", "mistralai/Mistral-7B-v0.3"]);
    public int EmbeddingDimensions => 0;

    private readonly OpenAIProvider _inner;
    public HuggingFaceTGIProvider(LLMWIKYConfig config)
        => _inner = new OpenAIProvider(config with { Endpoint = config.Endpoint ?? "http://localhost:8080/v1" });

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotSupportedException("HuggingFaceTGI: use a TEI (Text Embeddings Inference) container for embeddings.");
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => throw new NotSupportedException("HuggingFaceTGI: use a TEI (Text Embeddings Inference) container for embeddings.");
}
