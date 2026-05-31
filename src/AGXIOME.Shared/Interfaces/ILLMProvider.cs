using AGXIOME.Shared.Models;
using AGXIOME.Shared.Config;

namespace AGXIOME.Shared.Interfaces;

/// <summary>LLM provider abstraction. Wraps any language model (OpenAI, Anthropic, Ollama, etc.) with unified completion, streaming, and embedding APIs. Implementations handle auth, retry, rate limiting.</summary>
/// <remarks>All 50+ providers implement this. Use <see cref="ILLMProviderFactory"/> to create instances. Thread-safe.</remarks>
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

/// <summary>LLM provider abstraction. Wraps any language model (OpenAI, Anthropic, Ollama, etc.) with unified completion, streaming, and embedding APIs. Implementations handle auth, retry, rate limiting.</summary>
/// <remarks>All 50+ providers implement this. Use <see cref="ILLMProviderFactory"/> to create instances. Thread-safe.</remarks>
/// <summary>Factory for creating <see cref="ILLMProvider"/> instances from configuration. Supports custom provider registration.</summary>
public interface ILLMProviderFactory
{
    ILLMProvider CreateProvider(LLMWIKYConfig config);
    void RegisterProvider(string name, Func<LLMWIKYConfig, ILLMProvider> factory);
    IReadOnlyList<ProviderDescriptor> ListAvailableProviders();
}
