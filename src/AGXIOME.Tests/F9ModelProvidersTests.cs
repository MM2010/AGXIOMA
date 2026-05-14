using AGXIOME.LLMWIKY;
using AGXIOME.LLMWIKY.Providers;
using AGXIOME.Shared.Config;
using AGXIOME.Shared.Models;
using Xunit;

namespace AGXIOME.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// F9 — Model Providers Tests (M-01..M-50)
// Strategy: 5 tests per provider = 250 total.
// Tests are purely structural (no network calls).
// ─────────────────────────────────────────────────────────────────────────────

// ── Helpers ───────────────────────────────────────────────────────────────────
file static class ProviderTestHelpers
{
    public static LLMWIKYConfig Cfg(LLMProviderType p, string? endpoint = null)
        => new() { Provider = p, ApiKey = "test-key", Model = "test-model", Endpoint = endpoint };
}

// ─────────────────────────────────────────────────────────────────────────────
// M-01 — Groq
// ─────────────────────────────────────────────────────────────────────────────
public class GroqProviderTests
{
    private readonly GroqProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.Groq));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Groq", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<GroqProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.Groq)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-02 — Mistral AI
// ─────────────────────────────────────────────────────────────────────────────
public class MistralAIProviderTests
{
    private readonly MistralAIProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.Mistral));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("MistralAI", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<MistralAIProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.Mistral)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-03 — xAI Grok
// ─────────────────────────────────────────────────────────────────────────────
public class XAIGrokProviderTests
{
    private readonly XAIGrokProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.XAIGrok));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("xAIGrok", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<XAIGrokProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.XAIGrok)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-04 — Cohere
// ─────────────────────────────────────────────────────────────────────────────
public class CohereProviderTests
{
    private readonly CohereProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.Cohere));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Cohere", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<CohereProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.Cohere)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-05 — Perplexity
// ─────────────────────────────────────────────────────────────────────────────
public class PerplexityProviderTests
{
    private readonly PerplexityProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.Perplexity));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Perplexity", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<PerplexityProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.Perplexity)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-06 — TogetherAI
// ─────────────────────────────────────────────────────────────────────────────
public class TogetherAIProviderTests
{
    private readonly TogetherAIProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.TogetherAI));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("TogetherAI", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<TogetherAIProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.TogetherAI)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-07 — FireworksAI
// ─────────────────────────────────────────────────────────────────────────────
public class FireworksAIProviderTests
{
    private readonly FireworksAIProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.FireworksAI));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("FireworksAI", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<FireworksAIProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.FireworksAI)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-08 — Cerebras
// ─────────────────────────────────────────────────────────────────────────────
public class CerebrasProviderTests
{
    private readonly CerebrasProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.Cerebras));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Cerebras", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<CerebrasProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.Cerebras)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-09 — AWSBedrock
// ─────────────────────────────────────────────────────────────────────────────
public class AWSBedrockProviderTests
{
    private readonly AWSBedrockProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.AWSBedrock));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("AWSBedrock", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<AWSBedrockProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.AWSBedrock)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-10 — AzureAIFoundry
// ─────────────────────────────────────────────────────────────────────────────
public class AzureAIFoundryProviderTests
{
    private readonly AzureAIFoundryProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.AzureAIFoundry, "https://test.openai.azure.com/"));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("AzureAIFoundry", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<AzureAIFoundryProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.AzureAIFoundry, "https://test.openai.azure.com/")));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-11 — IBMWatsonX
// ─────────────────────────────────────────────────────────────────────────────
public class IBMWatsonXProviderTests
{
    private readonly IBMWatsonXProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.IBMWatsonX));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("IBMWatsonX", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<IBMWatsonXProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.IBMWatsonX)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-12 — SambaNova
// ─────────────────────────────────────────────────────────────────────────────
public class SambaNovaProviderTests
{
    private readonly SambaNovaProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.SambaNova));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("SambaNova", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<SambaNovaProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.SambaNova)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-13 — OpenRouter
// ─────────────────────────────────────────────────────────────────────────────
public class OpenRouterProviderTests
{
    private readonly OpenRouterProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.OpenRouter));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("OpenRouter", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<OpenRouterProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.OpenRouter)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-14 — LiteLLM
// ─────────────────────────────────────────────────────────────────────────────
public class LiteLLMProviderTests
{
    private readonly LiteLLMProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.LiteLLM));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("LiteLLM", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<LiteLLMProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.LiteLLM)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-15 — AIMLApi
// ─────────────────────────────────────────────────────────────────────────────
public class AIMLApiProviderTests
{
    private readonly AIMLApiProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.AIMLApi));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("AIMLApi", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<AIMLApiProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.AIMLApi)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-16 — NovitaAI
// ─────────────────────────────────────────────────────────────────────────────
public class NovitaAIProviderTests
{
    private readonly NovitaAIProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.NovitaAI));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("NovitaAI", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<NovitaAIProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.NovitaAI)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-17 — DeepInfra
// ─────────────────────────────────────────────────────────────────────────────
public class DeepInfraProviderTests
{
    private readonly DeepInfraProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.DeepInfra));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("DeepInfra", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<DeepInfraProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.DeepInfra)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-18 — Replicate
// ─────────────────────────────────────────────────────────────────────────────
public class ReplicateProviderTests
{
    private readonly ReplicateProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.Replicate));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Replicate", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<ReplicateProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.Replicate)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-19 — RekaAI
// ─────────────────────────────────────────────────────────────────────────────
public class RekaAIProviderTests
{
    private readonly RekaAIProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.RekaAI));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("RekaAI", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<RekaAIProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.RekaAI)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-20 — Modal
// ─────────────────────────────────────────────────────────────────────────────
public class ModalProviderTests
{
    private readonly ModalProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.Modal));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Modal", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<ModalProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.Modal)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-21 — LlamaCpp
// ─────────────────────────────────────────────────────────────────────────────
public class LlamaCppProviderTests
{
    private readonly LlamaCppProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.LlamaCpp));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("LlamaCpp", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<LlamaCppProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.LlamaCpp)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-22 — LMStudio
// ─────────────────────────────────────────────────────────────────────────────
public class LMStudioProviderTests
{
    private readonly LMStudioProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.LMStudio));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("LMStudio", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<LMStudioProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.LMStudio)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-23 — OllamaResponses
// ─────────────────────────────────────────────────────────────────────────────
public class OllamaResponsesProviderTests
{
    private readonly OllamaResponsesProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.OllamaResponses));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("OllamaResponses", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<OllamaResponsesProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.OllamaResponses)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-24 — LocalAI
// ─────────────────────────────────────────────────────────────────────────────
public class LocalAIProviderTests
{
    private readonly LocalAIProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.LocalAI));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("LocalAI", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<LocalAIProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.LocalAI)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-25 — GPT4All
// ─────────────────────────────────────────────────────────────────────────────
public class GPT4AllProviderTests
{
    private readonly GPT4AllProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.GPT4All));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("GPT4All", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<GPT4AllProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.GPT4All)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-26 — TextGenWebUI
// ─────────────────────────────────────────────────────────────────────────────
public class TextGenWebUIProviderTests
{
    private readonly TextGenWebUIProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.TextGenWebUI));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("TextGenWebUI", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<TextGenWebUIProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.TextGenWebUI)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-27 — KoboldCPP
// ─────────────────────────────────────────────────────────────────────────────
public class KoboldCPPProviderTests
{
    private readonly KoboldCPPProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.KoboldCPP));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("KoboldCPP", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<KoboldCPPProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.KoboldCPP)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-28 — HuggingFaceInference
// ─────────────────────────────────────────────────────────────────────────────
public class HuggingFaceInferenceProviderTests
{
    private readonly HuggingFaceInferenceProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.HuggingFaceInference));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("HuggingFaceInference", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<HuggingFaceInferenceProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.HuggingFaceInference)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-29 — HuggingFaceTGI
// ─────────────────────────────────────────────────────────────────────────────
public class HuggingFaceTGIProviderTests
{
    private readonly HuggingFaceTGIProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.HuggingFaceTGI));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("HuggingFaceTGI", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<HuggingFaceTGIProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.HuggingFaceTGI)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-30 — AI21Labs
// ─────────────────────────────────────────────────────────────────────────────
public class AI21LabsProviderTests
{
    private readonly AI21LabsProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.AI21Labs));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("AI21Labs", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<AI21LabsProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.AI21Labs)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-31 — Writer
// ─────────────────────────────────────────────────────────────────────────────
public class WriterProviderTests
{
    private readonly WriterProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.Writer));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Writer", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<WriterProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.Writer)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-32 — NvidiaNIM
// ─────────────────────────────────────────────────────────────────────────────
public class NvidiaNIMProviderTests
{
    private readonly NvidiaNIMProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.NvidiaNIM));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("NvidiaNIM", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<NvidiaNIMProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.NvidiaNIM)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-33 — Anyscale
// ─────────────────────────────────────────────────────────────────────────────
public class AnyscaleProviderTests
{
    private readonly AnyscaleProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.Anyscale));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Anyscale", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<AnyscaleProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.Anyscale)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-34 — OctoAI
// ─────────────────────────────────────────────────────────────────────────────
public class OctoAIProviderTests
{
    private readonly OctoAIProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.OctoAI));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("OctoAI", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<OctoAIProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.OctoAI)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-35 — LeptonAI
// ─────────────────────────────────────────────────────────────────────────────
public class LeptonAIProviderTests
{
    private readonly LeptonAIProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.LeptonAI));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("LeptonAI", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<LeptonAIProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.LeptonAI)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-36 — FriendliAI
// ─────────────────────────────────────────────────────────────────────────────
public class FriendliAIProviderTests
{
    private readonly FriendliAIProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.FriendliAI));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("FriendliAI", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<FriendliAIProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.FriendliAI)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-37 — MoonshotKimi
// ─────────────────────────────────────────────────────────────────────────────
public class MoonshotKimiProviderTests
{
    private readonly MoonshotKimiProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.MoonshotKimi));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("MoonshotKimi", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<MoonshotKimiProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.MoonshotKimi)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-38 — ZhipuGLM
// ─────────────────────────────────────────────────────────────────────────────
public class ZhipuGLMProviderTests
{
    private readonly ZhipuGLMProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.ZhipuGLM));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("ZhipuGLM", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<ZhipuGLMProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.ZhipuGLM)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-39 — Baichuan
// ─────────────────────────────────────────────────────────────────────────────
public class BaichuanProviderTests
{
    private readonly BaichuanProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.Baichuan));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Baichuan", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<BaichuanProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.Baichuan)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-40 — ByteDanceDoubao
// ─────────────────────────────────────────────────────────────────────────────
public class ByteDanceDoubaoProviderTests
{
    private readonly ByteDanceDoubaoProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.ByteDanceDoubao));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("ByteDanceDoubao", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<ByteDanceDoubaoProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.ByteDanceDoubao)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-41 — AlibabaQwen
// ─────────────────────────────────────────────────────────────────────────────
public class AlibabaQwenProviderTests
{
    private readonly AlibabaQwenProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.AlibabaQwen));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("AlibabaQwen", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<AlibabaQwenProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.AlibabaQwen)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-42 — BaiduERNIE
// ─────────────────────────────────────────────────────────────────────────────
public class BaiduERNIEProviderTests
{
    private readonly BaiduERNIEProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.BaiduERNIE));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("BaiduERNIE", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<BaiduERNIEProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.BaiduERNIE)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-43 — TencentHunyuan
// ─────────────────────────────────────────────────────────────────────────────
public class TencentHunyuanProviderTests
{
    private readonly TencentHunyuanProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.TencentHunyuan));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("TencentHunyuan", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<TencentHunyuanProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.TencentHunyuan)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-44 — YiAI
// ─────────────────────────────────────────────────────────────────────────────
public class YiAIProviderTests
{
    private readonly YiAIProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.YiAI));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("YiAI", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<YiAIProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.YiAI)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-45 — AI21Jamba
// ─────────────────────────────────────────────────────────────────────────────
public class AI21JambaProviderTests
{
    private readonly AI21JambaProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.AI21Jamba));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("AI21Jamba", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<AI21JambaProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.AI21Jamba)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-46 — SnowflakeArctic
// ─────────────────────────────────────────────────────────────────────────────
public class SnowflakeArcticProviderTests
{
    private readonly SnowflakeArcticProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.SnowflakeArctic));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("SnowflakeArctic", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<SnowflakeArcticProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.SnowflakeArctic)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-47 — Phind
// ─────────────────────────────────────────────────────────────────────────────
public class PhindProviderTests
{
    private readonly PhindProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.Phind));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Phind", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<PhindProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.Phind)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-48 — YouCom
// ─────────────────────────────────────────────────────────────────────────────
public class YouComProviderTests
{
    private readonly YouComProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.YouCom));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("YouCom", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<YouComProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.YouCom)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-49 — Hyperbolic
// ─────────────────────────────────────────────────────────────────────────────
public class HyperbolicProviderTests
{
    private readonly HyperbolicProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.Hyperbolic));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Hyperbolic", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<HyperbolicProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.Hyperbolic)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}

// ─────────────────────────────────────────────────────────────────────────────
// M-50 — Featherless
// ─────────────────────────────────────────────────────────────────────────────
public class FeatherlessProviderTests
{
    private readonly FeatherlessProvider _sut = new(ProviderTestHelpers.Cfg(LLMProviderType.Featherless));

    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Featherless", _sut.Name);
    [Fact] public async Task Capabilities_NotNull() => Assert.NotNull(_sut.Capabilities);
    [Fact] public async Task SupportedModels_HasAtLeastOneModel() => Assert.NotEmpty(_sut.Capabilities.SupportedModels);
    [Fact] public async Task Factory_Creates_CorrectProvider() => Assert.IsType<FeatherlessProvider>(LLMProviderFactory.Create(ProviderTestHelpers.Cfg(LLMProviderType.Featherless)));
    [Fact] public async Task EmbeddingDimensions_MatchesCapabilities() => Assert.Equal(_sut.Capabilities.EmbeddingDimensions, _sut.EmbeddingDimensions);
}
