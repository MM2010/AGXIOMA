using AGXIOME.Shared.Interfaces;
using Dapper;
using MySqlConnector;

namespace AGXIOME.Storage.Stores;

/// <summary>MySQL/MariaDB command store.</summary>
public sealed class MySqlCommandStore : ICommandStore
{
    private readonly string _connectionString;

    public MySqlCommandStore(string connectionString) => _connectionString = connectionString;

    public async Task SaveAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        await using var conn = new MySqlConnection(_connectionString);
        var sql = GenerateInsertSql<T>();
        await conn.ExecuteAsync(sql, entity, commandTimeout: 30);
    }

    public async Task UpdateAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        await using var conn = new MySqlConnection(_connectionString);
        var sql = GenerateUpdateSql<T>();
        await conn.ExecuteAsync(sql, entity, commandTimeout: 30);
    }

    public async Task DeleteAsync<T>(string id, CancellationToken ct = default) where T : class
    {
        await using var conn = new MySqlConnection(_connectionString);
        var table = typeof(T).Name.ToLower() + "s";
        await conn.ExecuteAsync($"DELETE FROM `{table}` WHERE id = @Id", new { Id = id }, commandTimeout: 30);
    }

    private static string GenerateInsertSql<T>()
    {
        var t = typeof(T); var table = t.Name.ToLower() + "s";
        var props = t.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        var cols = string.Join(", ", props.Select(p => $"`{p.Name.ToLower()}`"));
        var vals = string.Join(", ", props.Select(p => "@" + p.Name));
        return $"INSERT INTO `{table}` ({cols}) VALUES ({vals})";
    }

    private static string GenerateUpdateSql<T>()
    {
        var t = typeof(T); var table = t.Name.ToLower() + "s";
        var set = string.Join(", ", t.GetProperties().Where(p => p.Name != "Id")
            .Select(p => $"`{p.Name.ToLower()}` = @{p.Name}"));
        return $"UPDATE `{table}` SET {set} WHERE `id` = @Id";
    }
}
