using Microsoft.Azure.Cosmos;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage.Stores;

// ─────────────────────────────────────────────────────────────────────────────
// DB-10 — Azure Cosmos DB Store
// Uses Microsoft.Azure.Cosmos SDK v3 (stable).
// Partition key defaults to /id. Throughput: serverless or manual provisioning.
// ─────────────────────────────────────────────────────────────────────────────

public sealed record CosmosDbStoreOptions
{
    public required string ConnectionString { get; init; }
    public required string DatabaseId       { get; init; }
    public required string ContainerId      { get; init; }
    /// <summary>Partition key path. Default: /id</summary>
    public string PartitionKeyPath { get; init; } = "/id";
}

public sealed class CosmosDbCommandStore : ICommandStore, IDisposable
{
    private readonly CosmosDbStoreOptions _opts;
    private readonly CosmosClient         _client;
    private readonly Container            _container;

    public StorageProviderType ProviderType => StorageProviderType.CosmosDB;

    public CosmosDbCommandStore(CosmosDbStoreOptions opts)
    {
        _opts      = opts ?? throw new ArgumentNullException(nameof(opts));
        _client    = new CosmosClient(opts.ConnectionString);
        _container = _client.GetContainer(opts.DatabaseId, opts.ContainerId);
    }

    public async Task SaveAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        // UpsertItemAsync is idempotent and handles both insert and replace.
        await _container.UpsertItemAsync(entity, cancellationToken: ct);
    }

    public async Task UpdateAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        // Full-document replace via upsert (no partial patch in this provider).
        await _container.UpsertItemAsync(entity, cancellationToken: ct);
    }

    public async Task DeleteAsync<T>(string id, CancellationToken ct = default) where T : class
    {
        var partitionKey = new PartitionKey(id);
        await _container.DeleteItemAsync<T>(id, partitionKey, cancellationToken: ct);
    }

    public async Task<bool> HealthCheckAsync(CancellationToken ct = default)
    {
        try
        {
            await _client.GetDatabaseQueryIterator<dynamic>("SELECT VALUE 1").ReadNextAsync(ct);
            return true;
        }
        catch { return false; }
    }

    public void Dispose() => _client.Dispose();
}
