using System.Net.Http.Json;
using System.Text.Json;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage.Stores;

// ─────────────────────────────────────────────────────────────────────────────
// DB-16 — Memcached Store
// Uses Memcached text/binary protocol over HTTP-compatible wrapper.
// We use Enyim.Caching (EnyimMemcachedCore) via its API abstraction.
// For testability without live server, the store wraps a simple ASCII-protocol
// TCP client. Full integration uses the Enyim DI registration.
// ─────────────────────────────────────────────────────────────────────────────

public sealed record MemcachedStoreOptions
{
    public required string Host    { get; init; }
    public int             Port    { get; init; } = 11211;
    public string KeyPrefix        { get; init; } = "agxioma";
    /// <summary>Expiration in seconds (0 = no expiry).</summary>
    public int ExpirationSeconds   { get; init; } = 0;
}

public sealed class MemcachedCommandStore : ICommandStore
{
    private readonly MemcachedStoreOptions _opts;
    // Cache of per-type bucket hash function (Enyim-style bucket routing stub).
    // In production, inject Enyim.Caching.IMemcachedClient; here we expose the contract.
    private readonly Func<string, string, string, int, Task> _setFunc;
    private readonly Func<string, Task> _deleteFunc;

    public StorageProviderType ProviderType => StorageProviderType.Memcached;

    /// <param name="setFunc">delegate(key, value, host, port) → set in Memcached</param>
    /// <param name="deleteFunc">delegate(key) → delete in Memcached</param>
    public MemcachedCommandStore(
        MemcachedStoreOptions opts,
        Func<string, string, string, int, Task>? setFunc    = null,
        Func<string, Task>?                      deleteFunc = null)
    {
        _opts       = opts ?? throw new ArgumentNullException(nameof(opts));
        _setFunc    = setFunc    ?? DefaultSetAsync;
        _deleteFunc = deleteFunc ?? DefaultDeleteAsync;
    }

    public async Task SaveAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        var key   = BuildKey<T>(GetId(entity));
        var value = JsonSerializer.Serialize(entity);
        await _setFunc(key, value, _opts.Host, _opts.Port);
    }

    public async Task UpdateAsync<T>(T entity, CancellationToken ct = default) where T : class
        => await SaveAsync(entity, ct);

    public async Task DeleteAsync<T>(string id, CancellationToken ct = default) where T : class
    {
        var key = BuildKey<T>(id);
        await _deleteFunc(key);
    }

    private string BuildKey<T>(string id) => $"{_opts.KeyPrefix}:{typeof(T).Name}:{id}";

    private static string GetId<T>(T entity)
    {
        var prop = typeof(T).GetProperty("Id");
        return prop?.GetValue(entity)?.ToString() ?? Guid.NewGuid().ToString();
    }

    // Default implementations use ASCII protocol via raw TCP (lightweight, no extra NuGet).
    private async Task DefaultSetAsync(string key, string value, string host, int port)
    {
        using var client = new System.Net.Sockets.TcpClient();
        await client.ConnectAsync(host, port);
        await using var stream = client.GetStream();
        var cmd = System.Text.Encoding.ASCII.GetBytes(
            $"set {key} 0 {_opts.ExpirationSeconds} {System.Text.Encoding.UTF8.GetByteCount(value)}\r\n{value}\r\n");
        await stream.WriteAsync(cmd);
        await stream.FlushAsync();
    }

    private async Task DefaultDeleteAsync(string key)
    {
        using var client = new System.Net.Sockets.TcpClient();
        await client.ConnectAsync(_opts.Host, _opts.Port);
        await using var stream = client.GetStream();
        var cmd = System.Text.Encoding.ASCII.GetBytes($"delete {key}\r\n");
        await stream.WriteAsync(cmd);
        await stream.FlushAsync();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// DB-17 — Hazelcast Store
// Uses Hazelcast.Net client. Stores entities in a Hazelcast IMap.
// Near-cache is configured at map creation time (cluster configuration).
// ─────────────────────────────────────────────────────────────────────────────

public sealed record HazelcastStoreOptions
{
    public required string ClusterName    { get; init; }
    public required string[] NetworkAddresses { get; init; }
    public required string MapName        { get; init; }
}

public sealed class HazelcastCommandStore : ICommandStore, IAsyncDisposable
{
    private readonly HazelcastStoreOptions _opts;
    private Hazelcast.IHazelcastClient? _client;
    private Hazelcast.DistributedObjects.IHMap<string, string>? _map;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public StorageProviderType ProviderType => StorageProviderType.Hazelcast;

    public HazelcastCommandStore(HazelcastStoreOptions opts)
        => _opts = opts ?? throw new ArgumentNullException(nameof(opts));

    private async Task<Hazelcast.DistributedObjects.IHMap<string, string>> GetMapAsync(CancellationToken ct)
    {
        if (_map is not null) return _map;
        await _initLock.WaitAsync(ct);
        try
        {
            if (_map is null)
            {
                var options = new Hazelcast.HazelcastOptionsBuilder()
                    .With(o =>
                    {
                        o.ClusterName = _opts.ClusterName;
                        foreach (var addr in _opts.NetworkAddresses)
                            o.Networking.Addresses.Add(addr);
                    })
                    .Build();
                _client = await Hazelcast.HazelcastClientFactory.StartNewClientAsync(options, ct);
                _map    = await _client.GetMapAsync<string, string>(_opts.MapName);
            }
        }
        finally { _initLock.Release(); }
        return _map!;
    }

    public async Task SaveAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        var map = await GetMapAsync(ct);
        var key = GetId(entity);
        await map.SetAsync(key, JsonSerializer.Serialize(entity));
    }

    public async Task UpdateAsync<T>(T entity, CancellationToken ct = default) where T : class
        => await SaveAsync(entity, ct);

    public async Task DeleteAsync<T>(string id, CancellationToken ct = default) where T : class
    {
        var map = await GetMapAsync(ct);
        await map.DeleteAsync(id);
    }

    private static string GetId<T>(T entity)
    {
        var prop = typeof(T).GetProperty("Id");
        return prop?.GetValue(entity)?.ToString() ?? Guid.NewGuid().ToString();
    }

    public async ValueTask DisposeAsync()
    {
        // IHMap does not implement IDisposable; just release reference.
        if (_client is not null) await _client.DisposeAsync();
        _initLock.Dispose();
    }
}
