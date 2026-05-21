using AGXIOME.Shared.Models;

namespace AGXIOME.Shared.Config;

/// <summary>LLM provider configuration used by the AGXIOME reasoning engine (LLMWIKY).
/// Serialize Provider as enum name string for config files; compare by value in code.</summary>
public record LLMWIKYConfig
{
    /// <summary>Typed provider — avoids hardcoded strings in routing.
    /// Serialize as enum name string for config files; compare by value in code.</summary>
    public LLMProviderType Provider { get; init; } = LLMProviderType.OpenAI;
    public string Model { get; init; } = "gpt-4o";
    public string? ApiKey { get; init; }
    public string? Endpoint { get; init; }
    public string? ApiVersion { get; init; }
    public string? OrganizationId { get; init; }
    public double DefaultTemperature { get; init; } = 0.7;
    public int MaxTokens { get; init; } = 4096;
    public int EmbeddingDimensions { get; init; } = 1536;
    public int MaxRetries { get; init; } = 3;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(60);
    public Dictionary<string, object> ProviderOptions { get; init; } = new();
    public bool EnableAdaptiveRateLimit { get; init; } = true;
    public int MaxTokensPerMinute { get; init; } = 100_000;
    public int MaxRequestsPerMinute { get; init; } = 500;
    public List<LLMWIKYConfig>? FallbackProviders { get; init; }
    public bool EnableCostTracking { get; init; } = true;
}
