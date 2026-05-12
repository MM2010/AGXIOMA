using AGXIOME.Shared.Interfaces;
using Dapper;
using MySqlConnector; // MariaDB is wire-compatible with MySQL

namespace AGXIOME.Storage.Stores;

/// <summary>MariaDB command store. MariaDB is wire-compatible with MySQL — uses MySqlConnector.</summary>
/// <remarks>
/// MariaDB 11+ supports the same wire protocol as MySQL 8.x.
/// Connection string: "Server=localhost;Database=agxiome;User=root;Password=..."
/// For MariaDB-specific features (sequence storage engines, etc.), use MariaDb.DataNuGet package.
/// </remarks>
public sealed class MariaDbStore : ICommandStore
{
    private readonly string _connectionString;

    public MariaDbStore(string connectionString) => _connectionString = connectionString;

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

    private static string GenerateInsertSql<T>() => GenerateSql<T>("INSERT INTO `{0}` ({1}) VALUES ({2})", false);
    private static string GenerateUpdateSql<T>() => GenerateSql<T>("UPDATE `{0}` SET {2} WHERE `id` = @Id", true);

    private static string GenerateSql<T>(string format, bool isUpdate)
    {
        var t = typeof(T); var table = t.Name.ToLower() + "s";
        var props = t.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (isUpdate)
        {
            var set = string.Join(", ", props.Where(p => p.Name != "Id")
                .Select(p => $"`{p.Name.ToLower()}` = @{p.Name}"));
            return string.Format(format, table, "", set);
        }
        var cols = string.Join(", ", props.Select(p => $"`{p.Name.ToLower()}`"));
        var vals = string.Join(", ", props.Select(p => "@" + p.Name));
        return string.Format(format, table, cols, vals);
    }
}
