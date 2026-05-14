using AGXIOME.Shared.Config;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.LLMWIKY.Providers;

/// <summary>Azure OpenAI provider — reuses OpenAI logic with Azure endpoint construction.</summary>
public sealed class AzureOpenAIProvider : ILLMProvider
{
    private readonly LLMWIKYConfig _config;
    public string Name => "AzureOpenAI";
    public LLMProviderCapabilities Capabilities => new(true, true, true, 1536, 128_000,
        new[] { "gpt-4o", "gpt-4o-mini", "gpt-4" });
    public int EmbeddingDimensions => _config.EmbeddingDimensions;

    private readonly OpenAIProvider _openaiCompat;

    public AzureOpenAIProvider(LLMWIKYConfig config, IHttpClientFactory? httpClientFactory = null)
    {
        _config = config;
        // Azure endpoint: {resource}.openai.azure.com
        var azureEndpoint = $"{config.Endpoint}/openai/deployments/{config.Model}/chat/completions?api-version={config.ApiVersion ?? "2024-08-01-preview"}";
        var compatConfig = config with { Endpoint = azureEndpoint };
        _openaiCompat = new OpenAIProvider(compatConfig, httpClientFactory);
    }

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest request, CancellationToken ct = default)
        => _openaiCompat.CompleteAsync(request, ct);

    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest request, CancellationToken ct = default)
        => _openaiCompat.CompleteStreamAsync(request, ct);

    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
        => _openaiCompat.EmbedAsync(text, ct);

    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
        => _openaiCompat.EmbedBatchAsync(texts, ct);
}
