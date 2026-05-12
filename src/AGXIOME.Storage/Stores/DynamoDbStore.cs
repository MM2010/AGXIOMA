using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage.Stores;

// ─────────────────────────────────────────────────────────────────────────────
// DB-09 — DynamoDB Store
// Uses AWSSDK.DynamoDBv2 low-level client for direct PutItem/UpdateItem/DeleteItem.
// Serialization: all properties serialized as AttributeValue; Id field is partition key.
// Billing: PAY_PER_REQUEST (on-demand) by default.
// ─────────────────────────────────────────────────────────────────────────────

public sealed record DynamoDbStoreOptions
{
    public required string TableName { get; init; }
    public required string ServiceUrl { get; init; }   // e.g., "https://dynamodb.us-east-1.amazonaws.com"
    public required string AccessKey { get; init; }
    public required string SecretKey { get; init; }
    public string Region { get; init; } = "us-east-1";
}

public sealed class DynamoDbCommandStore : ICommandStore
{
    private readonly DynamoDbStoreOptions _opts;
    private readonly AmazonDynamoDBClient _client;

    public StorageProviderType ProviderType => StorageProviderType.DynamoDB;

    public DynamoDbCommandStore(DynamoDbStoreOptions opts)
    {
        _opts = opts ?? throw new ArgumentNullException(nameof(opts));
        var config = new AmazonDynamoDBConfig
        {
            ServiceURL = opts.ServiceUrl,
            AuthenticationRegion = opts.Region
        };
        var credentials = new Amazon.Runtime.BasicAWSCredentials(opts.AccessKey, opts.SecretKey);
        _client = new AmazonDynamoDBClient(credentials, config);
    }

    public async Task SaveAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        var item = SerializeToAttributes(entity);
        var request = new PutItemRequest
        {
            TableName = _opts.TableName,
            Item      = item
        };
        await _client.PutItemAsync(request, ct);
    }

    public async Task UpdateAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        // Use PutItem for full replacement — DynamoDB UpdateItem is complex for full-entity replacement.
        await SaveAsync(entity, ct);
    }

    public async Task DeleteAsync<T>(string id, CancellationToken ct = default) where T : class
    {
        var request = new DeleteItemRequest
        {
            TableName = _opts.TableName,
            Key       = new Dictionary<string, AttributeValue> { ["Id"] = new() { S = id } }
        };
        await _client.DeleteItemAsync(request, ct);
    }

    // Serialize entity properties to DynamoDB AttributeValue map.
    // Supports string, numeric, and bool types; all others fall back to string JSON.
    private static Dictionary<string, AttributeValue> SerializeToAttributes<T>(T entity)
    {
        var result = new Dictionary<string, AttributeValue>();
        foreach (var prop in typeof(T).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            var val = prop.GetValue(entity);
            if (val is null) continue;

            result[prop.Name] = val switch
            {
                string  s => new AttributeValue { S = s },
                int     n => new AttributeValue { N = n.ToString() },
                long    l => new AttributeValue { N = l.ToString() },
                double  d => new AttributeValue { N = d.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                float   f => new AttributeValue { N = f.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                bool    b => new AttributeValue { BOOL = b },
                _         => new AttributeValue { S = JsonSerializer.Serialize(val) }
            };
        }
        return result;
    }
}
