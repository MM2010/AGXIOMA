using AGXIOME.Shared.Interfaces;
using AGXIOME.Storage.Stores;
using Npgsql;

namespace AGXIOME.Storage;

/// <summary>Unit of Work: explicit transaction boundary for command-side operations.</summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly NpgsqlConnection _connection;
    private NpgsqlTransaction? _transaction;
    private readonly PostgresCommandStore _commands;

    public ICommandStore Commands => _commands;

    public UnitOfWork(string connectionString)
    {
        _connection = new NpgsqlConnection(connectionString);
        _commands = new PostgresCommandStore(connectionString);
    }

    public async Task BeginAsync(CancellationToken ct = default)
    {
        await _connection.OpenAsync(ct);
        _transaction = await _connection.BeginTransactionAsync(ct);
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        if (_transaction != null)
            await _transaction.CommitAsync(ct);
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_transaction != null)
            await _transaction.RollbackAsync(ct);
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _connection.Dispose();
    }
}
