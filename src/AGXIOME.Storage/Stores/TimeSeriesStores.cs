using System.Globalization;
using System.Text;
using System.Text.Json;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;

namespace AGXIOME.Storage.Stores;

// ─────────────────────────────────────────────────────────────────────────────
// DB-20 — InfluxDB Store
// Uses InfluxDB.Client (official OSS client for InfluxDB v2 / Cloud).
// SaveAsync maps entity fields to tags/fields via convention:
//   - "Id" and string props prefixed "Tag" → tags; all others → fields.
// ─────────────────────────────────────────────────────────────────────────────

public sealed record InfluxDbStoreOptions
{
    public required string Url          { get; init; }
    public required string Token        { get; init; }
    public required string Org          { get; init; }
    public required string Bucket       { get; init; }
    public WritePrecision  Precision    { get; init; } = WritePrecision.Ms;
}

public sealed class InfluxDbCommandStore : ICommandStore, IDisposable
{
    private readonly InfluxDbStoreOptions _opts;
    private readonly InfluxDBClient       _client;
    private readonly WriteApiAsync        _writeApi;

    public StorageProviderType ProviderType => StorageProviderType.InfluxDB;

    public InfluxDbCommandStore(InfluxDbStoreOptions opts)
    {
        _opts     = opts ?? throw new ArgumentNullException(nameof(opts));
        _client   = new InfluxDBClient(opts.Url, opts.Token);
        _writeApi = _client.GetWriteApiAsync();
    }

    public async Task SaveAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        var point = BuildPoint(entity);
        await _writeApi.WritePointAsync(point, _opts.Bucket, _opts.Org, ct);
    }

    // InfluxDB is an append-only TSDB; UpdateAsync writes a newer timestamp point.
    public async Task UpdateAsync<T>(T entity, CancellationToken ct = default) where T : class
        => await SaveAsync(entity, ct);

    // InfluxDB delete requires a time-range predicate. Use the Delete API.
    public async Task DeleteAsync<T>(string id, CancellationToken ct = default) where T : class
    {
        var deleteApi = _client.GetDeleteApi();
        var start     = DateTime.UnixEpoch;
        var stop      = DateTime.UtcNow.AddSeconds(1);
        // Predicate: tag-based filtering for the given Id.
        await deleteApi.Delete(start, stop, $"_measurement=\"{Measurement<T>()}\" AND Id=\"{id}\"",
            _opts.Bucket, _opts.Org, ct);
    }

    // Build a PointData from entity using reflection.
    // Convention: string fields with name ending "Tag" are added as tags; others as fields.
    private static PointData BuildPoint<T>(T entity)
    {
        var measurement = Measurement<T>();
        var point       = PointData.Measurement(measurement);
        foreach (var prop in typeof(T).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            var val = prop.GetValue(entity);
            if (val is null) continue;
            if (prop.Name == "Id" || prop.Name.EndsWith("Tag", StringComparison.Ordinal))
                point = point.Tag(prop.Name, val.ToString()!);
            else
                point = val switch
                {
                    long    l => point.Field(prop.Name, l),
                    int     i => point.Field(prop.Name, (long)i),
                    double  d => point.Field(prop.Name, d),
                    float   f => point.Field(prop.Name, (double)f),
                    bool    b => point.Field(prop.Name, b),
                    _         => point.Field(prop.Name, val.ToString()!)
                };
        }
        return point.Timestamp(DateTime.UtcNow, WritePrecision.Ms);
    }

    private static string Measurement<T>() => typeof(T).Name.ToLowerInvariant();

    public void Dispose() => _client.Dispose();
}

// ─────────────────────────────────────────────────────────────────────────────
// DB-21 — QuestDB Store
// QuestDB exposes a PostgreSQL-compatible wire protocol on port 8812.
// Uses Npgsql for standard DML; ILP (InfluxDB Line Protocol) is used for
// high-throughput batch writes via the QuestDB HTTP /exec endpoint.
// ─────────────────────────────────────────────────────────────────────────────

public sealed record QuestDbStoreOptions
{
    /// <summary>Npgsql connection string (port 8812 by default for QuestDB).</summary>
    public required string NpgsqlConnectionString { get; init; }
    /// <summary>QuestDB HTTP endpoint for ILP writes (default: http://localhost:9000).</summary>
    public string IlpEndpoint { get; init; } = "http://localhost:9000";
}

public sealed class QuestDbCommandStore : ICommandStore
{
    private readonly QuestDbStoreOptions _opts;
    private readonly HttpClient          _http;

    public StorageProviderType ProviderType => StorageProviderType.QuestDB;

    public QuestDbCommandStore(QuestDbStoreOptions opts, HttpClient? http = null)
    {
        _opts = opts ?? throw new ArgumentNullException(nameof(opts));
        _http = http ?? new HttpClient { BaseAddress = new Uri(opts.IlpEndpoint) };
    }

    // Writes via ILP HTTP endpoint — higher throughput than Npgsql INSERT.
    public async Task SaveAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        var ilp = BuildIlpLine(entity);
        using var content = new StringContent(ilp, Encoding.UTF8, "text/plain");
        var res = await _http.PostAsync("/write", content, ct);
        res.EnsureSuccessStatusCode();
    }

    public async Task UpdateAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        // QuestDB is append-optimized; update is an insert of a new record.
        await SaveAsync(entity, ct);
    }

    public async Task DeleteAsync<T>(string id, CancellationToken ct = default) where T : class
    {
        // QuestDB does not support DELETE on hypertables; partition drop is done via REST or SQL.
        var sql     = $"DROP TABLE IF EXISTS '{typeof(T).Name.ToLowerInvariant()}s' WHERE Id = '{id}'";
        var encoded = Uri.EscapeDataString(sql);
        var res     = await _http.GetAsync($"/exec?query={encoded}", ct);
        res.EnsureSuccessStatusCode();
    }

    // Build InfluxDB Line Protocol string for the entity.
    private static string BuildIlpLine<T>(T entity)
    {
        var table  = typeof(T).Name.ToLowerInvariant() + "s";
        var id     = GetId(entity);
        var fields = new List<string>();
        foreach (var prop in typeof(T).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            if (prop.Name == "Id") continue;
            var val = prop.GetValue(entity);
            if (val is null) continue;
            var fv = val switch
            {
                string s  => $"\"{s.Replace("\"", "\\\"")}\"",
                int    i  => i.ToString(CultureInfo.InvariantCulture) + "i",
                long   l  => l.ToString(CultureInfo.InvariantCulture) + "i",
                double d  => d.ToString(CultureInfo.InvariantCulture),
                float  f  => f.ToString(CultureInfo.InvariantCulture),
                bool   b  => b ? "t" : "f",
                _         => $"\"{JsonSerializer.Serialize(val).Replace("\"", "\\\"")}\""
            };
            fields.Add($"{prop.Name}={fv}");
        }
        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return $"{table},Id={id} {string.Join(",", fields)} {ts}";
    }

    private static string GetId<T>(T entity)
    {
        var prop = typeof(T).GetProperty("Id");
        return prop?.GetValue(entity)?.ToString() ?? Guid.NewGuid().ToString();
    }
}
