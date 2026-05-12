using AGXIOME.Shared.Interfaces;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace AGXIOME.Storage.Stores;

/// <summary>MongoDB document store for unstructured agent memory and session state.</summary>
public sealed class MongoStore : ICommandStore
{
    private readonly IMongoDatabase _db;

    public MongoStore(string connectionString, string databaseName = "agxiome")
    {
        var client = new MongoClient(connectionString);
        _db = client.GetDatabase(databaseName);
    }

    public async Task SaveAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        var collection = GetCollection<T>();
        var id = GetId(entity);
        if (id != null)
            await collection.ReplaceOneAsync(
                Builders<T>.Filter.Eq("_id", id), entity,
                new ReplaceOptions { IsUpsert = true }, cancellationToken: ct);
        else
            await collection.InsertOneAsync(entity, cancellationToken: ct);
    }

    public async Task UpdateAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        var collection = GetCollection<T>();
        var id = GetId(entity);
        if (id != null)
            await collection.ReplaceOneAsync(
                Builders<T>.Filter.Eq("_id", id), entity, cancellationToken: ct);
    }

    public async Task DeleteAsync<T>(string id, CancellationToken ct = default) where T : class
    {
        var collection = GetCollection<T>();
        await collection.DeleteOneAsync(Builders<T>.Filter.Eq("_id", id), cancellationToken: ct);
    }

    /// <summary>Query store with a MongoDB filter.</summary>
    public async Task<IReadOnlyList<T>> QueryAsync<T>(FilterDefinition<T> filter, int limit = 50, CancellationToken ct = default) where T : class
    {
        var collection = GetCollection<T>();
        return await (await collection.FindAsync(filter, cancellationToken: ct))
            .ToListAsync(ct);
    }

    private IMongoCollection<T> GetCollection<T>() where T : class
        => _db.GetCollection<T>(typeof(T).Name.ToLower() + "s");

    private static string? GetId<T>(T entity) where T : class
    {
        var prop = typeof(T).GetProperty("Id") ?? typeof(T).GetProperty("id");
        return prop?.GetValue(entity)?.ToString();
    }
}

/// <summary>MongoDB session entity example.</summary>
public class MongoSession
{
    [BsonId]
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public string ComponentId { get; set; } = "";
    public BsonDocument? State { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
