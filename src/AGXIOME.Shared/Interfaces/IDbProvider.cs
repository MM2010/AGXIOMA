using AGXIOME.Shared.Models;

namespace AGXIOME.Shared.Interfaces;

/// <summary>Paged query result.</summary>
public record DbPage<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

/// <summary>DB provider contract. The storage backend is opaque — SQL, NoSQL, KV, or time-series.
/// Implementations handle connection lifecycle and pooling internally.</summary>
public interface IDbProvider
{
    StorageProviderType ProviderType { get; }

    Task<T?> GetByIdAsync<T>(string collection, string id, CancellationToken ct = default) where T : class;
    Task<IReadOnlyList<T>> QueryAsync<T>(string collection, string? filter = null,
        int limit = 100, CancellationToken ct = default) where T : class;
    Task<DbPage<T>> QueryPagedAsync<T>(string collection, string? filter, int page, int size,
        CancellationToken ct = default) where T : class;
    Task UpsertAsync<T>(string collection, string id, T entity, CancellationToken ct = default) where T : class;
    Task DeleteAsync(string collection, string id, CancellationToken ct = default);
    Task<bool> ExistsAsync(string collection, string id, CancellationToken ct = default);
    Task<long> CountAsync(string collection, string? filter = null, CancellationToken ct = default);

    /// <summary>Ensure collection/table/index exists with the given schema hint.</summary>
    Task EnsureCollectionAsync(string collection, Type entityType, CancellationToken ct = default);
}
