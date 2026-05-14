using System.Net;
using System.Net.Http.Headers;
using AGXIOME.Shared.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AGXIOME.LLMWIKY.Http;

// ── Per-provider HttpClient profile ──────────────────────────────────────────

/// <summary>
/// All tunable parameters for a single provider's HttpClient.
/// Validated, production-safe defaults — override only what needs changing.
/// </summary>
public sealed record AgxiomeHttpClientProfile
{
    /// <summary>Base URL — required. Trailing slash is normalized internally.</summary>
    public string  BaseAddress              { get; init; } = "";

    /// <summary>End-to-end request timeout (connect + wait + read).</summary>
    public TimeSpan Timeout                 { get; init; } = TimeSpan.FromSeconds(120);

    /// <summary>Max simultaneous TCP connections to this host.</summary>
    public int     MaxConnectionsPerServer  { get; init; } = 10;

    /// <summary>
    /// How long a pooled connection lives before being recycled.
    /// 2 min avoids stale DNS while keeping connection reuse benefits.
    /// </summary>
    public TimeSpan PooledConnectionLifetime { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>Enable HTTP/2 multiplexing when the server supports it.</summary>
    public bool    EnableHttp2              { get; init; } = true;

    /// <summary>Request Accept-Encoding: gzip, br — most APIs return compressed responses.</summary>
    public bool    EnableCompression        { get; init; } = true;

    /// <summary>User-Agent header sent with every request.</summary>
    public string  UserAgent               { get; init; } = "Agxioma/1.0";
}

// ── Per-provider default profiles ────────────────────────────────────────────

/// <summary>
/// Hardened, provider-specific HttpClient profiles.
/// Every value is derived from the provider's actual SLA and payload characteristics.
/// </summary>
public static class AgxiomeHttpClientDefaults
{
    // ── Cloud API providers ── //

    /// <summary>OpenAI. Large payloads, gzip enabled, HTTP/2 supported.</summary>
    public static readonly AgxiomeHttpClientProfile OpenAI = new()
    {
        BaseAddress             = "https://api.openai.com/v1/",
        Timeout                 = TimeSpan.FromSeconds(120),
        MaxConnectionsPerServer = 20,
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        EnableHttp2             = true,
        EnableCompression       = true,
    };

    /// <summary>DeepSeek. Identical wire format to OpenAI.</summary>
    public static readonly AgxiomeHttpClientProfile DeepSeek = new()
    {
        BaseAddress             = "https://api.deepseek.com/v1/",
        Timeout                 = TimeSpan.FromSeconds(180),   // reasoning models are slow
        MaxConnectionsPerServer = 20,
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        EnableHttp2             = true,
        EnableCompression       = true,
    };

    /// <summary>Anthropic. No streaming compression in Claude API; HTTP/2 yes.</summary>
    public static readonly AgxiomeHttpClientProfile Anthropic = new()
    {
        BaseAddress             = "https://api.anthropic.com/v1/",
        Timeout                 = TimeSpan.FromSeconds(180),
        MaxConnectionsPerServer = 10,
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        EnableHttp2             = true,
        EnableCompression       = true,
    };

    /// <summary>Gemini. Google infra, HTTP/2, short pooling to catch regional failover.</summary>
    public static readonly AgxiomeHttpClientProfile Gemini = new()
    {
        BaseAddress             = "https://generativelanguage.googleapis.com/",
        Timeout                 = TimeSpan.FromSeconds(120),
        MaxConnectionsPerServer = 10,
        PooledConnectionLifetime = TimeSpan.FromMinutes(1),
        EnableHttp2             = true,
        EnableCompression       = true,
    };

    /// <summary>Groq. Ultra-low latency; higher MaxConnections to exploit it.</summary>
    public static readonly AgxiomeHttpClientProfile Groq = new()
    {
        BaseAddress             = "https://api.groq.com/openai/v1/",
        Timeout                 = TimeSpan.FromSeconds(60),
        MaxConnectionsPerServer = 30,
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        EnableHttp2             = true,
        EnableCompression       = true,
    };

    /// <summary>Mistral. Standard cloud latency.</summary>
    public static readonly AgxiomeHttpClientProfile Mistral = new()
    {
        BaseAddress             = "https://api.mistral.ai/v1/",
        Timeout                 = TimeSpan.FromSeconds(120),
        MaxConnectionsPerServer = 10,
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        EnableHttp2             = true,
        EnableCompression       = true,
    };

    /// <summary>XAI / Grok. OpenAI-compatible.</summary>
    public static readonly AgxiomeHttpClientProfile XAIGrok = new()
    {
        BaseAddress             = "https://api.x.ai/v1/",
        Timeout                 = TimeSpan.FromSeconds(120),
        MaxConnectionsPerServer = 10,
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        EnableHttp2             = true,
        EnableCompression       = true,
    };

    /// <summary>Cohere. Standard cloud.</summary>
    public static readonly AgxiomeHttpClientProfile Cohere = new()
    {
        BaseAddress             = "https://api.cohere.com/v2/",
        Timeout                 = TimeSpan.FromSeconds(120),
        MaxConnectionsPerServer = 10,
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        EnableHttp2             = false,
        EnableCompression       = true,
    };

    /// <summary>
    /// Ollama. Local GPU process — no TLS, low connection limit, long timeout
    /// (large model generation can take many seconds on CPU fallback).
    /// </summary>
    public static readonly AgxiomeHttpClientProfile Ollama = new()
    {
        BaseAddress             = "http://localhost:11434/",
        Timeout                 = TimeSpan.FromMinutes(10),
        MaxConnectionsPerServer = 4,    // single GPU process; more connections = queue not speed
        PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        EnableHttp2             = false, // Ollama HTTP/1.1 only
        EnableCompression       = false, // local; compression overhead not worth it
    };

    /// <summary>Generic fallback for unknown/custom providers.</summary>
    public static readonly AgxiomeHttpClientProfile Default = new()
    {
        BaseAddress             = "",
        Timeout                 = TimeSpan.FromSeconds(120),
        MaxConnectionsPerServer = 10,
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        EnableHttp2             = true,
        EnableCompression       = true,
    };

    // ── Naming convention ────────────────────────────────────────────────────

    /// <summary>
    /// Named client key used with IHttpClientFactory.
    /// Format: <c>agxiome:{providerType}</c>  e.g. "agxiome:DeepSeek".
    /// </summary>
    public static string ClientName(LLMProviderType provider) => $"agxiome:{provider}";

    /// <summary>Resolve the default profile for a known provider, or <see cref="Default"/>.</summary>
    public static AgxiomeHttpClientProfile For(LLMProviderType provider) => provider switch
    {
        LLMProviderType.OpenAI     => OpenAI,
        LLMProviderType.DeepSeek   => DeepSeek,
        LLMProviderType.Custom     => DeepSeek,   // usually OpenAI-compatible override
        LLMProviderType.Anthropic  => Anthropic,
        LLMProviderType.Gemini     => Gemini,
        LLMProviderType.Groq       => Groq,
        LLMProviderType.Mistral    => Mistral,
        LLMProviderType.XAIGrok    => XAIGrok,
        LLMProviderType.Cohere     => Cohere,
        LLMProviderType.Ollama
          or LLMProviderType.OllamaResponses => Ollama,
        _                          => Default,
    };
}

// ── IServiceCollection extensions ────────────────────────────────────────────

/// <summary>
/// Extension methods to register production-grade named HttpClients for all
/// AGXIOMA LLM providers. Call once from your <c>Program.cs</c>:
///
/// <code>
/// builder.Services.AddAgxiomeHttpClients();
/// // or with per-provider overrides:
/// builder.Services.AddAgxiomeHttpClients(opts =>
///     opts.Override(LLMProviderType.Ollama, p => p with { BaseAddress = "http://gpu-server:11434/" }));
/// </code>
/// </summary>
public static class AgxiomeHttpClientExtensions
{
    public static IServiceCollection AddAgxiomeHttpClients(
        this IServiceCollection services,
        Action<AgxiomeHttpClientRegistrationOptions>? configure = null)
    {
        var opts = new AgxiomeHttpClientRegistrationOptions();
        configure?.Invoke(opts);

        // Register a named client for every provider that has a default profile.
        // Providers without a base-URL (generic "Default") are skipped — they get
        // a fallback client when their overridden base address is supplied at runtime.
        var providers = Enum.GetValues<LLMProviderType>();
        foreach (var provider in providers)
        {
            var profile = opts.GetProfile(provider) ?? AgxiomeHttpClientDefaults.For(provider);

            // Skip if no base address is set (will fall back to per-request config)
            if (string.IsNullOrWhiteSpace(profile.BaseAddress)) continue;

            var name = AgxiomeHttpClientDefaults.ClientName(provider);
            services.AddHttpClient(name)
                    .ConfigurePrimaryHttpMessageHandler(() => BuildHandler(profile))
                    .ConfigureHttpClient(client => ConfigureClient(client, profile));
        }

        return services;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Build an optimized SocketsHttpHandler for the given profile.</summary>
    internal static SocketsHttpHandler BuildHandler(AgxiomeHttpClientProfile p) =>
        new()
        {
            PooledConnectionLifetime      = p.PooledConnectionLifetime,
            MaxConnectionsPerServer       = p.MaxConnectionsPerServer,
            EnableMultipleHttp2Connections = p.EnableHttp2,
            AutomaticDecompression        = p.EnableCompression
                ? DecompressionMethods.GZip | DecompressionMethods.Brotli
                : DecompressionMethods.None,
            // Keep-alive is on by default — keeps TCP connections warm
        };

    /// <summary>Apply base address, timeout, and user-agent to the named client.</summary>
    internal static void ConfigureClient(HttpClient client, AgxiomeHttpClientProfile p)
    {
        if (!string.IsNullOrWhiteSpace(p.BaseAddress))
        {
            // Ensure trailing slash so relative paths work correctly with BaseAddress
            var url = p.BaseAddress.TrimEnd('/') + '/';
            client.BaseAddress = new Uri(url);
        }

        client.Timeout = p.Timeout;
        client.DefaultRequestHeaders.UserAgent.ParseAdd(p.UserAgent);
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }
}

// ── Registration options ──────────────────────────────────────────────────────

/// <summary>
/// Allows selective profile overrides without touching the defaults.
/// Only providers you explicitly override are changed.
/// </summary>
public sealed class AgxiomeHttpClientRegistrationOptions
{
    private readonly Dictionary<LLMProviderType, AgxiomeHttpClientProfile> _overrides = new();

    /// <summary>Override the default profile for one provider.</summary>
    public AgxiomeHttpClientRegistrationOptions Override(
        LLMProviderType provider,
        Func<AgxiomeHttpClientProfile, AgxiomeHttpClientProfile> mutate)
    {
        var current = _overrides.TryGetValue(provider, out var o)
            ? o
            : AgxiomeHttpClientDefaults.For(provider);
        _overrides[provider] = mutate(current);
        return this;
    }

    /// <summary>Resolve the effective profile for a provider (override ?? default).</summary>
    internal AgxiomeHttpClientProfile? GetProfile(LLMProviderType provider) =>
        _overrides.TryGetValue(provider, out var o) ? o : null;
}
