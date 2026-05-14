using AGXIOME.Shared.Config;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.LLMWIKY;

/// <summary>Provider factory: create, register, discover LLM providers.
/// Keys are <see cref="LLMProviderType"/> — no magic strings in routing logic.</summary>
public sealed class LLMProviderFactory : ILLMProviderFactory
{
    private static readonly Dictionary<LLMProviderType, Func<LLMWIKYConfig, ILLMProvider>> _builtIn = new()
    {
        [LLMProviderType.OpenAI]            = c => new Providers.OpenAIProvider(c),
        [LLMProviderType.Anthropic]         = c => new Providers.AnthropicProvider(c),
        [LLMProviderType.DeepSeek]          = c => new Providers.OpenAIProvider(c with { Endpoint = "https://api.deepseek.com/v1" }),
        [LLMProviderType.Ollama]            = c => new Providers.OllamaProvider(c),
        [LLMProviderType.OllamaResponses]   = c => new Providers.OllamaResponsesProvider(c),
        [LLMProviderType.Local]             = c => new Providers.LocalProvider(c),
        [LLMProviderType.AzureOpenAI]       = c => new Providers.AzureOpenAIProvider(c),
        [LLMProviderType.Gemini]            = c => new Providers.GeminiProvider(c),
        [LLMProviderType.Custom]            = c => new Providers.OpenAIProvider(c),
        [LLMProviderType.Groq]                 = c => new Providers.GroqProvider(c),
        [LLMProviderType.Mistral]              = c => new Providers.MistralAIProvider(c),
        [LLMProviderType.XAIGrok]              = c => new Providers.XAIGrokProvider(c),
        [LLMProviderType.Cohere]               = c => new Providers.CohereProvider(c),
        [LLMProviderType.Perplexity]           = c => new Providers.PerplexityProvider(c),
        [LLMProviderType.TogetherAI]           = c => new Providers.TogetherAIProvider(c),
        [LLMProviderType.FireworksAI]          = c => new Providers.FireworksAIProvider(c),
        [LLMProviderType.Cerebras]             = c => new Providers.CerebrasProvider(c),
        [LLMProviderType.AWSBedrock]           = c => new Providers.AWSBedrockProvider(c),
        [LLMProviderType.AzureAIFoundry]       = c => new Providers.AzureAIFoundryProvider(c),
        [LLMProviderType.IBMWatsonX]           = c => new Providers.IBMWatsonXProvider(c),
        [LLMProviderType.SambaNova]            = c => new Providers.SambaNovaProvider(c),
        [LLMProviderType.OpenRouter]           = c => new Providers.OpenRouterProvider(c),
        [LLMProviderType.LiteLLM]              = c => new Providers.LiteLLMProvider(c),
        [LLMProviderType.AIMLApi]              = c => new Providers.AIMLApiProvider(c),
        [LLMProviderType.NovitaAI]             = c => new Providers.NovitaAIProvider(c),
        [LLMProviderType.DeepInfra]            = c => new Providers.DeepInfraProvider(c),
        [LLMProviderType.Replicate]            = c => new Providers.ReplicateProvider(c),
        [LLMProviderType.RekaAI]               = c => new Providers.RekaAIProvider(c),
        [LLMProviderType.Modal]                = c => new Providers.ModalProvider(c),
        [LLMProviderType.LlamaCpp]             = c => new Providers.LlamaCppProvider(c),
        [LLMProviderType.LMStudio]             = c => new Providers.LMStudioProvider(c),
        [LLMProviderType.LocalAI]              = c => new Providers.LocalAIProvider(c),
        [LLMProviderType.GPT4All]              = c => new Providers.GPT4AllProvider(c),
        [LLMProviderType.TextGenWebUI]         = c => new Providers.TextGenWebUIProvider(c),
        [LLMProviderType.KoboldCPP]            = c => new Providers.KoboldCPPProvider(c),
        [LLMProviderType.HuggingFaceInference] = c => new Providers.HuggingFaceInferenceProvider(c),
        [LLMProviderType.HuggingFaceTGI]       = c => new Providers.HuggingFaceTGIProvider(c),
        [LLMProviderType.AI21Labs]             = c => new Providers.AI21LabsProvider(c),
        [LLMProviderType.Writer]               = c => new Providers.WriterProvider(c),
        [LLMProviderType.NvidiaNIM]            = c => new Providers.NvidiaNIMProvider(c),
        [LLMProviderType.Anyscale]             = c => new Providers.AnyscaleProvider(c),
        [LLMProviderType.OctoAI]               = c => new Providers.OctoAIProvider(c),
        [LLMProviderType.LeptonAI]             = c => new Providers.LeptonAIProvider(c),
        [LLMProviderType.FriendliAI]           = c => new Providers.FriendliAIProvider(c),
        [LLMProviderType.MoonshotKimi]         = c => new Providers.MoonshotKimiProvider(c),
        [LLMProviderType.ZhipuGLM]             = c => new Providers.ZhipuGLMProvider(c),
        [LLMProviderType.Baichuan]             = c => new Providers.BaichuanProvider(c),
        [LLMProviderType.ByteDanceDoubao]      = c => new Providers.ByteDanceDoubaoProvider(c),
        [LLMProviderType.AlibabaQwen]          = c => new Providers.AlibabaQwenProvider(c),
        [LLMProviderType.BaiduERNIE]           = c => new Providers.BaiduERNIEProvider(c),
        [LLMProviderType.TencentHunyuan]       = c => new Providers.TencentHunyuanProvider(c),
        [LLMProviderType.YiAI]                 = c => new Providers.YiAIProvider(c),
        [LLMProviderType.AI21Jamba]            = c => new Providers.AI21JambaProvider(c),
        [LLMProviderType.SnowflakeArctic]      = c => new Providers.SnowflakeArcticProvider(c),
        [LLMProviderType.Phind]                = c => new Providers.PhindProvider(c),
        [LLMProviderType.YouCom]               = c => new Providers.YouComProvider(c),
        [LLMProviderType.Hyperbolic]           = c => new Providers.HyperbolicProvider(c),
        [LLMProviderType.Featherless]          = c => new Providers.FeatherlessProvider(c),
    };

    private readonly Dictionary<LLMProviderType, Func<LLMWIKYConfig, ILLMProvider>> _registry;

    public LLMProviderFactory() => _registry = new(_builtIn);

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
