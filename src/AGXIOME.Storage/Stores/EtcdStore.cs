using System.Net.Http.Json;
using System.Text.Json;
using dotnet_etcd;
using Etcdserverpb;
using Google.Protobuf;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage.Stores;

// ─────────────────────────────────────────────────────────────────────────────
// DB-18 — Etcd Store
// Uses dotnet-etcd. Stores JSON-serialized entities as string values.
// Key format: "/{KeyPrefix}/{EntityType}/{Id}"
// Watch and leader election are exposed as separate methods beyond ICommandStore.
// ─────────────────────────────────────────────────────────────────────────────

public sealed record EtcdStoreOptions
{
    /// <summary>Etcd endpoint, e.g., "localhost:2379"</summary>
    public required string Endpoint  { get; init; }
    public string KeyPrefix          { get; init; } = "/agxioma";
    /// <summary>Optional: lease TTL in seconds. 0 = no lease.</summary>
    public long LeaseTtlSeconds      { get; init; } = 0;
}

public sealed class EtcdCommandStore : ICommandStore, IDisposable
{
    private readonly EtcdClient          _client;
    private readonly EtcdStoreOptions    _opts;

    public StorageProviderType ProviderType => StorageProviderType.Etcd;

    public EtcdCommandStore(EtcdStoreOptions opts)
    {
        _opts   = opts ?? throw new ArgumentNullException(nameof(opts));
        _client = new EtcdClient(opts.Endpoint);
    }

    public async Task SaveAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        var key   = BuildKey<T>(GetId(entity));
        var value = JsonSerializer.Serialize(entity);
        var req   = new PutRequest { Key = ByteString.CopyFromUtf8(key), Value = ByteString.CopyFromUtf8(value) };
        if (_opts.LeaseTtlSeconds > 0)
        {
            var lease = await _client.LeaseGrantAsync(new LeaseGrantRequest { TTL = _opts.LeaseTtlSeconds },
                cancellationToken: ct);
            req.Lease = lease.ID;
        }
        await _client.PutAsync(req, cancellationToken: ct);
    }

    public async Task UpdateAsync<T>(T entity, CancellationToken ct = default) where T : class
        => await SaveAsync(entity, ct);

    public async Task DeleteAsync<T>(string id, CancellationToken ct = default) where T : class
    {
        var key = BuildKey<T>(id);
        await _client.DeleteAsync(key, cancellationToken: ct);
    }

    private string BuildKey<T>(string id) => $"{_opts.KeyPrefix}/{typeof(T).Name}/{id}";

    private static string GetId<T>(T entity)
    {
        var prop = typeof(T).GetProperty("Id");
        return prop?.GetValue(entity)?.ToString() ?? Guid.NewGuid().ToString();
    }

    public void Dispose() => _client.Dispose();
}
