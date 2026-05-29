using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using AGXIOME.Storage.VectorStores.ValdProto;
using Grpc.Core;
using Grpc.Net.Client;
using ValdObjectService = AGXIOME.Storage.VectorStores.ValdProto.Object;

namespace AGXIOME.Storage.VectorStores;

/// <summary>Higher-level Vald transport abstraction used to keep store tests deterministic.</summary>
public interface IValdClient
{
    Task UpsertAsync(string collection, VectorRecord record, CancellationToken ct = default);
    Task UpsertBatchAsync(string collection, IReadOnlyList<VectorRecord> records, CancellationToken ct = default);
    Task<VectorRecord?> GetAsync(string collection, string id, CancellationToken ct = default);
    Task DeleteAsync(string collection, string id, CancellationToken ct = default);
    Task<IReadOnlyList<VectorSearchResult>> SearchAsync(string collection, float[] queryVector, int topK = 10, float minScore = 0f, CancellationToken ct = default);
}

/// <summary>VD-17 — Vald vector store backed by the real gRPC agent contract.
/// Vald's standalone agent exposes one ANN index, so the collection is encoded into the object ID.
/// Searches therefore over-fetch and filter by that prefix client-side.</summary>
public sealed class ValdStore : IVectorStore
{
    private readonly IValdClient _client;

    public VectorDbProviderType ProviderType => VectorDbProviderType.Vald;

    public ValdStore(IValdClient client)
        => _client = client ?? throw new ArgumentNullException(nameof(client));

    public ValdStore(Uri endpoint)
        : this(new GrpcValdClient(endpoint))
    {
    }

    public ValdStore(string endpoint)
        : this(new Uri(endpoint ?? throw new ArgumentNullException(nameof(endpoint)), UriKind.Absolute))
    {
    }

    // Compatibility overload for existing HTTP-based wiring: only BaseAddress is meaningful.
    public ValdStore(HttpClient http)
        : this(http?.BaseAddress ?? throw new ArgumentNullException(nameof(http)))
    {
    }

    public Task EnsureCollectionAsync(string collection, int dimensions, CancellationToken ct = default) => Task.CompletedTask;
    public Task DeleteCollectionAsync(string collection, CancellationToken ct = default) => Task.CompletedTask;
    public Task<IReadOnlyList<string>> ListCollectionsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

    public Task UpsertAsync(string collection, VectorRecord record, CancellationToken ct = default)
        => _client.UpsertAsync(collection, record, ct);

    public Task UpsertBatchAsync(string collection, IReadOnlyList<VectorRecord> records, CancellationToken ct = default)
        => _client.UpsertBatchAsync(collection, records, ct);

    public Task<VectorRecord?> GetAsync(string collection, string id, CancellationToken ct = default)
        => _client.GetAsync(collection, id, ct);

    public Task DeleteAsync(string collection, string id, CancellationToken ct = default)
        => _client.DeleteAsync(collection, id, ct);

    public Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        string collection,
        float[] queryVector,
        int topK = 10,
        float minScore = 0f,
        VectorFilter? filter = null,
        CancellationToken ct = default)
        => _client.SearchAsync(collection, queryVector, topK, minScore, ct);
}

internal sealed class GrpcValdClient : IValdClient
{
    private readonly GrpcChannel _channel;
    private readonly Upsert.UpsertClient _upsert;
    private readonly ValdObjectService.ObjectClient _object;
    private readonly Remove.RemoveClient _remove;
    private readonly Search.SearchClient _search;

    public GrpcValdClient(Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        _channel = GrpcChannel.ForAddress(endpoint);
        _upsert = new Upsert.UpsertClient(_channel);
        _object = new ValdObjectService.ObjectClient(_channel);
        _remove = new Remove.RemoveClient(_channel);
        _search = new Search.SearchClient(_channel);
    }

    public async Task UpsertAsync(string collection, VectorRecord record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var request = new UpsertRequest
        {
            Vector = ToObjectVector(collection, record),
        };

        _ = await _upsert.UpsertAsync(request, cancellationToken: ct);
    }

    public async Task UpsertBatchAsync(string collection, IReadOnlyList<VectorRecord> records, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(records);

        var request = new UpsertMultiRequest();
        foreach (var record in records)
        {
            request.Requests.Add(new UpsertRequest
            {
                Vector = ToObjectVector(collection, record),
            });
        }

        _ = await _upsert.MultiUpsertAsync(request, cancellationToken: ct);
    }

    public async Task<VectorRecord?> GetAsync(string collection, string id, CancellationToken ct = default)
    {
        try
        {
            var response = await _object.GetObjectAsync(new ObjectVectorRequest
            {
                Id = new ObjectId { Id = ValdId(collection, id) },
            }, cancellationToken: ct);

            return new VectorRecord(id, response.Vector.ToArray());
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string collection, string id, CancellationToken ct = default)
    {
        _ = await _remove.RemoveAsync(new RemoveRequest
        {
            Id = new ObjectId { Id = ValdId(collection, id) },
        }, cancellationToken: ct);
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(string collection, float[] queryVector, int topK = 10, float minScore = 0f, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(queryVector);

        try
        {
            var request = new SearchRequest
            {
                Config = new SearchConfig { Num = (uint)Math.Max(topK * 16, 64) },
            };
            request.Vector.AddRange(queryVector);

            var response = await _search.SearchAsync(request, cancellationToken: ct);
            var results = new List<VectorSearchResult>(topK);
            foreach (var match in response.Results)
            {
                if (!match.Id.StartsWith(collection + "/", StringComparison.Ordinal))
                    continue;

                var score = 1f - match.Distance;
                if (score < minScore)
                    continue;

                results.Add(new VectorSearchResult(match.Id[(collection.Length + 1)..], score));
                if (results.Count == topK)
                    break;
            }

            return results;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return Array.Empty<VectorSearchResult>();
        }
    }

    private static ObjectVector ToObjectVector(string collection, VectorRecord record)
    {
        var vector = new ObjectVector
        {
            Id = ValdId(collection, record.Id),
        };
        vector.Vector.AddRange(record.Vector);
        return vector;
    }

    private static string ValdId(string collection, string id) => $"{collection}/{id}";
}