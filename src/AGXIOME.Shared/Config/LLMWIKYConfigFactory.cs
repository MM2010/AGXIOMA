using AGXIOME.Shared.Models;

namespace AGXIOME.Shared.Config;

/// <summary>
/// LLMWIKYConfigFactory — auto-detect e costruzione fluente di LLMWIKYConfig da env vars.
///
/// DIFFERENZIATORE: Swarms richiede model_name="gpt-4" + api_key=os.environ["KEY"] manualmente.
/// AGXIOMA: una riga → provider configurato automaticamente.
///
/// Usage (1 riga):
///   var config = LLMWIKYConfigFactory.FromEnv();            // auto-detect primo disponibile
///   var config = LLMWIKYConfigFactory.FromEnv("deepseek");  // forza provider specifico
///
/// Env vars riconosciute:
///   OPENAI_API_KEY, ANTHROPIC_API_KEY, GEMINI_API_KEY, DEEPSEEK_API_KEY,
///   GROQ_API_KEY, MISTRAL_API_KEY, OLLAMA_BASE_URL (no key needed),
///   AZURE_OPENAI_KEY + AZURE_OPENAI_ENDPOINT + AZURE_OPENAI_DEPLOYMENT,
///   TOGETHER_API_KEY, PERPLEXITY_API_KEY, OPENROUTER_API_KEY
/// </summary>
public static class LLMWIKYConfigFactory
{
    // ─── Priority order for auto-detect ──────────────────────────────────────
    private static readonly (string EnvVar, LLMProviderType Type, string DefaultModel, string? Endpoint)[] _providerMap =
    [
        ("DEEPSEEK_API_KEY",   LLMProviderType.DeepSeek,   "deepseek-chat",                "https://api.deepseek.com/v1"),
        ("OPENAI_API_KEY",     LLMProviderType.OpenAI,     "gpt-4o",                       null),
        ("ANTHROPIC_API_KEY",  LLMProviderType.Anthropic,  "claude-sonnet-4-20250514",      null),
        ("GEMINI_API_KEY",     LLMProviderType.Gemini,     "gemini-2.0-flash",             null),
        ("GROQ_API_KEY",       LLMProviderType.Groq,       "llama-3.3-70b-versatile",      null),
        ("MISTRAL_API_KEY",    LLMProviderType.Mistral,    "mistral-large-latest",         null),
        ("TOGETHER_API_KEY",   LLMProviderType.TogetherAI, "meta-llama/Llama-3-70b-chat-hf", "https://api.together.xyz/v1"),
        ("PERPLEXITY_API_KEY", LLMProviderType.Perplexity, "llama-3.1-sonar-huge-128k-online", "https://api.perplexity.ai"),
        ("OPENROUTER_API_KEY", LLMProviderType.OpenRouter, "openai/gpt-4o",                "https://openrouter.ai/api/v1"),
    ];

    /// <summary>
    /// Auto-detect il primo provider disponibile (API key trovata in env).
    /// Ordine di priorità: DeepSeek → OpenAI → Anthropic → Gemini → Groq → Mistral → ...
    /// Passa un hint per forzare un provider specifico.
    /// </summary>
    public static LLMWIKYConfig FromEnv(string? providerHint = null)
    {
        // Check Azure first if all 3 vars are set
        var azureKey = Env("AZURE_OPENAI_KEY");
        var azureEndpoint = Env("AZURE_OPENAI_ENDPOINT");
        var azureDeployment = Env("AZURE_OPENAI_DEPLOYMENT");
        if (azureKey is not null && azureEndpoint is not null && azureDeployment is not null)
        {
            if (providerHint is null || providerHint.Contains("azure", StringComparison.OrdinalIgnoreCase))
                return new LLMWIKYConfig
                {
                    Provider = LLMProviderType.AzureOpenAI,
                    ApiKey = azureKey,
                    Endpoint = azureEndpoint,
                    Model = azureDeployment
                };
        }

        // Check Ollama (no API key needed — just check if URL is set or default is reachable)
        var ollamaUrl = Env("OLLAMA_BASE_URL") ?? "http://localhost:11434";
        if (providerHint?.Contains("ollama", StringComparison.OrdinalIgnoreCase) == true ||
            (providerHint is null && Env("OLLAMA_BASE_URL") is not null))
        {
            return new LLMWIKYConfig
            {
                Provider = LLMProviderType.Ollama,
                Model = Env("OLLAMA_MODEL") ?? "llama3.2",
                Endpoint = ollamaUrl
            };
        }

        foreach (var (envVar, type, defaultModel, endpoint) in _providerMap)
        {
            // If a hint is given, only match providers that contain the hint string
            if (providerHint is not null &&
                !type.ToString().Contains(providerHint, StringComparison.OrdinalIgnoreCase))
                continue;

            var apiKey = Env(envVar);
            if (apiKey is null) continue;

            return new LLMWIKYConfig
            {
                Provider = type,
                ApiKey = apiKey,
                Model = Env($"{type.ToString().ToUpperInvariant()}_MODEL") ?? defaultModel,
                Endpoint = endpoint
            };
        }

        throw new InvalidOperationException(
            "No LLM provider API key found in environment variables. " +
            "Set one of: DEEPSEEK_API_KEY, OPENAI_API_KEY, ANTHROPIC_API_KEY, GEMINI_API_KEY, " +
            "GROQ_API_KEY, MISTRAL_API_KEY, OLLAMA_BASE_URL, AZURE_OPENAI_KEY, " +
            "TOGETHER_API_KEY, PERPLEXITY_API_KEY, OPENROUTER_API_KEY");
    }

    /// <summary>
    /// Returns all providers with detected API keys — useful for multi-provider routing.
    /// </summary>
    public static IReadOnlyList<LLMWIKYConfig> AllAvailableFromEnv()
    {
        var result = new List<LLMWIKYConfig>();

        foreach (var (envVar, type, defaultModel, endpoint) in _providerMap)
        {
            var apiKey = Env(envVar);
            if (apiKey is null) continue;
            result.Add(new LLMWIKYConfig
            {
                Provider = type,
                ApiKey = apiKey,
                Model = Env($"{type.ToString().ToUpperInvariant()}_MODEL") ?? defaultModel,
                Endpoint = endpoint
            });
        }

        if (Env("OLLAMA_BASE_URL") is not null)
            result.Add(new LLMWIKYConfig
            {
                Provider = LLMProviderType.Ollama,
                Model = Env("OLLAMA_MODEL") ?? "llama3.2",
                Endpoint = Env("OLLAMA_BASE_URL")
            });

        return result;
    }

    /// <summary>Try to get the best config. Returns null instead of throwing if nothing is available.</summary>
    public static LLMWIKYConfig? TryFromEnv(string? providerHint = null)
    {
        try { return FromEnv(providerHint); }
        catch { return null; }
    }

    private static string? Env(string key) =>
        Environment.GetEnvironmentVariable(key)?.Trim() is { Length: > 0 } v ? v : null;
}
