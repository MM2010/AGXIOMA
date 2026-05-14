using System.Collections.Concurrent;
using AGXIOME.Core;
using AGXIOME.Core.Agent;
using AGXIOME.Core.Patterns;
using AGXIOME.Core.Routing;
using AGXIOME.LLMWIKY;
using AGXIOME.LLMWIKY.Http;
using AGXIOME.Shared.Config;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AGXIOME.Core.DependencyInjection;

/// <summary>
/// AGXIOMA Dependency Injection Extensions for ASP.NET Core.
/// ZERO EQUIVALENT in Swarms (Python, no native DI).
///
/// Usage:
///   services.AddAgxiome(options => {
///       options.UseDeepSeek(apiKey);
///       options.AddAgent("researcher", cfg => cfg.WithSystemPrompt("Research topics."));
///       options.AddAgent("writer",     cfg => cfg.WithMaxIterations(16));
///   });
///
///   // In controller:
///   [Inject] private IAgxiomeAgentFactory _factory;
///   var agent = _factory.GetAgent("researcher");
/// </summary>
public static class AgxiomeServiceCollectionExtensions
{
    public static AgxiomeServiceBuilder AddAgxiome(
        this IServiceCollection services,
        Action<AgxiomeDIOptions>? configure = null)
    {
        var options = new AgxiomeDIOptions();
        configure?.Invoke(options);

        // Register named HttpClients for all LLM providers (pooled, optimized per-provider settings).
        // Must be called before registering LLMProviderFactory so the factory can resolve IHttpClientFactory.
        services.AddAgxiomeHttpClients();

        // Register core engine config
        services.AddSingleton(options.DefaultLLMConfig);

        // LLMProviderFactory: uses IHttpClientFactory when available (injected via DI).
        services.AddSingleton<ILLMProviderFactory>(sp =>
        {
            var hcf = sp.GetService<IHttpClientFactory>();
            return hcf is not null ? new LLMProviderFactory(hcf) : new LLMProviderFactory();
        });
        // Keep the legacy Func<LLMWIKYConfig, ILLMProvider> delegate for code that uses it directly.
        services.AddSingleton<Func<LLMWIKYConfig, ILLMProvider>>(sp =>
            sp.GetRequiredService<ILLMProviderFactory>().CreateProvider);

        // Register agent factory
        services.AddSingleton<IAgxiomeAgentFactory>(sp =>
        {
            var loggerFactory = sp.GetService<ILoggerFactory>();
            if (loggerFactory is not null) AgentFactory.Configure(loggerFactory);
            return new AgxiomeAgentFactory(options);
        });

        // Register named agents
        foreach (var (name, _) in options.AgentDefinitions)
        {
            services.AddTransient(sp =>
            {
                var factory = sp.GetRequiredService<IAgxiomeAgentFactory>();
                return factory.GetAgent(name);
            });
        }

        // Register swarm patterns
        services.AddTransient<MixtureOfAgentsSwarm>(sp =>
        {
            var factory = sp.GetRequiredService<IAgxiomeAgentFactory>();
            return SwarmFactory.MixtureOfAgents(options.AgentDefinitions.Keys
                .Select(n => factory.GetAgent(n)).ToList());
        });

        services.AddTransient<AgxiomeModelRouter>(_ =>
            new AgxiomeModelRouter(options.RouterEntries,
                new ModelRouterConfig { DefaultStrategy = options.DefaultRoutingStrategy }));

        return new AgxiomeServiceBuilder(services);
    }
}

// ─── Options ────────────────────────────────────────────────────────────────

public sealed class AgxiomeDIOptions
{
    internal LLMWIKYConfig DefaultLLMConfig { get; private set; } = new();
    internal Dictionary<string, Action<AgxiomeBuilder.AgentBuilder>> AgentDefinitions { get; } = new();
    internal List<RouterEntry> RouterEntries { get; } = new();
    internal RoutingStrategy DefaultRoutingStrategy { get; private set; } = RoutingStrategy.Hybrid;

    public AgxiomeDIOptions UseDeepSeek(string? apiKey = null, string model = "deepseek-chat")
    {
        DefaultLLMConfig = new LLMWIKYConfig
        {
            Provider = LLMProviderType.DeepSeek,
            ApiKey = apiKey ?? Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY"),
            Model = model,
            Endpoint = "https://api.deepseek.com/v1"
        };
        return this;
    }

    public AgxiomeDIOptions UseOpenAI(string? apiKey = null, string model = "gpt-4o")
    {
        DefaultLLMConfig = new LLMWIKYConfig
        {
            Provider = LLMProviderType.OpenAI,
            ApiKey = apiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
            Model = model
        };
        return this;
    }

    public AgxiomeDIOptions UseAnthropic(string? apiKey = null, string model = "claude-sonnet-4-20250514")
    {
        DefaultLLMConfig = new LLMWIKYConfig
        {
            Provider = LLMProviderType.Anthropic,
            ApiKey = apiKey ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"),
            Model = model
        };
        return this;
    }

    public AgxiomeDIOptions AddAgent(string name, Action<AgxiomeBuilder.AgentBuilder>? configure = null)
    {
        AgentDefinitions[name] = configure ?? (_ => { });
        return this;
    }

    public AgxiomeDIOptions WithRoutingStrategy(RoutingStrategy strategy)
    {
        DefaultRoutingStrategy = strategy;
        return this;
    }

    public AgxiomeDIOptions AddRouterEntry(RouterEntry entry)
    {
        RouterEntries.Add(entry);
        return this;
    }

    /// <summary>
    /// Set the default LLM provider using explicit values.
    /// Replaces <see cref="UseDeepSeek"/>, <see cref="UseOpenAI"/>, <see cref="UseAnthropic"/> overloads
    /// when the provider type is resolved at runtime.
    /// </summary>
    public AgxiomeDIOptions UseModel(LLMProviderType provider, string? model = null, string? apiKey = null)
    {
        var (_, defaultModel, defaultEndpoint) = AgxiomeProviderResolver.Defaults(provider);
        DefaultLLMConfig = new LLMWIKYConfig
        {
            Provider = provider,
            Model    = model ?? defaultModel,
            ApiKey   = apiKey,
            Endpoint = defaultEndpoint
        };
        return this;
    }

    /// <summary>
    /// Reads <c>Agent:Provider</c> and <c>Agent:Model</c> from <paramref name="config"/>,
    /// resolves the matching API key from env-vars / config, and calls <see cref="UseModel"/>.
    /// This replaces the ~10-line provider-switch + apiKey-switch boilerplate in every Program.cs.
    /// </summary>
    /// <example>
    /// services.AddAgxiome(options => options
    ///     .UseModelFromConfig(builder.Configuration)
    ///     .AddAgent("assistant"));
    /// </example>
    public AgxiomeDIOptions UseModelFromConfig(IConfiguration config)
    {
        var providerName = config["Agent:Provider"] ?? "openai";
        var model        = config["Agent:Model"];
        var (provider, defaultModel, _) = AgxiomeProviderResolver.Resolve(providerName, config);
        return UseModel(provider, model ?? defaultModel,
            AgxiomeProviderResolver.ResolveApiKey(provider, config));
    }
}

// ─── Agent Factory Service ────────────────────────────────────────────────────

public interface IAgxiomeAgentFactory
{
    IAgxiomeAgent GetAgent(string name);
    IAgxiomeAgent CreateAnonymous(Action<AgxiomeBuilder.AgentBuilder> configure);
}

internal sealed class AgxiomeAgentFactory(AgxiomeDIOptions options)
    : IAgxiomeAgentFactory
{
    private readonly ConcurrentDictionary<string, IAgxiomeAgent> _cache = new();

    public IAgxiomeAgent GetAgent(string name)
    {
        return _cache.GetOrAdd(name, n =>
        {
            if (!options.AgentDefinitions.TryGetValue(n, out var configure))
                throw new InvalidOperationException($"Agent '{n}' not registered. Call AddAgent(\"{n}\") in AddAgxiome().");

            var builder = AgxiomeBuilder.Agent(n);
            configure(builder);
            return builder.Build();
        });
    }

    public IAgxiomeAgent CreateAnonymous(Action<AgxiomeBuilder.AgentBuilder> configure)
    {
        var builder = AgxiomeBuilder.Agent(Guid.NewGuid().ToString("N")[..8]);
        configure(builder);
        return builder.Build();
    }
}

// ─── Provider resolver ────────────────────────────────────────────────────────

/// <summary>
/// Centralizes the provider-name → <see cref="LLMProviderType"/> mapping and API-key resolution
/// that was previously duplicated as a switch in every app's Program.cs.
/// </summary>
public static class AgxiomeProviderResolver
{
    /// <summary>
    /// Maps a lower-case provider name (as stored in <c>appsettings.json Agent:Provider</c>) to
    /// its <see cref="LLMProviderType"/>, default model, and canonical API endpoint.
    /// </summary>
    public static (LLMProviderType Provider, string DefaultModel, string? DefaultEndpoint) Resolve(
        string providerName, IConfiguration? config = null)
        => providerName.Trim().ToLowerInvariant() switch
        {
            "deepseek"        => (LLMProviderType.DeepSeek,   "deepseek-chat",               "https://api.deepseek.com/v1"),
            "anthropic"       => (LLMProviderType.Anthropic,  "claude-sonnet-4-20250514",    null),
            "ollama"          => (LLMProviderType.Ollama,     "llama3.2",                    "http://localhost:11434"),
            "ollamaresponses" => (LLMProviderType.OllamaResponses, "llama3.2",               "http://localhost:11434"),
            "gemini"          => (LLMProviderType.Gemini,     "gemini-1.5-flash",            null),
            "groq"            => (LLMProviderType.Groq,       "llama-3.3-70b-versatile",     null),
            "mistral"         => (LLMProviderType.Mistral,    "mistral-large-latest",        null),
            "xai" or "xaigrok"=> (LLMProviderType.XAIGrok,   "grok-2",                      null),
            "cohere"          => (LLMProviderType.Cohere,     "command-r-plus",              null),
            "perplexity"      => (LLMProviderType.Perplexity, "sonar-pro",                   null),
            "togetherai"      => (LLMProviderType.TogetherAI, "meta-llama/Llama-3.3-70B",   null),
            "fireworksai"     => (LLMProviderType.FireworksAI,"accounts/fireworks/models/llama-v3p1-70b-instruct", null),
            "cerebras"        => (LLMProviderType.Cerebras,   "llama3.1-70b",                null),
            "openrouter"      => (LLMProviderType.OpenRouter, "openai/gpt-4o",               null),
            "huggingface"     => (LLMProviderType.HuggingFaceInference, "meta-llama/Llama-3.3-70B-Instruct", null),
            "azureopenai"     => (LLMProviderType.AzureOpenAI,"gpt-4o",                      null),
            "local"           => (LLMProviderType.Local,      "local-model",                 "http://localhost:1234"),
            _                 => (LLMProviderType.OpenAI,     "gpt-4o",                      null),
        };

    /// <summary>
    /// Returns the (DefaultModel, DefaultEndpoint) tuple for a known <see cref="LLMProviderType"/>.
    /// Used by <see cref="AgxiomeDIOptions.UseModel"/> to fill in omitted values.
    /// </summary>
    public static (LLMProviderType Provider, string DefaultModel, string? DefaultEndpoint) Defaults(
        LLMProviderType provider) => provider switch
    {
        LLMProviderType.DeepSeek         => (provider, "deepseek-chat",               "https://api.deepseek.com/v1"),
        LLMProviderType.Anthropic        => (provider, "claude-sonnet-4-20250514",    null),
        LLMProviderType.Ollama           => (provider, "llama3.2",                    "http://localhost:11434"),
        LLMProviderType.OllamaResponses  => (provider, "llama3.2",                    "http://localhost:11434"),
        LLMProviderType.Gemini           => (provider, "gemini-1.5-flash",            null),
        LLMProviderType.Groq             => (provider, "llama-3.3-70b-versatile",     null),
        LLMProviderType.Mistral          => (provider, "mistral-large-latest",        null),
        LLMProviderType.XAIGrok          => (provider, "grok-2",                      null),
        LLMProviderType.Cohere           => (provider, "command-r-plus",              null),
        LLMProviderType.OpenRouter       => (provider, "openai/gpt-4o",               null),
        LLMProviderType.AzureOpenAI      => (provider, "gpt-4o",                      null),
        LLMProviderType.Local            => (provider, "local-model",                 "http://localhost:1234"),
        _                                => (provider, "gpt-4o",                      null),
    };

    /// <summary>
    /// Resolves the API key for <paramref name="provider"/> by checking (in order):
    /// 1. <c>LLMProvider:{ProviderName}:ApiKey</c> in <paramref name="config"/>
    /// 2. The matching environment variable (e.g. <c>DEEPSEEK_API_KEY</c>)
    /// </summary>
    public static string? ResolveApiKey(LLMProviderType provider, IConfiguration? config = null)
    {
        var (configPath, envVar) = provider switch
        {
            LLMProviderType.DeepSeek        => ("LLMProvider:DeepSeek:ApiKey",    "DEEPSEEK_API_KEY"),
            LLMProviderType.Anthropic       => ("LLMProvider:Anthropic:ApiKey",   "ANTHROPIC_API_KEY"),
            LLMProviderType.Gemini          => ("LLMProvider:Gemini:ApiKey",      "GEMINI_API_KEY"),
            LLMProviderType.Groq            => ("LLMProvider:Groq:ApiKey",        "GROQ_API_KEY"),
            LLMProviderType.Mistral         => ("LLMProvider:Mistral:ApiKey",     "MISTRAL_API_KEY"),
            LLMProviderType.XAIGrok         => ("LLMProvider:XAI:ApiKey",         "XAI_API_KEY"),
            LLMProviderType.Cohere          => ("LLMProvider:Cohere:ApiKey",      "COHERE_API_KEY"),
            LLMProviderType.Perplexity      => ("LLMProvider:Perplexity:ApiKey",  "PERPLEXITY_API_KEY"),
            LLMProviderType.TogetherAI      => ("LLMProvider:TogetherAI:ApiKey",  "TOGETHER_API_KEY"),
            LLMProviderType.FireworksAI     => ("LLMProvider:FireworksAI:ApiKey", "FIREWORKS_API_KEY"),
            LLMProviderType.Cerebras        => ("LLMProvider:Cerebras:ApiKey",    "CEREBRAS_API_KEY"),
            LLMProviderType.OpenRouter      => ("LLMProvider:OpenRouter:ApiKey",  "OPENROUTER_API_KEY"),
            LLMProviderType.HuggingFaceInference => ("LLMProvider:HuggingFace:ApiKey", "HUGGINGFACE_API_KEY"),
            LLMProviderType.AzureOpenAI     => ("LLMProvider:AzureOpenAI:ApiKey", "AZURE_OPENAI_API_KEY"),
            _ /* OpenAI + rest */           => ("LLMProvider:OpenAI:ApiKey",      "OPENAI_API_KEY"),
        };
        return config?[configPath] ?? Environment.GetEnvironmentVariable(envVar);
    }
}
