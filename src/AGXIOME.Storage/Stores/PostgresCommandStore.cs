using AGXIOME.Shared.Interfaces;
using Dapper;
using Npgsql;

namespace AGXIOME.Storage.Stores;

/// <summary>PostgreSQL command store. All writes go through this.</summary>
public sealed class PostgresCommandStore : ICommandStore
{
    private readonly string _connectionString;

    public PostgresCommandStore(string connectionString)
        => _connectionString = connectionString;

    public async Task SaveAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        // Dynamic SQL generation from entity type + attributes
        var sql = GenerateInsertSql<T>();
        await conn.ExecuteAsync(sql, entity, commandTimeout: 30);
    }

    public async Task UpdateAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        var sql = GenerateUpdateSql<T>();
        await conn.ExecuteAsync(sql, entity, commandTimeout: 30);
    }

    public async Task DeleteAsync<T>(string id, CancellationToken ct = default) where T : class
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        var table = typeof(T).Name.ToLower() + "s";
        await conn.ExecuteAsync($"DELETE FROM \"{table}\" WHERE id = @Id", new { Id = id }, commandTimeout: 30);
    }

    private static string GenerateInsertSql<T>()
    {
        var type = typeof(T);
        var table = type.Name.ToLower() + "s";
        var props = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        var cols = string.Join(", ", props.Select(p => $"\"{p.Name.ToLower()}\""));
        var vals = string.Join(", ", props.Select(p => "@" + p.Name));
        return $"INSERT INTO \"{table}\" ({cols}) VALUES ({vals})";
    }

    private static string GenerateUpdateSql<T>()
    {
        var type = typeof(T);
        var table = type.Name.ToLower() + "s";
        var props = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(p => p.Name != "Id");
        var set = string.Join(", ", props.Select(p => $"\"{p.Name.ToLower()}\" = @{p.Name}"));
        return $"UPDATE \"{table}\" SET {set} WHERE \"id\" = @Id";
    }
}
