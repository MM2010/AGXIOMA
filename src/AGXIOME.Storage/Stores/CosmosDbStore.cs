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
    /// <summary>
    /// Enables insecure TLS only for the local Cosmos emulator running on loopback.
    /// This must remain opt-in so production paths never relax certificate validation.
    /// </summary>
    public bool AllowInsecureTlsForLocalEmulator { get; init; }

    public CosmosClientOptions CreateClientOptions()
    {
        if (!AllowInsecureTlsForLocalEmulator)
        {
            return new CosmosClientOptions();
        }

        var endpoint = TryGetAccountEndpoint(ConnectionString);
        if (endpoint is null || !endpoint.IsLoopback)
        {
            throw new InvalidOperationException(
                "AllowInsecureTlsForLocalEmulator can only be used with a loopback Cosmos endpoint.");
        }

        return new CosmosClientOptions
        {
            ConnectionMode = ConnectionMode.Gateway,
            LimitToEndpoint = true,
            RequestTimeout = TimeSpan.FromSeconds(10),
            MaxRetryAttemptsOnRateLimitedRequests = 0,
            SerializerOptions = new CosmosSerializationOptions
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
            },
            HttpClientFactory = () =>
            {
                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback =
                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };

                return new HttpClient(handler, disposeHandler: true);
            }
        };
    }

    private static Uri? TryGetAccountEndpoint(string connectionString)
    {
        foreach (var segment in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            const string prefix = "AccountEndpoint=";
            if (!segment.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var endpointValue = segment[prefix.Length..];
            return Uri.TryCreate(endpointValue, UriKind.Absolute, out var endpoint) ? endpoint : null;
        }

        return null;
    }
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
        _client    = new CosmosClient(opts.ConnectionString, opts.CreateClientOptions());
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

    public void Dispose() => _client.Dispose();
}
