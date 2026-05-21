using AGXIOME.Shared.Models;

namespace AGXIOME.Shared.Interfaces;

// ─────────────────────────────────────────────────────────────────────────────
// Core command/query contract — CQRS split.
// All 25 DB store implementations converge on ICommandStore.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Write-side contract shared by all 25 DB provider implementations.
/// Every store must expose its <see cref="ProviderType"/> and a
/// <see cref="HealthCheckAsync"/> so infrastructure can probe liveness.
/// </summary>
public interface ICommandStore
{
    /// <summary>Discriminator: which backend this store targets.</summary>
    StorageProviderType ProviderType { get; }

    Task SaveAsync<T>(T entity, CancellationToken ct = default) where T : class;
    Task UpdateAsync<T>(T entity, CancellationToken ct = default) where T : class;
    Task DeleteAsync<T>(string id, CancellationToken ct = default) where T : class;

    /// <summary>Liveness probe — returns false on connectivity failure instead of throwing.</summary>
    Task<bool> HealthCheckAsync(CancellationToken ct = default);
}

public interface IQueryStore<T> where T : class
{
    Task<T?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> QueryAsync(ISpecification<T> spec, CancellationToken ct = default);
    Task<IPaginatedResult<T>> QueryPagedAsync(ISpecification<T> spec, int page, int size, CancellationToken ct = default);
}

public interface ISpecification<T>
{
    System.Linq.Expressions.Expression<Func<T, bool>> ToExpression();
}

public interface IPaginatedResult<T>
{
    IReadOnlyList<T> Items { get; }
    int TotalCount { get; }
    int Page { get; }
    int PageSize { get; }
    int TotalPages { get; }
}

public interface IUnitOfWork : IDisposable
{
    Task BeginAsync(CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
    ICommandStore Commands { get; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Provider-agnostic session store — used wherever a session needs persistence
// regardless of backend (SQLite, Redis, PostgreSQL, JSON, …).
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Provider-agnostic session store abstraction.
/// Consumers depend on this interface; the concrete backend (SQLite, Redis,
/// PostgreSQL, JSON file, …) is selected at DI registration time.
/// </summary>
public interface ISessionStore<T> where T : class
{
    /// <summary>Persist or update a session entry. Implementations must be idempotent.</summary>
    Task SetAsync(string sessionId, T value, CancellationToken ct = default);

    /// <summary>Returns null when the session does not exist.</summary>
    Task<T?> GetAsync(string sessionId, CancellationToken ct = default);

    /// <summary>Removes the session. No-op when not found.</summary>
    Task RemoveAsync(string sessionId, CancellationToken ct = default);

    /// <summary>Liveness probe.</summary>
    Task<bool> HealthCheckAsync(CancellationToken ct = default);
}

// ─────────────────────────────────────────────────────────────────────────────
// Store factory — resolves the correct ICommandStore by provider type at runtime.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Resolves a registered <see cref="ICommandStore"/> by provider type.
/// Enables fully agnostic usage: callers request a provider, the factory
/// returns the correct implementation registered in the DI container.
/// </summary>
public interface IStoreFactory
{
    /// <summary>Returns the store registered for <paramref name="provider"/>.</summary>
    /// <exception cref="InvalidOperationException">When no store is registered for the requested provider.</exception>
    ICommandStore GetStore(StorageProviderType provider);

    /// <summary>Returns all registered stores.</summary>
    IReadOnlyDictionary<StorageProviderType, ICommandStore> GetAllStores();
}
