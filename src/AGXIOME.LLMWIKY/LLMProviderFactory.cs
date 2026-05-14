using AGXIOME.LLMWIKY.Http;
using AGXIOME.Shared.Config;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.LLMWIKY;

/// <summary>Provider factory: create, register, discover LLM providers.
/// Keys are <see cref="LLMProviderType"/> — no magic strings in routing logic.</summary>
public sealed class LLMProviderFactory : ILLMProviderFactory
{
    private static Dictionary<LLMProviderType, Func<LLMWIKYConfig, ILLMProvider>> BuildRegistry(
        IHttpClientFactory? hcf) => new()
    {
        [LLMProviderType.OpenAI]            = c => new Providers.OpenAIProvider(c, hcf),
        [LLMProviderType.Anthropic]         = c => new Providers.AnthropicProvider(c, hcf),
        [LLMProviderType.DeepSeek]          = c => new Providers.OpenAIProvider(c with { Endpoint = "https://api.deepseek.com/v1" }, hcf),
        [LLMProviderType.Ollama]            = c => new Providers.OllamaProvider(c, hcf),
        [LLMProviderType.OllamaResponses]   = c => new Providers.OllamaResponsesProvider(c, hcf),
        [LLMProviderType.Local]             = c => new Providers.LocalProvider(c, hcf),
        [LLMProviderType.AzureOpenAI]       = c => new Providers.AzureOpenAIProvider(c, hcf),
        [LLMProviderType.Gemini]            = c => new Providers.GeminiProvider(c, hcf),
        [LLMProviderType.Custom]            = c => new Providers.OpenAIProvider(c, hcf),
        [LLMProviderType.Groq]                 = c => new Providers.GroqProvider(c, hcf),
        [LLMProviderType.Mistral]              = c => new Providers.MistralAIProvider(c, hcf),
        [LLMProviderType.XAIGrok]              = c => new Providers.XAIGrokProvider(c, hcf),
        [LLMProviderType.Cohere]               = c => new Providers.CohereProvider(c, hcf),
        [LLMProviderType.Perplexity]           = c => new Providers.PerplexityProvider(c, hcf),
        [LLMProviderType.TogetherAI]           = c => new Providers.TogetherAIProvider(c, hcf),
        [LLMProviderType.FireworksAI]          = c => new Providers.FireworksAIProvider(c, hcf),
        [LLMProviderType.Cerebras]             = c => new Providers.CerebrasProvider(c, hcf),
        [LLMProviderType.AWSBedrock]           = c => new Providers.AWSBedrockProvider(c, hcf),
        [LLMProviderType.AzureAIFoundry]       = c => new Providers.AzureAIFoundryProvider(c, hcf),
        [LLMProviderType.IBMWatsonX]           = c => new Providers.IBMWatsonXProvider(c, hcf),
        [LLMProviderType.SambaNova]            = c => new Providers.SambaNovaProvider(c, hcf),
        [LLMProviderType.OpenRouter]           = c => new Providers.OpenRouterProvider(c, hcf),
        [LLMProviderType.LiteLLM]              = c => new Providers.LiteLLMProvider(c, hcf),
        [LLMProviderType.AIMLApi]              = c => new Providers.AIMLApiProvider(c, hcf),
        [LLMProviderType.NovitaAI]             = c => new Providers.NovitaAIProvider(c, hcf),
        [LLMProviderType.DeepInfra]            = c => new Providers.DeepInfraProvider(c, hcf),
        [LLMProviderType.Replicate]            = c => new Providers.ReplicateProvider(c, hcf),
        [LLMProviderType.RekaAI]               = c => new Providers.RekaAIProvider(c, hcf),
        [LLMProviderType.Modal]                = c => new Providers.ModalProvider(c, hcf),
        [LLMProviderType.LlamaCpp]             = c => new Providers.LlamaCppProvider(c, hcf),
        [LLMProviderType.LMStudio]             = c => new Providers.LMStudioProvider(c, hcf),
        [LLMProviderType.LocalAI]              = c => new Providers.LocalAIProvider(c, hcf),
        [LLMProviderType.GPT4All]              = c => new Providers.GPT4AllProvider(c, hcf),
        [LLMProviderType.TextGenWebUI]         = c => new Providers.TextGenWebUIProvider(c, hcf),
        [LLMProviderType.KoboldCPP]            = c => new Providers.KoboldCPPProvider(c, hcf),
        [LLMProviderType.HuggingFaceInference] = c => new Providers.HuggingFaceInferenceProvider(c, hcf),
        [LLMProviderType.HuggingFaceTGI]       = c => new Providers.HuggingFaceTGIProvider(c, hcf),
        [LLMProviderType.AI21Labs]             = c => new Providers.AI21LabsProvider(c, hcf),
        [LLMProviderType.Writer]               = c => new Providers.WriterProvider(c, hcf),
        [LLMProviderType.NvidiaNIM]            = c => new Providers.NvidiaNIMProvider(c, hcf),
        [LLMProviderType.Anyscale]             = c => new Providers.AnyscaleProvider(c, hcf),
        [LLMProviderType.OctoAI]               = c => new Providers.OctoAIProvider(c, hcf),
        [LLMProviderType.LeptonAI]             = c => new Providers.LeptonAIProvider(c, hcf),
        [LLMProviderType.FriendliAI]           = c => new Providers.FriendliAIProvider(c, hcf),
        [LLMProviderType.MoonshotKimi]         = c => new Providers.MoonshotKimiProvider(c, hcf),
        [LLMProviderType.ZhipuGLM]             = c => new Providers.ZhipuGLMProvider(c, hcf),
        [LLMProviderType.Baichuan]             = c => new Providers.BaichuanProvider(c, hcf),
        [LLMProviderType.ByteDanceDoubao]      = c => new Providers.ByteDanceDoubaoProvider(c, hcf),
        [LLMProviderType.AlibabaQwen]          = c => new Providers.AlibabaQwenProvider(c, hcf),
        [LLMProviderType.BaiduERNIE]           = c => new Providers.BaiduERNIEProvider(c, hcf),
        [LLMProviderType.TencentHunyuan]       = c => new Providers.TencentHunyuanProvider(c, hcf),
        [LLMProviderType.YiAI]                 = c => new Providers.YiAIProvider(c, hcf),
        [LLMProviderType.AI21Jamba]            = c => new Providers.AI21JambaProvider(c, hcf),
        [LLMProviderType.SnowflakeArctic]      = c => new Providers.SnowflakeArcticProvider(c, hcf),
        [LLMProviderType.Phind]                = c => new Providers.PhindProvider(c, hcf),
        [LLMProviderType.YouCom]               = c => new Providers.YouComProvider(c, hcf),
        [LLMProviderType.Hyperbolic]           = c => new Providers.HyperbolicProvider(c, hcf),
        [LLMProviderType.Featherless]          = c => new Providers.FeatherlessProvider(c, hcf),
    };

    private readonly Dictionary<LLMProviderType, Func<LLMWIKYConfig, ILLMProvider>> _registry;

    /// <summary>
    /// Create a factory that uses its own <c>new HttpClient()</c> per provider (backward-compat, tests).
    /// </summary>
    public LLMProviderFactory() => _registry = BuildRegistry(null);

    /// <summary>
    /// Create a factory backed by ASP.NET Core's <see cref="IHttpClientFactory"/>.
    /// All providers will receive a named, pooled HttpClient instead of <c>new HttpClient()</c>.
    /// </summary>
    public LLMProviderFactory(IHttpClientFactory httpClientFactory)
        => _registry = BuildRegistry(httpClientFactory);

    public void RegisterProvider(LLMProviderType type, Func<LLMWIKYConfig, ILLMProvider> factory)
        => _registry[type] = factory;

    void ILLMProviderFactory.RegisterProvider(string name, Func<LLMWIKYConfig, ILLMProvider> factory)
    {
        if (!Enum.TryParse<LLMProviderType>(name, ignoreCase: true, out var t))
            throw new ArgumentException($"Unknown LLMProviderType name: '{name}'");
        _registry[t] = factory;
    }

    public ILLMProvider CreateProvider(LLMWIKYConfig config)
    {
        if (!_registry.TryGetValue(config.Provider, out var factory))
            throw new ArgumentException($"No factory registered for provider: {config.Provider}");
        return factory(config);
    }

    public IReadOnlyList<ProviderDescriptor> ListAvailableProviders()
        => _registry.Select(kv =>
        {
            var dummy = kv.Value(new LLMWIKYConfig { Provider = kv.Key });
            var isLocal = kv.Key is LLMProviderType.Ollama or LLMProviderType.Local or LLMProviderType.LlamaCpp
                or LLMProviderType.LMStudio or LLMProviderType.GPT4All or LLMProviderType.TextGenWebUI
                or LLMProviderType.KoboldCPP or LLMProviderType.OllamaResponses or LLMProviderType.LocalAI;
            return new ProviderDescriptor(kv.Key.ToString(), dummy.Capabilities.SupportedModels,
                !isLocal, isLocal, true);
        }).ToList();

    public static ILLMProvider Create(LLMWIKYConfig config)
        => new LLMProviderFactory().CreateProvider(config);
}
