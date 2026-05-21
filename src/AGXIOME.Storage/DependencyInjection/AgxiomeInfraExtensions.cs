using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using AGXIOME.Storage.Embedders;
using AGXIOME.Storage.Session;
using AGXIOME.Storage.VectorStores;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AGXIOME.Storage.DependencyInjection;

/// <summary>
/// Fluent DI extensions for AGXIOMA infrastructure components
/// (Ollama embedder, Redis, PostgreSQL / pgvector).
///
/// Usage example:
/// <code>
///   services
///       .AddAgxiomeOllamaEmbedder()
///       .AddAgxiomeRedis(builder.Configuration.GetConnectionString("Redis"))
///       .AddAgxiomePgVector(builder.Configuration.GetConnectionString("Postgres")!);
/// </code>
/// </summary>
public static class AgxiomeInfraExtensions
{
    // ── Ollama Embedder ──────────────────────────────────────────────────────

    /// <summary>
    /// Registers a named HttpClient (<c>"agxiome:ollama-embed"</c>) and
    /// <see cref="OllamaMxbaiEmbedder"/> as both <see cref="IEmbedder"/> and its
    /// concrete type.  Uses the mxbai-embed-large model (1024 dims) by default.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="baseUrl">Ollama base URL. Defaults to <c>http://localhost:11434</c>.</param>
    /// <param name="model">Ollama embedding model. Defaults to <c>mxbai-embed-large</c>.</param>
    public static IServiceCollection AddAgxiomeOllamaEmbedder(
        this IServiceCollection services,
        string baseUrl = "http://localhost:11434",
        string model   = "mxbai-embed-large")
    {
        services.AddHttpClient("agxiome:ollama-embed", c =>
        {
            c.BaseAddress = new Uri(baseUrl.TrimEnd('/') + '/');
            c.Timeout     = TimeSpan.FromSeconds(120);
        });

        services.AddSingleton<OllamaMxbaiEmbedder>(sp =>
        {
            var http = sp.GetRequiredService<IHttpClientFactory>()
                         .CreateClient("agxiome:ollama-embed");
            return new OllamaMxbaiEmbedder(http, new OllamaEmbedderOptions
            {
                BaseUrl = baseUrl,
                Model   = model
            });
        });

        // Also register as IEmbedder so consumers can inject the interface.
        services.AddSingleton<IEmbedder>(sp =>
            sp.GetRequiredService<OllamaMxbaiEmbedder>());

        return services;
    }

    // ── Redis ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Registers <see cref="IConnectionMultiplexer"/> backed by Redis.
    /// Connection failures are caught and logged; the multiplexer is registered
    /// as a no-op stub (rather than throwing) so the application can start
    /// even when Redis is unavailable (graceful degradation).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">
    ///     Redis connection string (e.g. <c>localhost:6379</c>).
    ///     Defaults to <c>localhost:6379,abortConnect=false,connectTimeout=3000</c>.
    /// </param>
    public static IServiceCollection AddAgxiomeRedis(
        this IServiceCollection services,
        string? connectionString = null)
    {
        var cs = string.IsNullOrWhiteSpace(connectionString)
            ? "localhost:6379,abortConnect=false,connectTimeout=3000"
            : connectionString;

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            try
            {
                return ConnectionMultiplexer.Connect(cs);
            }
            catch (Exception ex)
            {
                sp.GetService<ILoggerFactory>()?
                  .CreateLogger("AgxiomeInfra.Redis")
                  .LogWarning(ex, "Redis unavailable at '{Cs}' — caching/session storage disabled.", cs);

                // Return a non-connected multiplexer so the container resolves
                // without throwing. Callers must handle an unusable connection.
                return ConnectionMultiplexer.Connect(
                    "localhost:6379,abortConnect=false,connectTimeout=1");
            }
        });

        return services;
    }

    // ── PostgreSQL / pgvector ────────────────────────────────────────────────

    /// <summary>
    /// Registers <see cref="PgVectorStore"/> as <see cref="IVectorStore"/>.
    /// The store requires PostgreSQL 16+ with the <c>pgvector</c> extension
    /// (<c>CREATE EXTENSION IF NOT EXISTS vector;</c> is executed automatically
    /// on first use via <see cref="IVectorStore.EnsureCollectionAsync"/>).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">Npgsql connection string.</param>
    public static IServiceCollection AddAgxiomePgVector(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddSingleton<IVectorStore>(_ => new PgVectorStore(connectionString));

        return services;
    }

    // ── IStoreFactory ────────────────────────────────────────────────────────

    /// <summary>
    /// Registers <see cref="StoreFactory"/> as <see cref="IStoreFactory"/>.
    /// Must be called after all <see cref="ICommandStore"/> implementations are registered.
    /// The factory allows agnostic provider selection at runtime:
    /// <code>var store = factory.GetStore(StorageProviderType.SQLite);</code>
    /// </summary>
    public static IServiceCollection AddAgxiomeStoreFactory(this IServiceCollection services)
    {
        services.AddSingleton<IStoreFactory>(sp =>
            new StoreFactory(sp.GetServices<ICommandStore>()));
        return services;
    }

    // ── ISessionStore<T> adapters ─────────────────────────────────────────────

    /// <summary>Registers a volatile <see cref="InMemorySessionStore{T}"/>. Zero infrastructure, dev/test only.</summary>
    public static IServiceCollection AddAgxiomeInMemorySessionStore<T>(
        this IServiceCollection services) where T : class
    {
        services.AddSingleton<ISessionStore<T>, InMemorySessionStore<T>>();
        return services;
    }

    /// <summary>Registers a SQLite-backed <see cref="SqliteSessionStore{T}"/>. Embedded, zero-server.</summary>
    public static IServiceCollection AddAgxiomeSqliteSessionStore<T>(
        this IServiceCollection services,
        string connectionString,
        string tableName = "agxiome_sessions") where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        services.AddSingleton<ISessionStore<T>>(_ =>
            new SqliteSessionStore<T>(new SqliteSessionStoreOptions
            {
                ConnectionString = connectionString,
                TableName        = tableName
            }));
        return services;
    }

    /// <summary>Registers a Redis-backed <see cref="RedisSessionStoreAdapter{T}"/>. Distributed, production-grade.</summary>
    public static IServiceCollection AddAgxiomeRedisSessionStore<T>(
        this IServiceCollection services,
        string connectionString,
        string keyPrefix = "agxioma:session",
        TimeSpan? expiry = null) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        services.AddSingleton<ISessionStore<T>>(_ =>
            new RedisSessionStoreAdapter<T>(new RedisSessionAdapterOptions
            {
                ConnectionString = connectionString,
                KeyPrefix        = keyPrefix,
                Expiry           = expiry
            }));
        return services;
    }

    /// <summary>Registers a PostgreSQL-backed <see cref="PostgresSessionStore{T}"/>. ACID, multi-tenant.</summary>
    public static IServiceCollection AddAgxiomePostgresSessionStore<T>(
        this IServiceCollection services,
        string connectionString,
        string tableName  = "agxiome_sessions",
        string schemaName = "public") where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        services.AddSingleton<ISessionStore<T>>(_ =>
            new PostgresSessionStore<T>(new PostgresSessionStoreOptions
            {
                ConnectionString = connectionString,
                TableName        = tableName,
                SchemaName       = schemaName
            }));
        return services;
    }

    /// <summary>Registers a JSON-file-backed <see cref="JsonFileSessionStore{T}"/>. Zero infrastructure.</summary>
    public static IServiceCollection AddAgxiomeJsonFileSessionStore<T>(
        this IServiceCollection services,
        string baseDirectory) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        services.AddSingleton<ISessionStore<T>>(_ =>
            new JsonFileSessionStore<T>(new JsonFileSessionStoreOptions
            {
                BaseDirectory = baseDirectory
            }));
        return services;
    }
}