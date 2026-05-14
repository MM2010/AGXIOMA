using System.Collections.Concurrent;
using AGXIOME.Core;
using AGXIOME.Core.Agent;
using AGXIOME.Core.Patterns;
using AGXIOME.Core.Routing;
using AGXIOME.LLMWIKY;
using AGXIOME.Shared.Config;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
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
    public static IServiceCollection AddAgxiome(
        this IServiceCollection services,
        Action<AgxiomeDIOptions>? configure = null)
    {
        var options = new AgxiomeDIOptions();
        configure?.Invoke(options);

        // Register core engine config
        services.AddSingleton(options.DefaultLLMConfig);
        // LLMProviderFactory is a static class — register as a factory delegate
        services.AddSingleton<Func<LLMWIKYConfig, ILLMProvider>>(sp => LLMProviderFactory.Create);

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

        return services;
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


