using System.Text.Json;
using System.Text.Json.Serialization;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage.Stores;

// ─────────────────────────────────────────────────────────────────────────────
// DB-11 — Firestore Store
// Uses Google.Cloud.Firestore SDK. Entities are stored as Firestore documents.
// Collection name derived from entity type (PascalCase → lower plural).
// Supports subcollections via explicit collectionPath override in derived use.
// ─────────────────────────────────────────────────────────────────────────────

public sealed record FirestoreStoreOptions
{
    /// <summary>GCP project ID.</summary>
    public required string ProjectId { get; init; }
    /// <summary>Path to service account JSON credentials file. Null = use ADC.</summary>
    public string? CredentialFilePath { get; init; }
}

public sealed class FirestoreCommandStore : ICommandStore
{
    private readonly FirestoreDb _db;

    public StorageProviderType ProviderType => StorageProviderType.Firestore;

    public FirestoreCommandStore(FirestoreStoreOptions opts)
    {
        if (opts is null) throw new ArgumentNullException(nameof(opts));
        var builder = new FirestoreDbBuilder { ProjectId = opts.ProjectId };

        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST")))
        {
            builder.EmulatorDetection = EmulatorDetection.EmulatorOrProduction;
        }

#pragma warning disable CS0618 // GoogleCredential.FromFile is deprecated but CredentialFactory API may not be available in all versions
        if (opts.CredentialFilePath is not null)
            builder.Credential = Google.Apis.Auth.OAuth2.GoogleCredential.FromFile(opts.CredentialFilePath);
#pragma warning restore CS0618
        _db = builder.Build();
    }

    public async Task SaveAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        var (coll, id) = GetCollectionAndId(entity);
        var docRef = _db.Collection(coll).Document(id);
        // SetAsync with MergeAll merges into existing document (idempotent upsert).
        await docRef.SetAsync(ToDict(entity), SetOptions.MergeAll, ct);
    }

    public async Task UpdateAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        var (coll, id) = GetCollectionAndId(entity);
        var docRef = _db.Collection(coll).Document(id);
        await docRef.SetAsync(ToDict(entity), SetOptions.MergeAll, ct);
    }

    public async Task DeleteAsync<T>(string id, CancellationToken ct = default) where T : class
    {
        var coll   = typeof(T).Name.ToLowerInvariant() + "s";
        var docRef = _db.Collection(coll).Document(id);
        await docRef.DeleteAsync(cancellationToken: ct);
    }

    // Serialize entity to Dictionary<string,object> for Firestore.
    private static Dictionary<string, object> ToDict<T>(T entity)
    {
        var json  = JsonSerializer.Serialize(entity);
        var dict  = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        var result = new Dictionary<string, object>();
        foreach (var (k, v) in dict)
            result[k] = UnwrapElement(v);
        return result;
    }

    private static object UnwrapElement(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String  => el.GetString()!,
        JsonValueKind.Number  => el.TryGetInt64(out var l) ? (object)l : el.GetDouble(),
        JsonValueKind.True    => true,
        JsonValueKind.False   => false,
        JsonValueKind.Null    => null!,
        _                     => el.GetRawText()
    };

    private static (string Collection, string Id) GetCollectionAndId<T>(T entity)
    {
        var coll  = typeof(T).Name.ToLowerInvariant() + "s";
        var idProp = typeof(T).GetProperty("Id");
        var id    = idProp?.GetValue(entity)?.ToString() ?? Guid.NewGuid().ToString();
        return (coll, id);
    }
}
