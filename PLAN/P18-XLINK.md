# P18 — X-Link Transport

## File: `XLinkTransport.cs`

Namespace: `AGXIOME.XLink`

## Classi

### `XLinkEnvelope`

```csharp
public sealed class XLinkEnvelope
{
    public XLinkHeader Header { get; set; }
    public string Checksum { get; set; }       // HMAC-SHA256
    public byte[] Payload { get; set; }        // MessagePack
    public byte[]? Signature { get; set; }     // ECDsa (opzionale)

    public byte[] Serialize()
    {
        // Header + Payload → MessagePack + Checksum
    }

    public static XLinkEnvelope Deserialize(byte[] data) { /* ... */ }
}

public sealed class XLinkHeader
{
    public string Version { get; set; } = "1.0";
    public string MessageId { get; set; }
    public string? CorrelationId { get; set; }
    public string Source { get; set; }
    public string? Target { get; set; }
    public string ContentType { get; set; } = "application/msgpack";
    public string? Compression { get; set; }
    public long Timestamp { get; set; }
    public int TtlMs { get; set; } = 5000;
    public string? Route { get; set; }
}
```

### `XLinkRouter : IXLinkRouter`

```csharp
public sealed class XLinkRouter : IXLinkRouter
{
    private readonly Dictionary<string, Func<XLinkEnvelope, Task>> _handlers = new();

    public async Task SendAsync<T>(string target, T payload, XLinkOptions? options = null)
    {
        // 1. Serialize to MessagePack
        // 2. Compress (se > 1KB)
        // 3. Compute HMAC
        // 4. Attach signature (se richiesto)
        // 5. Dispatch (in-memory / Redis / SignalR)
    }

    public void Subscribe<T>(string topic, Func<T, Task> handler)
    {
        _handlers[topic] = async env =>
        {
            var data = Deserialize<T>(env.Payload, env.Header.Compression);
            await handler(data);
        };
    }
}

public record XLinkOptions(
    bool RequireChecksum = true,
    bool RequireSignature = false,
    CompressionLevel Compression = CompressionLevel.Auto,
    int? TtlMs = null,
    bool FireAndForget = false,
    int MaxRetries = 3
);

public enum CompressionLevel { None, Gzip, Brotli }
```

### Checksum validation

```csharp
public sealed class ChecksumValidator
{
    private readonly byte[] _key;

    public string Compute(byte[] data)
        => HMACSHA256.HashData(_key, data).ToHexString();

    public bool Verify(byte[] data, string expected)
        => CryptographicOperations.FixedTimeEquals(
            HMACSHA256.HashData(_key, data),
            expected.FromHex());
}
```

## MCP Tool adapter

```csharp
public sealed class MCPToolAdapter : IAgxiomeExecutableTool
{
    public string Name { get; }
    public string Description => _inputSchema["description"]?.GetValue<string>() ?? "";

    private readonly MCPClientOptions _options;
    private readonly string _toolName;
    private readonly JsonObject _inputSchema;

    public MCPToolAdapter(MCPClientOptions opts, string toolName, JsonObject schema)
    {
        _options = opts;
        _toolName = toolName;
        _inputSchema = schema;
        Name = $"{opts.ServerName}_{toolName}";  // Prefix per collisioni
    }

    public async Task<ToolResult> ExecuteAsync(ToolContext ctx, CancellationToken ct)
    {
        // Connette a MCP server via transport specifico
        // Invia tool call JSON-RPC
        // Riceve risultato
    }
}

public record MCPClientOptions(
    string ServerName, MCPTransport Transport,
    string? Command = null, string[]? Args = null,  // Stdio
    Uri? Endpoint = null,                             // SSE / HTTP
    Dictionary<string, string>? Headers = null        // Dynamic headers
);

public enum MCPTransport { Stdio, SSE, StreamableHttp }
```

## Test

| Test | Descrizione |
|------|------------|
| Envelope roundtrip | Payload invariato dopo serialize/deserialize |
| Checksum valido | Verifica passa |
| Checksum alterato | Verifica fallisce |
| Compressione >1KB | Payload compresso |
| MCP tool call | Tool eseguito via MCP |
