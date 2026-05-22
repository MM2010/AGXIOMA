// CO-A7 — ToolkitServiceExtensions
// DI registration for all Sprint 7 orchestration components.
// Single entry point: services.AddToolkitOrchestration(registry => registry.ScanAssembly(...))

using AGXIOME.Core.Tools;
using AGXIOME.Shared.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace AGXIOME.Toolkits.Orchestration;

/// <summary>
/// Extension methods for registering the Toolkit Orchestration Intelligence layer into
/// an <see cref="IServiceCollection"/>.
/// </summary>
public static class ToolkitServiceExtensions
{
    /// <summary>
    /// Register all Sprint 7 orchestration services:
    /// <list type="bullet">
    ///   <item><description><see cref="CapabilityRegistry"/> (singleton) — built via <paramref name="configure"/>.</description></item>
    ///   <item><description><see cref="ToolRouter"/> (singleton) — deterministic fallback routing.</description></item>
    ///   <item><description><see cref="BudgetAwareExecutor"/> (singleton) — budget + metrics gateway.</description></item>
    ///   <item><description><see cref="ToolkitPolicyEnforcer"/> (singleton) — RBAC enforcement.</description></item>
    ///   <item><description><see cref="SemanticToolSelector"/> (transient) — embedding-based selection (optional, requires <see cref="IEmbedder"/> in DI).</description></item>
    /// </list>
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <param name="configure">
    /// Optional callback to populate <see cref="CapabilityRegistry"/> with toolkits.
    /// Example:
    /// <code>
    /// services.AddToolkitOrchestration(r =>
    ///     r.ScanAssembly(typeof(SqliteToolkit).Assembly));
    /// </code>
    /// </param>
    /// <param name="policyProvider">
    /// Optional custom policy provider. Defaults to <see cref="AllowAllPolicyProvider"/>.
    /// </param>
    public static IServiceCollection AddToolkitOrchestration(
        this IServiceCollection         services,
        Action<CapabilityRegistry>?     configure      = null,
        IToolkitPolicyProvider?         policyProvider = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // CapabilityRegistry — populated via configure callback or left empty for manual registration
        services.AddSingleton<CapabilityRegistry>(sp =>
        {
            var registry = new CapabilityRegistry();
            configure?.Invoke(registry);
            return registry;
        });

        // ToolRouter — pure registry lookup
        services.AddSingleton<ToolRouter>();

        // BudgetAwareExecutor — requires IToolMetricsCollector; register default if missing
        services.TryAddSingleton<IToolMetricsCollector, ToolMetricsCollector>();
        services.AddSingleton<BudgetAwareExecutor>();

        // ToolkitPolicyEnforcer — uses provided or allow-all provider
        services.AddSingleton<IToolkitPolicyProvider>(
            _ => policyProvider ?? AllowAllPolicyProvider.Instance);
        services.AddSingleton<ToolkitPolicyEnforcer>();

        // SemanticToolSelector — transient because it can hold a per-query lifecycle;
        // only registered if IEmbedder is available in the container
        services.AddTransient<SemanticToolSelector>(sp =>
        {
            var embedder = sp.GetService<IEmbedder>();
            if (embedder is null)
                throw new InvalidOperationException(
                    "SemanticToolSelector requires an IEmbedder registration. " +
                    "Add an embedder (e.g. services.AddSingleton<IEmbedder, OllamaMxbaiEmbedder>()) " +
                    "before resolving SemanticToolSelector.");
            return new SemanticToolSelector(sp.GetRequiredService<CapabilityRegistry>(), embedder);
        });

        return services;
    }

    // ── Convenience helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Register the default <see cref="ToolMetricsCollector"/> as <see cref="IToolMetricsCollector"/>
    /// if no other implementation is already registered.
    /// </summary>
    public static IServiceCollection AddToolkitMetrics(this IServiceCollection services)
    {
        services.TryAddSingleton<IToolMetricsCollector, ToolMetricsCollector>();
        return services;
    }

    /// <summary>
    /// Register an <see cref="InMemoryPolicyProvider"/> pre-populated with the given policies.
    /// </summary>
    public static IServiceCollection AddInMemoryToolkitPolicy(
        this IServiceCollection                     services,
        Action<InMemoryPolicyProvider>              configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var provider = new InMemoryPolicyProvider();
        configure(provider);
        services.AddSingleton<IToolkitPolicyProvider>(provider);
        return services;
    }
}

// ── Internal shim ─────────────────────────────────────────────────────────────

// Avoid bringing in Microsoft.Extensions.DependencyInjection.Abstractions just for TryAddSingleton.
// This replicates the essential behaviour for the two uses above.
file static class ServiceCollectionExtras
{
    public static void TryAddSingleton<TService, TImplementation>(this IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        if (services.All(d => d.ServiceType != typeof(TService)))
            services.AddSingleton<TService, TImplementation>();
    }
}
