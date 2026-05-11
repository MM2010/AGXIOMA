namespace AGXIOME.Shared.Interfaces;

public interface ICommandStore
{
    Task SaveAsync<T>(T entity, CancellationToken ct = default) where T : class;
    Task UpdateAsync<T>(T entity, CancellationToken ct = default) where T : class;
    Task DeleteAsync<T>(string id, CancellationToken ct = default) where T : class;
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
