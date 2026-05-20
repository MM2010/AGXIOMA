using AGXIOME.Core.Session;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AGXIOME.Core.Cache;

/// <summary>
/// One-liner DI registration for the entire memory + cache + checkpoint system.
///
/// Usage (Program.cs):
///   builder.Services.AddAgxiomeMemory(config =>
///   {
///       config.Strategy    = "pointer";
///       config.TokenBudget = 4000;
///       config.SummaryEvery = 5;
///       config.Pointers.Enabled = true;
///   });
///
/// Or with appsettings.json:
///   builder.Services.AddAgxiomeMemory(builder.Configuration.GetSection("Agent:Memory"));
/// </summary>
public static class MemoryServiceExtensions
{
    /// <summary>
    /// Register all memory services with default config.
    /// Call this once in Program.cs.
    /// </summary>
    public static IServiceCollection AddAgxiomeMemory(
        this IServiceCollection services,
        Action<AgentMemoryConfig>? configure = null)
    {
        var config = new AgentMemoryConfig();
        configure?.Invoke(config);
        services.AddSingleton(config);

        // Cache profile (detected from provider name at runtime)
        services.AddSingleton(CacheProfile.DeepSeek);

        // Token budget tracker
        services.AddSingleton<TokenBudgetTracker>();

        // Tokenizer (calibrated for default provider)
        services.AddSingleton<ITokenizer>(_ => new SimpleTokenizer("deepseek"));

        // Content pointer cache (uses ICacheStore if available — optional)
        services.AddSingleton<ContentPointerCache>(sp =>
        {
            var cfg    = sp.GetRequiredService<AgentMemoryConfig>();
            var log    = sp.GetRequiredService<ILogger<ContentPointerCache>>();
            var store  = sp.GetService<ICacheStore>();
            return new ContentPointerCache(cfg.Pointers, log, store);
        });

        // Workflow checkpoint store
        services.AddSingleton<WorkflowCheckpointStore>(sp =>
        {
            var log    = sp.GetRequiredService<ILogger<WorkflowCheckpointStore>>();
            var store  = sp.GetService<ICacheStore>();
            return new WorkflowCheckpointStore(log, store);
        });

        // Context assembler (replaces PromptBuilder)
        services.AddSingleton<ContextAssembler>();

        // Semantic fact store (in-memory default; swap for Redis/pgvector in production)
        services.AddSingleton<ISemanticFactStore, InMemoryFactStore>();

        // Unified memory store
        services.AddSingleton<IAgentMemoryStore, AgentMemoryStore>();

        return services;
    }

    /// <summary>
    /// Register memory services from a configuration section.
    /// </summary>
    public static IServiceCollection AddAgxiomeMemory(
        this IServiceCollection services,
        AgentMemoryConfig config)
    {
        services.AddSingleton(config);
        services.AddSingleton(CacheProfile.DeepSeek);
        services.AddSingleton<TokenBudgetTracker>();
        services.AddSingleton<ITokenizer>(_ => new SimpleTokenizer("deepseek"));
        services.AddSingleton<ContentPointerCache>(sp =>
        {
            var log    = sp.GetRequiredService<ILogger<ContentPointerCache>>();
            var store  = sp.GetService<ICacheStore>();
            return new ContentPointerCache(config.Pointers, log, store);
        });
        services.AddSingleton<WorkflowCheckpointStore>(sp =>
        {
            var log    = sp.GetRequiredService<ILogger<WorkflowCheckpointStore>>();
            var store  = sp.GetService<ICacheStore>();
            return new WorkflowCheckpointStore(log, store);
        });
        services.AddSingleton<ContextAssembler>();
        services.AddSingleton<ISemanticFactStore, InMemoryFactStore>();
        services.AddSingleton<IAgentMemoryStore, AgentMemoryStore>();
        return services;
    }
}
