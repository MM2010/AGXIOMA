using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage.Stores;

// ─────────────────────────────────────────────────────────────────────────────
// DB-12 — SurrealDB Store
// Uses SurrealDB HTTP REST API (JSON over HTTP).
// SurrealDB.Net client is heavy — wrapping the HTTP API avoids the dependency.
// Table name is derived from entity type (lowercase plural).
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Data transfer record for the AGXIOME framework.</summary>
public sealed record SurrealDbStoreOptions
{
    public required string BaseUrl   { get; init; }  // e.g., "http://localhost:8000"
    public required string Namespace { get; init; }
    public required string Database  { get; init; }
    public required string Username  { get; init; }
    public required string Password  { get; init; }
}

public sealed class SurrealDbCommandStore : ICommandStore
{
    private readonly HttpClient           _http;
    private readonly SurrealDbStoreOptions _opts;

    public StorageProviderType ProviderType => StorageProviderType.SurrealDB;

    public SurrealDbCommandStore(SurrealDbStoreOptions opts, HttpClient? http = null)
    {
        _opts = opts ?? throw new ArgumentNullException(nameof(opts));
        _http = http ?? new HttpClient { BaseAddress = new Uri(opts.BaseUrl) };
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        var credentials = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($"{opts.Username}:{opts.Password}"));
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
        _http.DefaultRequestHeaders.Add("Surreal-NS", opts.Namespace);
        _http.DefaultRequestHeaders.Add("Surreal-DB", opts.Database);
    }

    public async Task SaveAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        var table = TableName<T>();
        var id    = GetId(entity);
        await ExecuteSqlAsync(BuildUpsertQuery(table, id, entity), ct);
    }

    public async Task UpdateAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        var table = TableName<T>();
        var id    = GetId(entity);
        await ExecuteSqlAsync(BuildUpsertQuery(table, id, entity), ct);
    }

    public async Task DeleteAsync<T>(string id, CancellationToken ct = default) where T : class
    {
        var table = TableName<T>();
        await ExecuteSqlAsync($"DELETE type::record('{EscapeSqlString(table)}', '{EscapeSqlString(id)}');", ct);
    }

    private async Task ExecuteSqlAsync(string sql, CancellationToken ct)
    {
        using var content = new StringContent(sql, Encoding.UTF8, "text/plain");
        using var response = await _http.PostAsync("/sql", content, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);

        response.EnsureSuccessStatusCode();

        if (string.IsNullOrWhiteSpace(payload))
            return;

        using var document = JsonDocument.Parse(payload);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            return;

        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (item.TryGetProperty("status", out var status)
                && string.Equals(status.GetString(), "ERR", StringComparison.OrdinalIgnoreCase))
            {
                var message = item.TryGetProperty("result", out var result)
                    ? result.ToString()
                    : payload;
                throw new InvalidOperationException($"SurrealDB SQL request failed: {message}");
            }
        }
    }

    private static string BuildUpsertQuery<T>(string table, string id, T entity)
    {
        var json = JsonSerializer.Serialize(entity);
        return $"UPSERT type::record('{EscapeSqlString(table)}', '{EscapeSqlString(id)}') CONTENT {json};";
    }

    private static string TableName<T>() => typeof(T).Name.ToLowerInvariant() + "s";

    private static string EscapeSqlString(string value) => value.Replace("'", "\\'", StringComparison.Ordinal);

    private static string GetId<T>(T entity)
    {
        var prop = typeof(T).GetProperty("Id");
        return prop?.GetValue(entity)?.ToString() ?? Guid.NewGuid().ToString();
    }
}
