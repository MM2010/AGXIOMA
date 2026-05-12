using System.Net.Http.Json;
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
        _http.DefaultRequestHeaders.Add("NS", opts.Namespace);
        _http.DefaultRequestHeaders.Add("DB", opts.Database);
    }

    public async Task SaveAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        var table = TableName<T>();
        var id    = GetId(entity);
        // PUT creates or replaces a specific record.
        var res = await _http.PutAsJsonAsync($"/key/{table}/{id}", entity, ct);
        res.EnsureSuccessStatusCode();
    }

    public async Task UpdateAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        var table = TableName<T>();
        var id    = GetId(entity);
        // PATCH performs a merge update; we use PUT for full replacement.
        var res = await _http.PutAsJsonAsync($"/key/{table}/{id}", entity, ct);
        res.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync<T>(string id, CancellationToken ct = default) where T : class
    {
        var table = TableName<T>();
        var res = await _http.DeleteAsync($"/key/{table}/{id}", ct);
        res.EnsureSuccessStatusCode();
    }

    private static string TableName<T>() => typeof(T).Name.ToLowerInvariant() + "s";

    private static string GetId<T>(T entity)
    {
        var prop = typeof(T).GetProperty("Id");
        return prop?.GetValue(entity)?.ToString() ?? Guid.NewGuid().ToString();
    }
}
