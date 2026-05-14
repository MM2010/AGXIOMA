using AGXIOME.Shared.Config;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.LLMWIKY.Providers;

// ── M-09 — AWS Bedrock ────────────────────────────────────────────────────────
/// <summary>AWS Bedrock provider. Routes Anthropic, Meta, Mistral, and Amazon Titan models
/// via AWS infrastructure. Endpoint is a regional Bedrock runtime URL.
/// For production, configure <c>LLMWIKYConfig.Endpoint</c> to the appropriate regional base URL.</summary>
public sealed class AWSBedrockProvider : ILLMProvider
{
    public string Name => "AWSBedrock";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: true,
        EmbeddingDimensions: 0,
        MaxContextTokens: 200_000,
        SupportedModels: ["anthropic.claude-3-5-sonnet-20241022-v2:0", "meta.llama3-1-70b-instruct-v1:0", "mistral.mistral-large-2402-v1:0", "amazon.titan-text-premier-v1:0"]);
    public int EmbeddingDimensions => 0;

    private readonly OpenAIProvider _inner;
    public AWSBedrockProvider(LLMWIKYConfig config, IHttpClientFactory? httpClientFactory = null)
        => _inner = new OpenAIProvider(config, httpClientFactory);

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotSupportedException("Use a dedicated Bedrock embeddings endpoint for AWS Titan embeddings.");
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => throw new NotSupportedException("Use a dedicated Bedrock embeddings endpoint for AWS Titan embeddings.");
}

// ── M-10 — Azure AI Foundry ───────────────────────────────────────────────────
/// <summary>Azure AI Foundry provider. Hosts Phi-4, Meta Llama, Mistral, and other
/// third-party models via Azure-managed endpoints. Configure <c>Endpoint</c> to the
/// project endpoint URL from the Azure AI Foundry portal.</summary>
public sealed class AzureAIFoundryProvider : ILLMProvider
{
    public string Name => "AzureAIFoundry";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: false,
        EmbeddingDimensions: 0,
        MaxContextTokens: 131_072,
        SupportedModels: ["Phi-4", "Phi-3.5-MoE-instruct", "Phi-3.5-mini-instruct", "Meta-Llama-3.1-70B-Instruct", "Mistral-Large-2411"]);
    public int EmbeddingDimensions => 0;

    private readonly AzureOpenAIProvider _inner;
    public AzureAIFoundryProvider(LLMWIKYConfig config, IHttpClientFactory? httpClientFactory = null)
        => _inner = new AzureOpenAIProvider(config, httpClientFactory);

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotSupportedException("AzureAIFoundry: configure a dedicated embedding deployment.");
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => throw new NotSupportedException("AzureAIFoundry: configure a dedicated embedding deployment.");
}

// ── M-11 — IBM WatsonX ────────────────────────────────────────────────────────
/// <summary>IBM WatsonX AI provider. Hosts Granite and Llama models via IBM Cloud.
/// Configure <c>Endpoint</c> to the WatsonX regional inference endpoint and provide
/// the IBM Cloud IAM API key in <c>ApiKey</c>.</summary>
public sealed class IBMWatsonXProvider : ILLMProvider
{
    public string Name => "IBMWatsonX";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: false,
        EmbeddingDimensions: 0,
        MaxContextTokens: 128_000,
        SupportedModels: ["ibm/granite-34b-code-instruct", "ibm/granite-3-8b-instruct", "meta-llama/llama-3-70b-instruct", "mistralai/mixtral-8x7b-instruct-v01"]);
    public int EmbeddingDimensions => 0;

    private readonly OpenAIProvider _inner;
    public IBMWatsonXProvider(LLMWIKYConfig config, IHttpClientFactory? httpClientFactory = null)
        => _inner = new OpenAIProvider(config, httpClientFactory);

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotSupportedException("IBMWatsonX: use Granite embedding endpoint separately.");
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => throw new NotSupportedException("IBMWatsonX: use Granite embedding endpoint separately.");
}

// ── M-12 — SambaNova ─────────────────────────────────────────────────────────
/// <summary>SambaNova provider. SN40L hardware inference for Llama 3 and Samba-1 models.
/// OpenAI-compatible API at api.sambanova.ai/v1.</summary>
public sealed class SambaNovaProvider : ILLMProvider
{
    public string Name => "SambaNova";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: false,
        EmbeddingDimensions: 0,
        MaxContextTokens: 131_072,
        SupportedModels: ["Meta-Llama-3.1-405B-Instruct", "Meta-Llama-3.1-70B-Instruct", "Meta-Llama-3.3-70B-Instruct", "Samba-1"]);
    public int EmbeddingDimensions => 0;

    private readonly OpenAIProvider _inner;
    public SambaNovaProvider(LLMWIKYConfig config, IHttpClientFactory? httpClientFactory = null)
        => _inner = new OpenAIProvider(config with { Endpoint = "https://api.sambanova.ai/v1" }, httpClientFactory);

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotSupportedException("SambaNova does not support embeddings.");
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => throw new NotSupportedException("SambaNova does not support embeddings.");
}
