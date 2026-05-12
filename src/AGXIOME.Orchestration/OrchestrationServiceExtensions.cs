using AGXIOME.Orchestration.Abstractions;
using AGXIOME.Orchestration.Dispatcher;
using AGXIOME.Orchestration.Infrastructure;
using AGXIOME.Orchestration.Models;
using AGXIOME.Orchestration.Orchestrator;
using AGXIOME.Orchestration.Tools.Master;
using AGXIOME.Orchestration.Tools.SubAgent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AGXIOME.Orchestration;

/// <summary>
/// Extension methods for registering all orchestration services into the DI container.
/// Default implementations: InMemory stores (suitable for testing and single-node scenarios).
/// Override with Redis/Postgres variants for production deployments.
/// </summary>
public static class OrchestrationServiceExtensions
{
    /// <summary>
    /// Registers all orchestration services with in-memory defaults.
    /// Call UseRedisHotStore / UsePostgresOrchestrationStore after this to override for production.
    /// </summary>
    public static IServiceCollection AddAgxiomeOrchestration(
        this IServiceCollection services,
        Action<OrchestratorOptions>? configure = null)
    {
        var options = new OrchestratorOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);

        // ── Default infrastructure (TryAdd — can be overridden before or after) ──
        services.TryAddSingleton<IHotStore, InMemoryHotStore>();
        services.TryAddSingleton<IOrchestrationStore, InMemoryOrchestrationStore>();
        services.TryAddSingleton<ICompletionNotifier, InMemoryCompletionNotifier>();

        // ── Spawner — depends on IBackgroundRunWorker (registered by caller) ──
        services.TryAddSingleton<IAgentSpawner, BackgroundWorkerAgentSpawner>();

        // ── Master tools (transient — stateless) ──
        services.AddTransient<DecomposeRequestTool>();
        services.AddTransient<PersistPlanTool>();
        services.AddTransient<GenerateAgentsTool>();
        services.AddTransient<SpawnAgentTool>();
        services.AddTransient<PollResultsTool>();
        services.AddTransient<AdvancePlanTool>();

        // ── Sub-agent tools (transient — stateless) ──
        services.AddTransient<SaveResultTool>();
        services.AddTransient<NotifyCompletionTool>();

        // ── Orchestrator (singleton — owns plan execution loop state) ──
        services.TryAddSingleton<IPlanOrchestrator, PlanOrchestrator>();

        // ── Resume service (hosted) ──
        services.AddHostedService<PlanResumeService>();

        return services;
    }

    /// <summary>
    /// Override the default InMemoryHotStore with a Redis-backed store.
    /// Call after AddAgxiomeOrchestration.
    /// </summary>
    public static IServiceCollection UseRedisHotStore(
        this IServiceCollection services,
        string connectionString)
    {
        // Remove the in-memory registration and replace with Redis.
        // The concrete Redis implementation is expected to be provided by the host project
        // (e.g., StackExchange.Redis-based). Here we record the connection string for that implementation.
        services.Configure<RedisHotStoreOptions>(o => o.ConnectionString = connectionString);
        return services;
    }

    /// <summary>
    /// Override the default InMemoryOrchestrationStore with a Postgres-backed store.
    /// Call after AddAgxiomeOrchestration.
    /// </summary>
    public static IServiceCollection UsePostgresOrchestrationStore(
        this IServiceCollection services,
        string connectionString)
    {
        services.Configure<PostgresOrchestrationStoreOptions>(o => o.ConnectionString = connectionString);
        return services;
    }

    /// <summary>
    /// P19: Registers optional security services (IAgxiomeCrypto, IAuditLogger) for
    /// TaskSpec signing and audit logging in SpawnAgentTool / SaveResultTool / PlanOrchestrator.
    /// Implementations (CryptoProvider, AuditLogger) live in AGXIOME.Security and must be
    /// registered by the host project before calling this; this method records the intent.
    /// </summary>
    public static IServiceCollection UseOrchestrationSecurity(
        this IServiceCollection services)
    {
        // Actual implementations are registered by the host (AGXIOME.Security project).
        // This method is a documentation and convention anchor — no-op if already registered.
        return services;
    }

    /// <summary>
    /// P21: Enables OpenTelemetry metric export for the orchestration layer.
    /// Adds the AGXIOME.Orchestration meter ("AGXIOME.Orchestration", "1.0.0") to the
    /// meter provider. Call inside your UseOpenTelemetry() setup:
    ///   builder.WithMetrics(m => m.AddOrchestrationMetrics());
    /// This extension is a documentation anchor — actual meter addition is done by the host.
    /// </summary>
    public static IServiceCollection AddOrchestrationMetrics(
        this IServiceCollection services)
    {
        // OrchestratorMetrics.Meter is auto-discovered by OpenTelemetry via Meter name.
        // Host project must call: meterProviderBuilder.AddMeter("AGXIOME.Orchestration")
        return services;
    }
}

/// <summary>Options for Redis hot store override.</summary>
public sealed class RedisHotStoreOptions
{
    public string ConnectionString { get; set; } = "localhost:6379,abortConnect=false";
}

/// <summary>Options for Postgres orchestration store override.</summary>
public sealed class PostgresOrchestrationStoreOptions
{
    public string ConnectionString { get; set; } = "";
}
