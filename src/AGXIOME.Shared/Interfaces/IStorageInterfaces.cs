namespace AGXIOME.Shared.Interfaces;

/// <summary>CQRS command-side store for creating, updating, and deleting entities. Part of the Unit of Work pattern. One command = one transactional UoW boundary.</summary>
public interface ICommandStore
{
    Task SaveAsync<T>(T entity, CancellationToken ct = default) where T : class;
    Task UpdateAsync<T>(T entity, CancellationToken ct = default) where T : class;
    Task DeleteAsync<T>(string id, CancellationToken ct = default) where T : class;
}

/// <summary>CQRS query-side store for reading entities by ID, specification, or paginated queries. Read-only â€” never mutates domain state.</summary>
/// <typeparam name="T">The entity type to query.</typeparam>
public interface IQueryStore<T> where T : class
{
    Task<T?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> QueryAsync(ISpecification<T> spec, CancellationToken ct = default);
    Task<IPaginatedResult<T>> QueryPagedAsync(ISpecification<T> spec, int page, int size, CancellationToken ct = default);
}

/// <summary>Specification pattern for query-side filtering. Converts to a LINQ expression tree for provider-specific translation.</summary>
/// <typeparam name="T">The entity type.</typeparam>
public interface ISpecification<T>
{
    System.Linq.Expressions.Expression<Func<T, bool>> ToExpression();
}

/// <summary>Paginated query result with total count and page metadata.</summary>
/// <typeparam name="T">The entity type.</typeparam>
public interface IPaginatedResult<T>
{
    IReadOnlyList<T> Items { get; }
    int TotalCount { get; }
    int Page { get; }
    int PageSize { get; }
    int TotalPages { get; }
}

/// <summary>Unit of Work for transactional command-side operations. Begin, commit, or rollback a batch of changes atomically. Always dispose when done.</summary>
public interface IUnitOfWork : IDisposable
{
    Task BeginAsync(CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
    ICommandStore Commands { get; }
}
