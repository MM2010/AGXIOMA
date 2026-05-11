using AGXIOME.Shared.Config;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.LLMWIKY;

/// <summary>Provider factory: create, register, discover LLM providers.</summary>
public sealed class LLMProviderFactory : ILLMProviderFactory
{
    private static readonly Dictionary<string, Func<LLMWIKYConfig, ILLMProvider>> _registry = new()
    {
        ["OpenAI"]      = c => new Providers.OpenAIProvider(c),
        ["Anthropic"]   = c => new Providers.AnthropicProvider(c),
        ["DeepSeek"]    = c => new Providers.OpenAIProvider(c with { Endpoint = "https://api.deepseek.com/v1" }),
        ["Ollama"]      = c => new Providers.OllamaProvider(c),
        ["Local"]       = c => new Providers.LocalProvider(c),
        ["AzureOpenAI"] = c => new Providers.AzureOpenAIProvider(c),
        ["Gemini"]      = c => new Providers.GeminiProvider(c),
        ["Custom"]      = c => new Providers.OpenAIProvider(c), // OpenAI-compat
    };

    private readonly Dictionary<string, Func<LLMWIKYConfig, ILLMProvider>> _instanceRegistry = new(_registry);

    public void RegisterProvider(string name, Func<LLMWIKYConfig, ILLMProvider> factory)
        => _instanceRegistry[name] = factory;

    public ILLMProvider CreateProvider(LLMWIKYConfig config)
    {
        if (!_instanceRegistry.TryGetValue(config.Provider, out var factory))
            throw new ArgumentException($"Unknown provider: {config.Provider}");
        return factory(config);
    }

    public IReadOnlyList<ProviderDescriptor> ListAvailableProviders()
        => _instanceRegistry.Select(kv =>
        {
            var dummy = kv.Value(new LLMWIKYConfig());
            return new ProviderDescriptor(kv.Key, dummy.Capabilities.SupportedModels,
                kv.Key is not "Ollama" and not "Local",
                kv.Key is "Ollama" or "Local" or "Custom", true);
        }).ToList();

    public static ILLMProvider Create(LLMWIKYConfig config)
        => new LLMProviderFactory().CreateProvider(config);
}
