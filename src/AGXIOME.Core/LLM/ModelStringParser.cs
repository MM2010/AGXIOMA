using AGXIOME.Shared.Models;

namespace AGXIOME.Core.LLM;

// ─────────────────────────────────────────────────────────────────────────────
// ModelStringParser — CE-05
// Parses "provider:model" shorthand strings into typed (LLMProviderType, model) pairs.
// Example: "openai:gpt-4o" → (LLMProviderType.OpenAI, "gpt-4o")
// This removes the need to instantiate provider classes just to route a request.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Result of parsing a model-as-string shorthand.</summary>
public record ParsedModelString(
    LLMProviderType Provider,
    string ModelId
);

/// <summary>Parses "provider:model" shorthands into typed routing keys.
/// All known provider aliases are normalised to <see cref="LLMProviderType"/> enum values —
/// no hardcoded strings propagate beyond this class.</summary>
public static class ModelStringParser
{
    // Canonical alias map: lower-cased prefix → enum value.
    // Add new providers here; never use raw strings outside this dictionary.
    private static readonly IReadOnlyDictionary<string, LLMProviderType> _aliases =
        new Dictionary<string, LLMProviderType>(StringComparer.OrdinalIgnoreCase)
        {
            ["openai"]           = LLMProviderType.OpenAI,
            ["anthropic"]        = LLMProviderType.Anthropic,
            ["claude"]           = LLMProviderType.Anthropic,
            ["gemini"]           = LLMProviderType.Gemini,
            ["google"]           = LLMProviderType.Gemini,
            ["deepseek"]         = LLMProviderType.DeepSeek,
            ["ollama"]           = LLMProviderType.Ollama,
            ["azure"]            = LLMProviderType.AzureOpenAI,
            ["azureopenai"]      = LLMProviderType.AzureOpenAI,
            ["local"]            = LLMProviderType.Local,
            ["custom"]           = LLMProviderType.Custom,
            ["groq"]             = LLMProviderType.Groq,
            ["mistral"]          = LLMProviderType.Mistral,
            ["cohere"]           = LLMProviderType.Cohere,
            ["together"]       = LLMProviderType.TogetherAI,
            ["togetherai"]     = LLMProviderType.TogetherAI,
            ["fireworks"]      = LLMProviderType.FireworksAI,
            ["perplexity"]       = LLMProviderType.Perplexity,
            ["xai"]            = LLMProviderType.XAIGrok,
            ["grok"]           = LLMProviderType.XAIGrok,
            ["sambanova"]        = LLMProviderType.SambaNova,
            ["cerebras"]         = LLMProviderType.Cerebras,
            ["voyage"]         = LLMProviderType.Custom,  // no dedicated Voyage enum yet
            ["nvidia"]         = LLMProviderType.NvidiaNIM,
            ["triton"]         = LLMProviderType.NvidiaNIM,
            ["bedrock"]          = LLMProviderType.AWSBedrock,
            ["aws"]              = LLMProviderType.AWSBedrock,
            ["vertex"]         = LLMProviderType.AzureAIFoundry,  // proxy mapping
            ["vertexai"]       = LLMProviderType.AzureAIFoundry,
            ["hf"]             = LLMProviderType.HuggingFaceInference,
            ["huggingface"]    = LLMProviderType.HuggingFaceInference,
            ["hugging_face"]   = LLMProviderType.HuggingFaceInference,
            ["llamacpp"]         = LLMProviderType.LlamaCpp,
            ["llama_cpp"]        = LLMProviderType.LlamaCpp,
            ["lmstudio"]         = LLMProviderType.LMStudio,
            ["lm_studio"]        = LLMProviderType.LMStudio,
            ["gpt4all"]          = LLMProviderType.GPT4All,
            ["textgen"]          = LLMProviderType.TextGenWebUI,
            ["textgenwebui"]     = LLMProviderType.TextGenWebUI,
            ["kobold"]           = LLMProviderType.KoboldCPP,
            ["koboldcpp"]        = LLMProviderType.KoboldCPP,
            ["localai"]          = LLMProviderType.LocalAI,
            ["openrouter"]     = LLMProviderType.OpenRouter,
            ["novita"]         = LLMProviderType.NovitaAI,
            ["novitaai"]       = LLMProviderType.NovitaAI,
            ["featherless"]    = LLMProviderType.Featherless,
        };

    /// <summary>Parse a "provider:model" string.
    /// Throws <see cref="ArgumentException"/> for unknown providers or malformed input.</summary>
    public static ParsedModelString Parse(string modelString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelString);

        var colonIdx = modelString.IndexOf(':', StringComparison.Ordinal);
        if (colonIdx <= 0)
            throw new ArgumentException(
                $"Invalid model string '{modelString}': expected 'provider:model' format");

        var prefix    = modelString[..colonIdx].Trim();
        var modelId   = modelString[(colonIdx + 1)..].Trim();

        if (string.IsNullOrWhiteSpace(modelId))
            throw new ArgumentException(
                $"Invalid model string '{modelString}': model ID part is empty");

        if (!_aliases.TryGetValue(prefix, out var providerType))
            throw new ArgumentException(
                $"Unknown provider prefix '{prefix}' in model string '{modelString}'. " +
                $"Known prefixes: {string.Join(", ", _aliases.Keys.Order())}");

        return new ParsedModelString(providerType, modelId);
    }

    /// <summary>Try to parse without throwing. Returns null on failure.</summary>
    public static ParsedModelString? TryParse(string? modelString)
    {
        if (string.IsNullOrWhiteSpace(modelString)) return null;
        try { return Parse(modelString); }
        catch (ArgumentException) { return null; }
    }

    /// <summary>Render a typed pair back to "provider:model" canonical form.</summary>
    public static string Format(LLMProviderType provider, string modelId) =>
        $"{provider.ToString().ToLowerInvariant()}:{modelId}";

    /// <summary>All known provider alias prefixes (sorted).</summary>
    public static IReadOnlyList<string> KnownPrefixes =>
        _aliases.Keys.Order().ToList();
}
