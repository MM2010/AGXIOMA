using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage.Stores;

// ─────────────────────────────────────────────────────────────────────────────
// DB-23 — Elasticsearch Store
// Uses Elastic.Clients.Elasticsearch (official .NET 8+ client).
// BM25 scoring is the default Elasticsearch retrieval model.
// ─────────────────────────────────────────────────────────────────────────────

public sealed record ElasticsearchStoreOptions
{
    public required string CloudId   { get; init; }   // Elastic Cloud ID or null
    public required string ApiKey    { get; init; }
    /// <summary>Index name prefix. Actual index = "{Prefix}-{entitytype}".</summary>
    public string IndexPrefix { get; init; } = "agxioma";
    /// <summary>Optional: single-node URI when CloudId is empty (e.g. dev/local).</summary>
    public string? NodeUri { get; init; }
}

public sealed class ElasticsearchCommandStore : ICommandStore
{
    private readonly ElasticsearchStoreOptions _opts;
    private readonly Elastic.Clients.Elasticsearch.ElasticsearchClient _client;

    public StorageProviderType ProviderType => StorageProviderType.Elasticsearch;

    public ElasticsearchCommandStore(ElasticsearchStoreOptions opts)
    {
        _opts = opts ?? throw new ArgumentNullException(nameof(opts));

        Elastic.Clients.Elasticsearch.ElasticsearchClientSettings settings;
        if (!string.IsNullOrWhiteSpace(opts.NodeUri))
        {
            settings = new Elastic.Clients.Elasticsearch.ElasticsearchClientSettings(new Uri(opts.NodeUri));
            if (!string.IsNullOrWhiteSpace(opts.ApiKey))
            {
                settings = settings.Authentication(new Elastic.Transport.ApiKey(opts.ApiKey));
            }
        }
        else
        {
            settings = new Elastic.Clients.Elasticsearch.ElasticsearchClientSettings(opts.CloudId,
                new Elastic.Transport.ApiKey(opts.ApiKey));
        }
        _client = new Elastic.Clients.Elasticsearch.ElasticsearchClient(settings);
    }

    public async Task SaveAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        var idx = IndexName<T>();
        var id  = GetId(entity);
        var res = await _client.IndexAsync(entity, idx, id, ct);
        if (!res.IsValidResponse)
            throw new InvalidOperationException($"Elasticsearch index failed: {res.DebugInformation}");
    }

    public async Task UpdateAsync<T>(T entity, CancellationToken ct = default) where T : class
        => await SaveAsync(entity, ct);   // Index with same id replaces the document.

    public async Task DeleteAsync<T>(string id, CancellationToken ct = default) where T : class
    {
        var idx = IndexName<T>();
        var res = await _client.DeleteAsync(idx, id, ct);
        if (!res.IsValidResponse && res.Result != Elastic.Clients.Elasticsearch.Result.NotFound)
            throw new InvalidOperationException($"Elasticsearch delete failed: {res.DebugInformation}");
    }

    private string IndexName<T>() => $"{_opts.IndexPrefix}-{typeof(T).Name.ToLowerInvariant()}";

    private static string GetId<T>(T entity)
    {
        var prop = typeof(T).GetProperty("Id");
        return prop?.GetValue(entity)?.ToString() ?? Guid.NewGuid().ToString();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// DB-24 — Meilisearch Store
// Uses Meilisearch.NET (official SDK). Typo-tolerance and facets configured
// at index level (outside this store). Upsert via AddDocumentsAsync.
// ─────────────────────────────────────────────────────────────────────────────

public sealed record MeilisearchStoreOptions
{
    public required string ApiUrl { get; init; }   // e.g., "http://localhost:7700"
    public required string ApiKey { get; init; }
    /// <summary>Index prefix. Actual index = "{Prefix}-{entitytype}".</summary>
    public string IndexPrefix { get; init; } = "agxioma";
}

public sealed class MeilisearchCommandStore : ICommandStore
{
    private readonly MeilisearchStoreOptions _opts;
    private readonly Meilisearch.MeilisearchClient _meili;

    public StorageProviderType ProviderType => StorageProviderType.Meilisearch;

    public MeilisearchCommandStore(MeilisearchStoreOptions opts)
    {
        _opts  = opts ?? throw new ArgumentNullException(nameof(opts));
        _meili = new Meilisearch.MeilisearchClient(opts.ApiUrl, opts.ApiKey);
    }

    public async Task SaveAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        var idx  = _meili.Index(IndexName<T>());
        var task = await idx.AddDocumentsAsync(new[] { entity }, primaryKey: "Id", cancellationToken: ct);
        // Fire-and-forget the task completion; caller can check status via taskUid if needed.
        _ = task;
    }

    public async Task UpdateAsync<T>(T entity, CancellationToken ct = default) where T : class
        => await SaveAsync(entity, ct);

    public async Task DeleteAsync<T>(string id, CancellationToken ct = default) where T : class
    {
        var idx = _meili.Index(IndexName<T>());
        await idx.DeleteOneDocumentAsync(id, ct);
    }

    private string IndexName<T>() => $"{_opts.IndexPrefix}-{typeof(T).Name.ToLowerInvariant()}";
}
