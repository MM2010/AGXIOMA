using AGXIOME.Shared.Config;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.LLMWIKY.Providers;

/// <summary>Local provider (vLLM, TGI, llama.cpp) — reuses OpenAI-compatible API.</summary>
public sealed class LocalProvider : ILLMProvider
{
    private readonly LLMWIKYConfig _config;
    public string Name => "Local";
    public LLMProviderCapabilities Capabilities => new(true, true, false, _config.EmbeddingDimensions, 8_192,
        new[] { "llama-3.1-8b", "mistral-7b", "mixtral-8x22b" });
    public int EmbeddingDimensions => _config.EmbeddingDimensions;

    private readonly OpenAIProvider _openaiCompat;

    public LocalProvider(LLMWIKYConfig config, IHttpClientFactory? httpClientFactory = null)
    {
        _config = config;
        var compatConfig = config with
        {
            Endpoint = config.Endpoint ?? "http://localhost:8080/v1",
            ApiKey = "not-needed"
        };
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
