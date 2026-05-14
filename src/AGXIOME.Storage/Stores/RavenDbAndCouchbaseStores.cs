using System.Text.Json;
using Couchbase;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.KeyValue;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;

namespace AGXIOME.Storage.Stores;

// ─────────────────────────────────────────────────────────────────────────────
// DB-13 — RavenDB Store
// Uses RavenDB.Client. Supports embedded mode (server URL = in-process cluster)
// and cloud/on-prem cluster. LINQ-based queries are on IDocumentSession.
// ─────────────────────────────────────────────────────────────────────────────

public sealed record RavenDbStoreOptions
{
    public required string[] ServerUrls { get; init; }
    public required string   DatabaseName { get; init; }
    /// <summary>Path to client certificate PFX. Null = unsecured (dev only).</summary>
    public string? CertificatePath { get; init; }
}

public sealed class RavenDbCommandStore : ICommandStore, IDisposable
{
    private readonly IDocumentStore _store;

    public StorageProviderType ProviderType => StorageProviderType.RavenDB;

    public RavenDbCommandStore(RavenDbStoreOptions opts)
    {
        if (opts is null) throw new ArgumentNullException(nameof(opts));
        var store = new DocumentStore
        {
            Urls     = opts.ServerUrls,
            Database = opts.DatabaseName
        };
        if (opts.CertificatePath is not null)
            store.Certificate = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadCertificateFromFile(opts.CertificatePath);
        _store = store.Initialize();
    }

    public async Task SaveAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        using var session = _store.OpenAsyncSession();
        var id = GetEntityId(entity);
        await session.StoreAsync(entity, id, ct);
        await session.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        using var session = _store.OpenAsyncSession();
        var id = GetEntityId(entity);
        await session.StoreAsync(entity, id, ct);
        await session.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync<T>(string id, CancellationToken ct = default) where T : class
    {
        using var session = _store.OpenAsyncSession();
        session.Delete(id);
        await session.SaveChangesAsync(ct);
    }

    private static string GetEntityId<T>(T entity)
    {
        var prop = typeof(T).GetProperty("Id");
        return prop?.GetValue(entity)?.ToString() ?? Guid.NewGuid().ToString();
    }

    public void Dispose() => _store.Dispose();
}

// ─────────────────────────────────────────────────────────────────────────────
// DB-14 — Couchbase Store
// Uses CouchbaseNetClient. KV operations (Get/Upsert/Remove) are used directly;
// N1QL queries for reads are handled externally.
// ─────────────────────────────────────────────────────────────────────────────

public sealed record CouchbaseStoreOptions
{
    public required string ConnectionString { get; init; }  // couchbase://localhost
    public required string Username         { get; init; }
    public required string Password         { get; init; }
    public required string BucketName       { get; init; }
    public required string ScopeName        { get; init; }  // "_default" for default scope
    public required string CollectionName   { get; init; }  // "_default" for default collection
}

public sealed class CouchbaseCommandStore : ICommandStore, IAsyncDisposable
{
    private readonly CouchbaseStoreOptions _opts;
    private ICluster?                  _cluster;
    private Couchbase.KeyValue.ICouchbaseCollection? _collection;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public StorageProviderType ProviderType => StorageProviderType.Couchbase;

    public CouchbaseCommandStore(CouchbaseStoreOptions opts)
        => _opts = opts ?? throw new ArgumentNullException(nameof(opts));

    private async Task<Couchbase.KeyValue.ICouchbaseCollection> GetCollectionAsync(CancellationToken ct)
    {
        if (_collection is not null) return _collection;
        await _initLock.WaitAsync(ct);
        try
        {
            if (_collection is null)
            {
                _cluster = await Cluster.ConnectAsync(
                    _opts.ConnectionString,
                    new ClusterOptions().WithPasswordAuthentication(_opts.Username, _opts.Password));
                var bucket = await _cluster.BucketAsync(_opts.BucketName);
                var scope  = bucket.Scope(_opts.ScopeName);
                _collection = scope.Collection(_opts.CollectionName);
            }
        }
        finally { _initLock.Release(); }
        return _collection!;
    }

    public async Task SaveAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        var col = await GetCollectionAsync(ct);
        var key = GetKey(entity);
        await col.UpsertAsync(key, entity, new Couchbase.KeyValue.UpsertOptions().CancellationToken(ct));
    }

    public async Task UpdateAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        var col = await GetCollectionAsync(ct);
        var key = GetKey(entity);
        await col.UpsertAsync(key, entity, new Couchbase.KeyValue.UpsertOptions().CancellationToken(ct));
    }

    public async Task DeleteAsync<T>(string id, CancellationToken ct = default) where T : class
    {
        var col = await GetCollectionAsync(ct);
        await col.RemoveAsync(id, new Couchbase.KeyValue.RemoveOptions().CancellationToken(ct));
    }

    private static string GetKey<T>(T entity)
    {
        var prop = typeof(T).GetProperty("Id");
        return prop?.GetValue(entity)?.ToString() ?? Guid.NewGuid().ToString();
    }

    public async ValueTask DisposeAsync()
    {
        if (_cluster is not null)
            await _cluster.DisposeAsync();
        _initLock.Dispose();
    }
}
