using AGXIOME.Shared.Models;

namespace AGXIOME.Core.LLM;

// ─────────────────────────────────────────────────────────────────────────────
// ModelCompatibilityMap — CE-08
// Maps model identifiers to their canonical provider and lifecycle status.
// All status values use the ModelCompatibilityStatus enum — no magic strings.
// Downstream code can query this map before sending a request to surface
// deprecation warnings without hard-coding model names in application logic.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Metadata for a single model entry in the compatibility map.</summary>
public record ModelEntry(
    LLMProviderType Provider,
    ModelCompatibilityStatus Status,
    string? ReplacedBy = null,
    string? DeprecationNote = null,
    DateTimeOffset? DeprecatedSince = null
);

/// <summary>Result of a compatibility check.</summary>
public record CompatibilityCheckResult(
    string ModelId,
    LLMProviderType Provider,
    ModelCompatibilityStatus Status,
    bool HasWarning,
    string? WarningMessage
);

/// <summary>Model-to-provider compatibility registry.
/// Enum-driven: all providers and statuses reference <see cref="LLMProviderType"/>
/// and <see cref="ModelCompatibilityStatus"/> — no hardcoded string comparisons.</summary>
public sealed class ModelCompatibilityMap
{
    // Initial well-known models. Extend via RegisterModel() at startup for custom deployments.
    private readonly Dictionary<string, ModelEntry> _entries = new(StringComparer.OrdinalIgnoreCase)
    {
        // ── OpenAI ────────────────────────────────────────────────────────────
        ["gpt-4o"]                     = new(LLMProviderType.OpenAI,   ModelCompatibilityStatus.Supported),
        ["gpt-4o-mini"]                = new(LLMProviderType.OpenAI,   ModelCompatibilityStatus.Supported),
        ["o1"]                         = new(LLMProviderType.OpenAI,   ModelCompatibilityStatus.Supported),
        ["o1-mini"]                    = new(LLMProviderType.OpenAI,   ModelCompatibilityStatus.Supported),
        ["o3"]                         = new(LLMProviderType.OpenAI,   ModelCompatibilityStatus.Preview),
        ["o3-mini"]                    = new(LLMProviderType.OpenAI,   ModelCompatibilityStatus.Supported),
        ["gpt-4-turbo"]                = new(LLMProviderType.OpenAI,   ModelCompatibilityStatus.Deprecated,
                                            ReplacedBy: "gpt-4o",
                                            DeprecationNote: "Use gpt-4o for lower cost and higher quality"),
        ["gpt-3.5-turbo"]              = new(LLMProviderType.OpenAI,   ModelCompatibilityStatus.Deprecated,
                                            ReplacedBy: "gpt-4o-mini"),
        ["text-davinci-003"]           = new(LLMProviderType.OpenAI,   ModelCompatibilityStatus.Removed,
                                            ReplacedBy: "gpt-4o-mini"),
        // ── Anthropic ─────────────────────────────────────────────────────────
        ["claude-3-5-sonnet-20241022"] = new(LLMProviderType.Anthropic, ModelCompatibilityStatus.Supported),
        ["claude-3-5-haiku-20241022"]  = new(LLMProviderType.Anthropic, ModelCompatibilityStatus.Supported),
        ["claude-3-opus-20240229"]     = new(LLMProviderType.Anthropic, ModelCompatibilityStatus.Supported),
        ["claude-3-sonnet-20240229"]   = new(LLMProviderType.Anthropic, ModelCompatibilityStatus.Deprecated,
                                            ReplacedBy: "claude-3-5-sonnet-20241022"),
        ["claude-2.1"]                 = new(LLMProviderType.Anthropic, ModelCompatibilityStatus.Deprecated,
                                            ReplacedBy: "claude-3-5-haiku-20241022"),
        // ── Google ────────────────────────────────────────────────────────────
        ["gemini-2.0-flash"]           = new(LLMProviderType.Gemini,   ModelCompatibilityStatus.Supported),
        ["gemini-1.5-pro"]             = new(LLMProviderType.Gemini,   ModelCompatibilityStatus.Supported),
        ["gemini-1.5-flash"]           = new(LLMProviderType.Gemini,   ModelCompatibilityStatus.Supported),
        ["gemini-pro"]                 = new(LLMProviderType.Gemini,   ModelCompatibilityStatus.Deprecated,
                                            ReplacedBy: "gemini-1.5-pro"),
        // ── DeepSeek ──────────────────────────────────────────────────────────
        ["deepseek-chat"]              = new(LLMProviderType.DeepSeek, ModelCompatibilityStatus.Supported),
        ["deepseek-reasoner"]          = new(LLMProviderType.DeepSeek, ModelCompatibilityStatus.Supported),
        // ── Mistral ───────────────────────────────────────────────────────────
        ["mistral-large-latest"]       = new(LLMProviderType.Mistral,  ModelCompatibilityStatus.Supported),
        ["mistral-small-latest"]       = new(LLMProviderType.Mistral,  ModelCompatibilityStatus.Supported),
        ["mixtral-8x7b-instruct"]      = new(LLMProviderType.Mistral,  ModelCompatibilityStatus.Deprecated,
                                            ReplacedBy: "mistral-large-latest"),
        // ── Groq ──────────────────────────────────────────────────────────────
        ["llama-3.3-70b-versatile"]    = new(LLMProviderType.Groq,     ModelCompatibilityStatus.Supported),
        ["llama-3.1-8b-instant"]       = new(LLMProviderType.Groq,     ModelCompatibilityStatus.Supported),
        // ── Local / Ollama ────────────────────────────────────────────────────
        ["llama3"]                     = new(LLMProviderType.Ollama,   ModelCompatibilityStatus.Supported),
        ["llama3.1"]                   = new(LLMProviderType.Ollama,   ModelCompatibilityStatus.Supported),
        ["mistral"]                    = new(LLMProviderType.Ollama,   ModelCompatibilityStatus.Supported),
        ["nomic-embed-text"]           = new(LLMProviderType.Ollama,   ModelCompatibilityStatus.Supported),
    };

    /// <summary>Register or update an entry. Use to add custom/private models at startup.</summary>
    public void RegisterModel(string modelId, ModelEntry entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentNullException.ThrowIfNull(entry);
        _entries[modelId] = entry;
    }

    /// <summary>Check compatibility and build a result with optional warning message.</summary>
    public CompatibilityCheckResult Check(string modelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

        if (!_entries.TryGetValue(modelId, out var entry))
        {
            // Unknown model — warn but don't block. Unknown provider = Custom.
            return new CompatibilityCheckResult(
                modelId,
                LLMProviderType.Custom,
                ModelCompatibilityStatus.Supported,
                HasWarning: true,
                WarningMessage: $"Model '{modelId}' is not in the compatibility map. " +
                                "Assuming supported — add it via RegisterModel() to suppress this warning.");
        }

        var (hasWarning, msg) = entry.Status switch
        {
            ModelCompatibilityStatus.Deprecated =>
                (true, BuildDeprecationMessage(modelId, entry)),
            ModelCompatibilityStatus.Removed =>
                (true, $"Model '{modelId}' has been REMOVED. " +
                        (entry.ReplacedBy is null ? "" : $"Migrate to '{entry.ReplacedBy}'.")),
            ModelCompatibilityStatus.Preview =>
                (true, $"Model '{modelId}' is in PREVIEW and may change without notice."),
            _ => (false, (string?)null),
        };

        return new CompatibilityCheckResult(modelId, entry.Provider, entry.Status, hasWarning, msg);
    }

    /// <summary>Look up the canonical provider for a model ID. Returns null if unknown.</summary>
    public LLMProviderType? GetProvider(string modelId) =>
        _entries.TryGetValue(modelId, out var e) ? e.Provider : null;

    /// <summary>All registered models grouped by provider.</summary>
    public IReadOnlyDictionary<LLMProviderType, IReadOnlyList<string>> GetByProvider()
    {
        return _entries
            .GroupBy(kv => kv.Value.Provider)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g.Select(kv => kv.Key).OrderBy(m => m).ToList());
    }

    /// <summary>All models with a given lifecycle status.</summary>
    public IReadOnlyList<string> GetByStatus(ModelCompatibilityStatus status) =>
        _entries
            .Where(kv => kv.Value.Status == status)
            .Select(kv => kv.Key)
            .OrderBy(m => m)
            .ToList();

    private static string BuildDeprecationMessage(string modelId, ModelEntry entry)
    {
        var msg = $"Model '{modelId}' is DEPRECATED.";
        if (entry.ReplacedBy is not null)
            msg += $" Replace with '{entry.ReplacedBy}'.";
        if (entry.DeprecationNote is not null)
            msg += $" {entry.DeprecationNote}";
        return msg;
    }

    // ── Static convenience helpers (CE-05 integration) ────────────────────────

    private static readonly ModelCompatibilityMap _default = new();

    /// <summary>
    /// Parse a model string into (provider, modelId). Supports two formats:
    /// <list type="bullet">
    ///   <item><c>"openai:gpt-4o"</c> — explicit provider prefix.</item>
    ///   <item><c>"gpt-4o"</c> — bare model name; provider inferred by model-map lookup,
    ///   then by common name prefixes.</item>
    /// </list>
    /// </summary>
    public static (LLMProviderType Provider, string ModelId) ParseProviderModel(string providerModel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerModel);

        // 1. Try "provider:model" colon format via ModelStringParser
        var parsed = ModelStringParser.TryParse(providerModel);
        if (parsed is not null)
            return (parsed.Provider, parsed.ModelId);

        // 2. Look up bare model name in the compatibility map
        if (_default._entries.TryGetValue(providerModel, out var entry))
            return (entry.Provider, providerModel);

        // 3. Infer from well-known model name prefixes (fallback for unlisted models)
        var lower = providerModel.ToLowerInvariant();
        if (lower.StartsWith("gpt") || lower.StartsWith("o1") || lower.StartsWith("o3"))
            return (LLMProviderType.OpenAI, providerModel);
        if (lower.StartsWith("claude"))
            return (LLMProviderType.Anthropic, providerModel);
        if (lower.StartsWith("gemini"))
            return (LLMProviderType.Gemini, providerModel);
        if (lower.StartsWith("deepseek"))
            return (LLMProviderType.DeepSeek, providerModel);
        if (lower.StartsWith("mistral-large") || lower.StartsWith("mistral-small"))
            return (LLMProviderType.Mistral, providerModel);
        if (lower.StartsWith("llama") || lower.StartsWith("phi") || lower.StartsWith("qwen"))
            return (LLMProviderType.Ollama, providerModel);

        return (LLMProviderType.Custom, providerModel);
    }

    /// <summary>
    /// Resolve the API key for a provider from well-known environment variables.
    /// Returns null if the environment variable is unset.
    /// </summary>
    public static string? ResolveApiKey(LLMProviderType provider) => provider switch
    {
        LLMProviderType.OpenAI           => GetEnv("OPENAI_API_KEY"),
        LLMProviderType.Anthropic        => GetEnv("ANTHROPIC_API_KEY"),
        LLMProviderType.Gemini           => GetEnv("GEMINI_API_KEY"),
        LLMProviderType.DeepSeek         => GetEnv("DEEPSEEK_API_KEY"),
        LLMProviderType.Groq             => GetEnv("GROQ_API_KEY"),
        LLMProviderType.Mistral          => GetEnv("MISTRAL_API_KEY"),
        LLMProviderType.Cohere           => GetEnv("COHERE_API_KEY"),
        LLMProviderType.AWSBedrock       => GetEnv("AWS_ACCESS_KEY_ID"),
        LLMProviderType.AzureOpenAI      => GetEnv("AZURE_OPENAI_API_KEY"),
        LLMProviderType.AzureAIFoundry   => GetEnv("AZURE_AI_API_KEY"),
        LLMProviderType.XAIGrok          => GetEnv("XAI_API_KEY"),
        LLMProviderType.TogetherAI       => GetEnv("TOGETHER_API_KEY"),
        LLMProviderType.FireworksAI      => GetEnv("FIREWORKS_API_KEY"),
        LLMProviderType.Perplexity       => GetEnv("PERPLEXITY_API_KEY"),
        LLMProviderType.HuggingFaceInference => GetEnv("HUGGINGFACE_API_KEY"),
        LLMProviderType.OpenRouter       => GetEnv("OPENROUTER_API_KEY"),
        LLMProviderType.Cerebras         => GetEnv("CEREBRAS_API_KEY"),
        LLMProviderType.SambaNova        => GetEnv("SAMBANOVA_API_KEY"),
        LLMProviderType.NvidiaNIM        => GetEnv("NVIDIA_API_KEY"),
        _ => null
    };

    private static string? GetEnv(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v : null;
}
