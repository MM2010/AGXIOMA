using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace AGXIOME.XLink;

/// <summary>X-Link router: send, receive, checksum, and MCP tool adaptation.</summary>
public sealed class XLinkRouter : IXLinkRouter
{
    private readonly ConcurrentDictionary<string, List<Func<XLinkEnvelope, Task>>> _subscribers = new();

    public async Task SendAsync<T>(string target, T payload, XLinkOptions? options = null)
    {
        var envelope = CreateEnvelope(target, payload, options ?? new());
        // Route to subscriber or channel
        if (_subscribers.TryGetValue(target, out var handlers))
            foreach (var h in handlers) await h(envelope);
    }

    public Task PublishAsync<T>(string topic, T payload, XLinkOptions? options = null)
        => SendAsync(topic, payload, options);

    public Task<T> RequestAsync<T>(string target, object payload, TimeSpan? timeout = null)
    {
        // Send + wait for response
        throw new NotImplementedException("Request-response pattern not yet implemented");
    }

    public void Subscribe<T>(string topic, Func<T, Task> handler)
    {
        _subscribers.AddOrUpdate(topic,
            _ => new() { env => handler(Deserialize<T>(env.Payload)) },
            (_, list) => { list.Add(env => handler(Deserialize<T>(env.Payload))); return list; });
    }

    private static XLinkEnvelope CreateEnvelope<T>(string target, T payload, XLinkOptions options)
    {
        var bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(payload);
        var compressed = options.Compression switch
        {
            CompressionLevel.Gzip => GzipCompress(bytes),
            _ => bytes
        };

        var header = new XLinkHeader("1.0", Guid.NewGuid().ToString(), "agxiome",
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Target: target, Compression: options.Compression != CompressionLevel.None ? options.Compression.ToString().ToLower() : null);

        var checksum = options.RequireChecksum ? ComputeHMAC(compressed) : "";
        return new XLinkEnvelope(header, checksum, compressed);
    }

    private static string ComputeHMAC(byte[] data)
        => Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes("xlink-key"), data));

    private static byte[] GzipCompress(byte[] data)
    {
        using var ms = new MemoryStream();
        using var gzip = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionLevel.Fastest);
        gzip.Write(data);
        gzip.Flush();
        return ms.ToArray();
    }

    private static T Deserialize<T>(byte[] data)
    {
        // Detect Gzip magic bytes (0x1F 0x8B) and decompress before JSON deserialization.
        // Compression is applied transparently by the router; subscribers always receive typed values.
        if (data.Length >= 2 && data[0] == 0x1F && data[1] == 0x8B)
        {
            using var ms   = new MemoryStream(data);
            using var gz   = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionMode.Decompress);
            using var outMs = new MemoryStream();
            gz.CopyTo(outMs);
            data = outMs.ToArray();
        }
        return System.Text.Json.JsonSerializer.Deserialize<T>(data)!;
    }
}
