using AGXIOME.Shared.Models;
using AGXIOME.Shared.Config;

namespace AGXIOME.Shared.Interfaces;

public interface ILLMProvider
{
    string Name { get; }
    LLMProviderCapabilities Capabilities { get; }
    Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest request, CancellationToken ct = default);
    IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest request, CancellationToken ct = default);
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
    Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default);
    int EmbeddingDimensions { get; }
}

public interface ILLMProviderFactory
{
    ILLMProvider CreateProvider(LLMWIKYConfig config);
    void RegisterProvider(string name, Func<LLMWIKYConfig, ILLMProvider> factory);
    IReadOnlyList<ProviderDescriptor> ListAvailableProviders();
}
