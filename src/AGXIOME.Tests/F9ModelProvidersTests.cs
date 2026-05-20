using AGXIOME.Core.DependencyInjection;
using AGXIOME.LlmWiky;
using AGXIOME.LlmWiky.Providers;
using AGXIOME.Shared.Config;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using Microsoft.Extensions.Configuration;

namespace AGXIOME.Tests;

public sealed class F9ModelProvidersTests
{
    public static IEnumerable<object?[]> ProviderCases()
    {
        yield return Case(LLMProviderType.Groq, typeof(GroqProvider));
        yield return Case(LLMProviderType.Mistral, typeof(MistralAIProvider));
        yield return Case(LLMProviderType.XAIGrok, typeof(XAIGrokProvider));
        yield return Case(LLMProviderType.Cohere, typeof(CohereProvider));
        yield return Case(LLMProviderType.Perplexity, typeof(PerplexityProvider));
        yield return Case(LLMProviderType.TogetherAI, typeof(TogetherAIProvider));
        yield return Case(LLMProviderType.FireworksAI, typeof(FireworksAIProvider));
        yield return Case(LLMProviderType.Cerebras, typeof(CerebrasProvider));
        yield return Case(LLMProviderType.AWSBedrock, typeof(AWSBedrockProvider));
        yield return Case(LLMProviderType.AzureAIFoundry, typeof(AzureAIFoundryProvider));
        yield return Case(LLMProviderType.IBMWatsonX, typeof(IBMWatsonXProvider));
        yield return Case(LLMProviderType.SambaNova, typeof(SambaNovaProvider));
        yield return Case(LLMProviderType.OpenRouter, typeof(OpenRouterProvider));
        yield return Case(LLMProviderType.LiteLLM, typeof(LiteLLMProvider));
        yield return Case(LLMProviderType.AIMLApi, typeof(AIMLApiProvider));
        yield return Case(LLMProviderType.NovitaAI, typeof(NovitaAIProvider));
        yield return Case(LLMProviderType.DeepInfra, typeof(DeepInfraProvider));
        yield return Case(LLMProviderType.Replicate, typeof(ReplicateProvider));
        yield return Case(LLMProviderType.RekaAI, typeof(RekaAIProvider));
        yield return Case(LLMProviderType.Modal, typeof(ModalProvider));
        yield return Case(LLMProviderType.LlamaCpp, typeof(LlamaCppProvider));
        yield return Case(LLMProviderType.LMStudio, typeof(LMStudioProvider));
        yield return Case(LLMProviderType.OllamaResponses, typeof(OllamaResponsesProvider));
        yield return Case(LLMProviderType.LocalAI, typeof(LocalAIProvider));
        yield return Case(LLMProviderType.GPT4All, typeof(GPT4AllProvider));
        yield return Case(LLMProviderType.TextGenWebUI, typeof(TextGenWebUIProvider));
        yield return Case(LLMProviderType.KoboldCPP, typeof(KoboldCPPProvider));
        yield return Case(LLMProviderType.HuggingFaceInference, typeof(HuggingFaceInferenceProvider));
        yield return Case(LLMProviderType.HuggingFaceTGI, typeof(HuggingFaceTGIProvider));
        yield return Case(LLMProviderType.AI21Labs, typeof(AI21LabsProvider));
        yield return Case(LLMProviderType.Writer, typeof(WriterProvider));
        yield return Case(LLMProviderType.NvidiaNIM, typeof(NvidiaNIMProvider));
        yield return Case(LLMProviderType.Anyscale, typeof(AnyscaleProvider));
        yield return Case(LLMProviderType.OctoAI, typeof(OctoAIProvider));
        yield return Case(LLMProviderType.LeptonAI, typeof(LeptonAIProvider));
        yield return Case(LLMProviderType.FriendliAI, typeof(FriendliAIProvider));
        yield return Case(LLMProviderType.MoonshotKimi, typeof(MoonshotKimiProvider));
        yield return Case(LLMProviderType.ZhipuGLM, typeof(ZhipuGLMProvider));
        yield return Case(LLMProviderType.Baichuan, typeof(BaichuanProvider));
        yield return Case(LLMProviderType.ByteDanceDoubao, typeof(ByteDanceDoubaoProvider));
        yield return Case(LLMProviderType.AlibabaQwen, typeof(AlibabaQwenProvider));
        yield return Case(LLMProviderType.BaiduERNIE, typeof(BaiduERNIEProvider));
        yield return Case(LLMProviderType.TencentHunyuan, typeof(TencentHunyuanProvider));
        yield return Case(LLMProviderType.YiAI, typeof(YiAIProvider));
        yield return Case(LLMProviderType.AI21Jamba, typeof(AI21JambaProvider));
        yield return Case(LLMProviderType.SnowflakeArctic, typeof(SnowflakeArcticProvider));
        yield return Case(LLMProviderType.Phind, typeof(PhindProvider));
        yield return Case(LLMProviderType.YouCom, typeof(YouComProvider));
        yield return Case(LLMProviderType.Hyperbolic, typeof(HyperbolicProvider));
        yield return Case(LLMProviderType.Featherless, typeof(FeatherlessProvider));
    }

    public static IEnumerable<object?[]> ResolverCases()
    {
        yield return new object?[] { "xai", LLMProviderType.XAIGrok, "grok-2", null };
        yield return new object?[] { "xaigrok", LLMProviderType.XAIGrok, "grok-2", null };
        yield return new object?[] { "ollama", LLMProviderType.Ollama, "llama3.2", "http://localhost:11434" };
        yield return new object?[] { "local", LLMProviderType.Local, "local-model", "http://localhost:1234" };
        yield return new object?[] { "unknown-provider", LLMProviderType.OpenAI, "gpt-4o", null };
    }

    [Theory]
    [MemberData(nameof(ProviderCases))]
    public void CreateProvider_ReturnsExpectedConcreteProvider(LLMProviderType providerType, Type expectedType)
    {
        var provider = new LLMProviderFactory().CreateProvider(CreateConfig(providerType));
        Assert.IsType(expectedType, provider);
    }

    [Theory]
    [MemberData(nameof(ProviderCases))]
    public void CreateProvider_ExposesConsistentCapabilities(LLMProviderType providerType, Type _)
    {
        var provider = new LLMProviderFactory().CreateProvider(CreateConfig(providerType));

        Assert.False(string.IsNullOrWhiteSpace(provider.Name));
        Assert.NotNull(provider.Capabilities);
        Assert.NotEmpty(provider.Capabilities.SupportedModels);
        Assert.Equal(provider.Capabilities.EmbeddingDimensions, provider.EmbeddingDimensions);
    }

    [Fact]
    public void Create_StaticFactory_UsesOpenAiCompatibilityForDeepSeek()
    {
        var provider = LLMProviderFactory.Create(CreateConfig(LLMProviderType.DeepSeek));

        Assert.IsType<OpenAIProvider>(provider);
        Assert.Equal("DeepSeek", provider.Name);
    }

    [Fact]
    public void CreateProvider_ThrowsForUnknownUnregisteredProvider()
    {
        var factory = new LLMProviderFactory();
        var config = CreateConfig((LLMProviderType)int.MaxValue);

        var exception = Assert.Throws<ArgumentException>(() => factory.CreateProvider(config));
        Assert.Contains("No factory registered", exception.Message);
    }

    [Fact]
    public void RegisterProvider_OverridesExistingRegistration()
    {
        var factory = new LLMProviderFactory();
        factory.RegisterProvider(LLMProviderType.Groq, config => new FakeProvider($"override:{config.Model}"));

        var provider = factory.CreateProvider(CreateConfig(LLMProviderType.Groq));

        var fake = Assert.IsType<FakeProvider>(provider);
        Assert.Equal("override:test-model", fake.Name);
    }

    [Fact]
    public void RegisterProvider_ByStringName_IsCaseInsensitive()
    {
        var factory = new LLMProviderFactory();
        ((ILLMProviderFactory)factory).RegisterProvider("gRoQ", _ => new FakeProvider("string-override"));

        var provider = factory.CreateProvider(CreateConfig(LLMProviderType.Groq));

        var fake = Assert.IsType<FakeProvider>(provider);
        Assert.Equal("string-override", fake.Name);
    }

    [Fact]
    public void RegisterProvider_ByStringName_RejectsUnknownEnumName()
    {
        var factory = new LLMProviderFactory();

        var exception = Assert.Throws<ArgumentException>(() =>
            ((ILLMProviderFactory)factory).RegisterProvider("not-a-provider", _ => new FakeProvider("invalid")));

        Assert.Contains("Unknown LLMProviderType name", exception.Message);
    }

    [Theory]
    [MemberData(nameof(ResolverCases))]
    public void Resolve_MapsProviderNameToCanonicalDefaults(string providerName, LLMProviderType expectedProvider, string expectedModel, string? expectedEndpoint)
    {
        var resolved = AgxiomeProviderResolver.Resolve(providerName, config: null);

        Assert.Equal(expectedProvider, resolved.Provider);
        Assert.Equal(expectedModel, resolved.DefaultModel);
        Assert.Equal(expectedEndpoint, resolved.DefaultEndpoint);
    }

    [Theory]
    [InlineData(LLMProviderType.Local, "local-model", "http://localhost:1234")]
    [InlineData(LLMProviderType.Ollama, "llama3.2", "http://localhost:11434")]
    [InlineData(LLMProviderType.OpenRouter, "openai/gpt-4o", null)]
    public void Defaults_ReturnExpectedModelAndEndpoint(LLMProviderType providerType, string expectedModel, string? expectedEndpoint)
    {
        var defaults = AgxiomeProviderResolver.Defaults(providerType);

        Assert.Equal(providerType, defaults.Provider);
        Assert.Equal(expectedModel, defaults.DefaultModel);
        Assert.Equal(expectedEndpoint, defaults.DefaultEndpoint);
    }

    [Fact]
    public void ResolveApiKey_PrefersConfigurationOverEnvironment()
    {
        const string envName = "GROQ_API_KEY";
        var previous = Environment.GetEnvironmentVariable(envName);
        try
        {
            Environment.SetEnvironmentVariable(envName, "env-key");
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["LLMProvider:Groq:ApiKey"] = "config-key"
                })
                .Build();

            var apiKey = AgxiomeProviderResolver.ResolveApiKey(LLMProviderType.Groq, config);

            Assert.Equal("config-key", apiKey);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envName, previous);
        }
    }

    [Fact]
    public void ResolveApiKey_FallsBackToEnvironmentWhenConfigurationMissing()
    {
        const string envName = "GROQ_API_KEY";
        var previous = Environment.GetEnvironmentVariable(envName);
        try
        {
            Environment.SetEnvironmentVariable(envName, "env-only-key");

            var apiKey = AgxiomeProviderResolver.ResolveApiKey(LLMProviderType.Groq, config: null);

            Assert.Equal("env-only-key", apiKey);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envName, previous);
        }
    }

    private static object?[] Case(LLMProviderType providerType, Type expectedType)
        => new object?[] { providerType, expectedType };

    private static LLMWIKYConfig CreateConfig(LLMProviderType providerType)
        => new()
        {
            Provider = providerType,
            ApiKey = "test-key",
            Model = "test-model",
            Endpoint = providerType == LLMProviderType.AzureAIFoundry ? "https://test.openai.azure.com" : null
        };

    private sealed class FakeProvider(string name) : ILLMProvider
    {
        public string Name => name;

        public LLMProviderCapabilities Capabilities => new(
            SupportsStreaming: false,
            SupportsFunctionCalling: false,
            SupportsVision: false,
            EmbeddingDimensions: 0,
            MaxContextTokens: 1,
            SupportedModels: ["fake"]);

        public int EmbeddingDimensions => 0;

        public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest request, CancellationToken ct = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest request, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
            => throw new NotSupportedException();
    }
}
