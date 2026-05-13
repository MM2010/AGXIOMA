using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AGXIOME.DynamicRuntime.Compilation;
using AGXIOME.DynamicRuntime.Generation;
using AGXIOME.DynamicRuntime.HotFolder;
using AGXIOME.DynamicRuntime.Persistence;
using AGXIOME.DynamicRuntime.Registry;
using AGXIOME.DynamicRuntime.Skills;

namespace AGXIOME.DynamicRuntime;

/// <summary>
/// Opzioni per il DynamicRuntime.
/// </summary>
public sealed class DynamicRuntimeOptions
{
    /// <summary>Directory radice dei drop folder utente. Default: ~/.agxioma/agents</summary>
    public string? AgentsBasePath { get; set; }

    /// <summary>Directory radice per le skills. Default: ~/.agxioma/skills</summary>
    public string? SkillsBasePath { get; set; }

    /// <summary>URL base per il client Skills.sh. Default: https://api.skills.sh/v1</summary>
    public string SkillsShBaseUrl { get; set; } = "https://api.skills.sh/v1";

    /// <summary>Namespace aggiuntivi consentiti nella compilazione degli agenti.</summary>
    public string[]? AdditionalAllowedNamespaces { get; set; }
}

/// <summary>
/// Extension methods per registrare AGXIOME.DynamicRuntime nel DI container.
/// </summary>
public static class DynamicRuntimeServiceExtensions
{
    /// <summary>
    /// Registra tutti i servizi del DynamicRuntime:
    /// registry, hot-folder watcher (IHostedService), compiler, generator, skills.
    /// </summary>
    public static IServiceCollection AddDynamicAgentRuntime(
        this IServiceCollection services,
        Action<DynamicRuntimeOptions>? configure = null)
    {
        var opts = new DynamicRuntimeOptions();
        configure?.Invoke(opts);

        services.AddSingleton(opts);

        // Core registry
        services.AddSingleton<DynamicAgentRegistry>();

        // Compilation pipeline
        services.AddSingleton<AgentCompiler>();
        services.AddSingleton<LazyAgentCompiler>();
        services.AddSingleton<AgentCacheStore>();

        // Hot-folder service (IHostedService)
        services.AddSingleton<AgentDropFolderService>();
        services.AddHostedService(sp => sp.GetRequiredService<AgentDropFolderService>());

        // LLM Code Generation
        services.AddSingleton(sp =>
        {
            var llmProvider = sp.GetRequiredService<Shared.Interfaces.ILLMProvider>();
            var compiler = sp.GetRequiredService<AgentCompiler>();
            var registry = sp.GetRequiredService<DynamicAgentRegistry>();
            return new AgentCodeGenerator(llmProvider, compiler, registry);
        });

        // Skills
        services.AddSingleton(sp =>
            new SkillsShClient { BaseUrl = opts.SkillsShBaseUrl });
        services.AddSingleton(sp =>
            new SkillsShRegistry(opts.SkillsBasePath));
        services.AddSingleton<SkillAdapter>();

        return services;
    }
}
