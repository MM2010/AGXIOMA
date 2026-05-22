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
    private readonly HttpClient            _http;
    private readonly SurrealDbStoreOptions _opts;
    private readonly HttpMessageHandler?   _handlerOverride;
    private string?                        _bearerToken;
    private readonly SemaphoreSlim         _authLock = new(1, 1);

    public StorageProviderType ProviderType => StorageProviderType.SurrealDB;

    public SurrealDbCommandStore(SurrealDbStoreOptions opts, HttpMessageHandler? handler = null)
    {
        _opts = opts ?? throw new ArgumentNullException(nameof(opts));
        _handlerOverride = handler;
        _http = handler is not null
            ? new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri(opts.BaseUrl) }
            : new HttpClient { BaseAddress = new Uri(opts.BaseUrl) };
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        // SurrealDB v2: namespace/database are set via Surreal-NS/Surreal-DB headers.
        _http.DefaultRequestHeaders.Add("Surreal-NS", opts.Namespace);
        _http.DefaultRequestHeaders.Add("Surreal-DB", opts.Database);
    }

    // Lazy JWT sign-in + namespace/database provisioning.
    // SurrealDB v2 requires JWT Bearer auth for writes, and namespaces/databases
    // must be explicitly created (no longer auto-created on first use).
    private async Task EnsureAuthAsync(CancellationToken ct)
    {
        if (_bearerToken is not null) return;
        await _authLock.WaitAsync(ct);
        try
        {
            if (_bearerToken is not null) return;

            // Step 1 — Sign in as root using a clean client (no NS/DB headers).
            using var signinHttp = _handlerOverride is not null
                ? new HttpClient(_handlerOverride, disposeHandler: false) { BaseAddress = _http.BaseAddress }
                : new HttpClient { BaseAddress = _http.BaseAddress };
            signinHttp.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            var signin = new { user = _opts.Username, pass = _opts.Password };
            using var signinRes = await signinHttp.PostAsJsonAsync("/signin", signin, ct);
            signinRes.EnsureSuccessStatusCode();
            using var signinDoc = await signinRes.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct);
            _bearerToken = signinDoc!.RootElement.GetProperty("token").GetString()
                ?? throw new InvalidOperationException("SurrealDB /signin returned no token.");

            // Step 2 — Define namespace and database if they don't exist yet.
            // Must be done without NS/DB context headers (root SQL level).
            using var initHttp = _handlerOverride is not null
                ? new HttpClient(_handlerOverride, disposeHandler: false) { BaseAddress = _http.BaseAddress }
                : new HttpClient { BaseAddress = _http.BaseAddress };
            initHttp.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            initHttp.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _bearerToken);
            var ddl = $"DEFINE NAMESPACE IF NOT EXISTS `{_opts.Namespace}`; " +
                      $"USE NS `{_opts.Namespace}`; " +
                      $"DEFINE DATABASE IF NOT EXISTS `{_opts.Database}`;";
            using var ddlContent = new StringContent(ddl, System.Text.Encoding.UTF8, "application/octet-stream");
            using var ddlRes = await initHttp.PostAsync("/sql", ddlContent, ct);
            ddlRes.EnsureSuccessStatusCode();

            // Step 3 — Apply Bearer token to the main client for all future requests.
            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _bearerToken);
        }
        finally
        {
            _authLock.Release();
        }
    }

    public async Task SaveAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        await EnsureAuthAsync(ct);
        var table = TableName<T>();
        var id    = GetId(entity);
        using var content = SerializeWithoutId(entity);
        var res  = await _http.PutAsync($"/key/{table}/{id}", content, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode || body.Contains("\"status\":\"ERR\"", StringComparison.Ordinal))
            throw new InvalidOperationException($"SurrealDB SaveAsync failed ({res.StatusCode}): {body}");
    }

    public async Task UpdateAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        await EnsureAuthAsync(ct);
        var table = TableName<T>();
        var id    = GetId(entity);
        using var content = SerializeWithoutId(entity);
        var res  = await _http.PutAsync($"/key/{table}/{id}", content, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode || body.Contains("\"status\":\"ERR\"", StringComparison.Ordinal))
            throw new InvalidOperationException($"SurrealDB UpdateAsync failed ({res.StatusCode}): {body}");
    }

    public async Task DeleteAsync<T>(string id, CancellationToken ct = default) where T : class
    {
        await EnsureAuthAsync(ct);
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

    // SurrealDB v2 rejects PUT bodies that contain an 'id' field (case-insensitive)
    // when the record ID is already specified in the URL. Strip it before serializing.
    private static StringContent SerializeWithoutId<T>(T entity)
    {
        var json = JsonSerializer.Serialize(entity);
        using var doc = JsonDocument.Parse(json);
        using var ms  = new System.IO.MemoryStream();
        using var w   = new System.Text.Json.Utf8JsonWriter(ms);
        w.WriteStartObject();
        foreach (var prop in doc.RootElement.EnumerateObject())
            if (!prop.Name.Equals("id", StringComparison.OrdinalIgnoreCase))
                prop.WriteTo(w);
        w.WriteEndObject();
        w.Flush();
        return new StringContent(
            System.Text.Encoding.UTF8.GetString(ms.ToArray()),
            System.Text.Encoding.UTF8,
            "application/json");
    }

    public async Task<bool> HealthCheckAsync(CancellationToken ct = default)
    {
        try
        {
            var res = await _http.GetAsync("/health", ct);
            return res.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}
