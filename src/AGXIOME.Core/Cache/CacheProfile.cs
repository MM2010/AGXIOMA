using AGXIOME.Shared.Models;

namespace AGXIOME.Core.Cache;

/// <summary>
/// Provider-aware cache profile. Defines how aggressively to optimise
/// prompts for a given LLM provider's caching characteristics.
///
/// Usage — inject via DI:
///   builder.Services.AddSingleton(CacheProfile.DeepSeek);
///   // or load from appsettings
///   builder.Services.Configure&lt;AgentMemoryConfig&gt;(config.GetSection("Agent:Memory"));
/// </summary>
public sealed record CacheProfile
{
    /// <summary>Provider name (deepseek, openai, anthropic, etc.).</summary>
    public string Provider         { get; init; } = "";

    /// <summary>Max token window the provider caches. After this delta, cold start.</summary>
    public int    CacheWindowSize  { get; init; } = 4096;

    /// <summary>Max frozen prefix tokens (system + agent definition).</summary>
    public int    MaxPrefixTokens  { get; init; } = 9000;

    /// <summary>Max variable tail tokens (recent messages + query).</summary>
    public int    MaxTailTokens    { get; init; } = 4000;

    /// <summary>Threshold before compression kicks in (fraction of budget).</summary>
    public double CompressThreshold { get; init; } = 0.75;

    /// <summary>How many recent messages to keep verbatim before summarising.</summary>
    public int    MaxRecentMessages { get; init; } = 3;

    /// <summary>Enable content-addressed pointer cache for this provider.</summary>
    public bool   UsePointerCache { get; init; } = true;

    // ── Well-known profiles ─────────────────────────────────────────

    public static readonly CacheProfile DeepSeek = new()
    {
        Provider          = "deepseek",
        CacheWindowSize   = 4096,
        MaxPrefixTokens   = 9000,
        MaxTailTokens     = 4000,
        CompressThreshold = 0.75,
        MaxRecentMessages = 3,
        UsePointerCache   = true,
    };

    public static readonly CacheProfile OpenAI = new()
    {
        Provider          = "openai",
        CacheWindowSize   = 1024,
        MaxPrefixTokens   = 9000,
        MaxTailTokens     = 1000,
        CompressThreshold = 0.70,
        MaxRecentMessages = 2,            // narrower window → fewer recent msgs
        UsePointerCache   = true,
    };

    public static readonly CacheProfile Anthropic = new()
    {
        Provider          = "anthropic",
        CacheWindowSize   = 2048,
        MaxPrefixTokens   = 9000,
        MaxTailTokens     = 2000,
        CompressThreshold = 0.70,
        MaxRecentMessages = 2,
        UsePointerCache   = true,
    };

    /// <summary>
    /// Picks the right profile from the provider name string.
    /// Falls back to DeepSeek profile.
    /// </summary>
    public static CacheProfile FromProvider(string provider) => provider.ToLowerInvariant() switch
    {
        "deepseek"  => DeepSeek,
        "openai"    => OpenAI,
        "anthropic" => Anthropic,
        _           => DeepSeek,
    };
}
