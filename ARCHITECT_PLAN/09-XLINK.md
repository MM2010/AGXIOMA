# 09 — X-Link Transport Protocol

## Scopo

Protocollo di trasporto proprietario AGXIOME: checksum obbligatorio, compressione adattiva, overhead ridotto del 25-40% rispetto a HTTP/JSON standard.

## Architettura

```
Producer (Agent / Tool / DB)
    │
    ▼
┌──────────────────┐
│  X-Link Envelope │
│  ┌────────────┐  │
│  │  Header    │  │  → version, timestamp, ttl, route
│  ├────────────┤  │
│  │  Checksum  │  │  → HMAC-SHA256(payload + header)
│  ├────────────┤  │
│  │  Payload   │  │  → serialized data (MessagePack binary)
│  ├────────────┤  │
│  │  Signature │  │  → ECDsa sign(header + checksum + payload)
│  └────────────┘  │
└────────┬─────────┘
         │
         ▼
┌──────────────────┐
│  Compression?    │──► Brotli (payload > 1KB) / Gzip (legacy)
└────────┬─────────┘
         │
         ▼
Consumer (Agent / Tool / DB)
```

## Envelope format

```csharp
public class XLinkEnvelope
{
    public XLinkHeader Header { get; set; }
    public string Checksum { get; set; }       // HMAC-SHA256
    public byte[] Payload { get; set; }        // MessagePack binary
    public byte[]? Signature { get; set; }     // ECDsa (opzionale)
}

public class XLinkHeader
{
    public string Version { get; set; } = "1.0";
    public string MessageId { get; set; }      // UUID v7 (time-sortable)
    public string? CorrelationId { get; set; }
    public string Source { get; set; }          // "agent:abc", "system"
    public string? Target { get; set; }         // "agent:xyz", "db:postgres"
    public string ContentType { get; set; }     // "application/json", "application/msgpack"
    public string? Compression { get; set; }    // "brotli", "gzip", null
    public long Timestamp { get; set; }         // Unix ms
    public int TtlMs { get; set; }             // Time-to-live in ms
    public string? Route { get; set; }          // Opzionale routing hint
}
```

## Compression strategy

| Payload size | Compression | CPU impact | Bandwidth saving |
|-------------|------------|-----------|-----------------|
| < 256 B | Nessuna | 0 | 0% |
| 256 B - 1 KB | Gzip (level 1) | < 0.1ms | 15-25% |
| 1 KB - 10 KB | Brotli (level 4) | < 0.5ms | 30-45% |
| 10 KB - 100 KB | Brotli (level 6) | < 2ms | 40-60% |
| > 100 KB | Brotli (level 8) | < 5ms | 50-70% |

## Checksum Validated Transport

**Obbligatorio** per ogni transazione:

```
Producer:
  1. Serialize data to MessagePack (binary)
  2. Compute HMAC-SHA256(payload + header.nonce + header.timestamp)
  3. Attach checksum to envelope
  4. Sign with ECDsa private key (opzionale: per agent-to-agent)
  5. Transmit

Consumer:
  1. Receive envelope
  2. Verify HMAC-SHA256 (stessa formula)
  3. FAIL → discard + audit log + alert
  4. PASS → decompress (se compresso)
  5. Deserialize MessagePack → typed object
  6. Process
```

## Serialization format

| Scenario | Formato | Pros |
|----------|---------|------|
| Agent ↔ Agent | MessagePack | Binary, 30-50% smaller than JSON |
| Agent ↔ DB (internal) | MessagePack | Direct deserialize to C# POCO |
| Agent ↔ HTTP endpoint | JSON (via bridge) | Interoperabilità |
| Agent ↔ Browser (SignalR) | JSON | JS nativo |

## Routing

```csharp
public interface IXLinkRouter
{
    Task SendAsync<T>(string target, T payload, XLinkOptions? options = null);
    Task PublishAsync<T>(string topic, T payload, XLinkOptions? options = null);
    Task<T> RequestAsync<T>(string target, object payload, TimeSpan? timeout = null);
    void Subscribe<T>(string topic, Func<T, Task> handler);
}

public class XLinkOptions
{
    public bool RequireChecksum { get; set; } = true;
    public bool RequireSignature { get; set; } = false;
    public CompressionLevel Compression { get; set; } = CompressionLevel.Auto;
    public int? TtlMs { get; set; }
    public bool FireAndForget { get; set; } = false;
    public int MaxRetries { get; set; } = 3;
}
```

## Transport backends

| Backend | Uso | Latenza | Throughput |
|---------|-----|---------|-----------|
| In-memory channel | Agent ↔ Tool (same process) | < 0.01ms | > 100K msg/s |
| Redis pub/sub | Cross-instance agent comm | < 5ms | > 10K msg/s |
| RabbitMQ / Kafka | Durable event streaming | < 50ms | > 50K msg/s |
| SignalR | Browser ↔ Server realtime | < 50ms | > 5K msg/s |

## Performance benchmark

| Transport | Overhead (per msg) | Throughput | 1KB msg latency |
|-----------|-------------------|-----------|----------------|
| HTTP/JSON | ~450 bytes | ~5K/s | ~3ms |
| X-Link (msgpack) | ~120 bytes | ~15K/s | ~0.8ms |
| X-Link + Brotli | ~60 bytes | ~12K/s | ~1.5ms |

## Esempio

```csharp
var envelope = new XLinkEnvelope
{
    Header = new XLinkHeader
    {
        MessageId = Guid.CreateVersion7().ToString(),
        Source = "agent:research-1",
        Target = "agent:analyst-2",
        ContentType = "application/msgpack",
        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        TtlMs = 5000
    },
    Payload = MessagePackSerializer.Serialize(new
    {
        type = "analysis_request",
        query = "Q4 earnings trends",
        max_tokens = 2048
    })
};

// Auto-compress if payload > 1KB
if (envelope.Payload.Length > 1024)
{
    envelope.Payload = BrotliCompress(envelope.Payload);
    envelope.Header.Compression = "brotli";
}

// Compute HMAC
envelope.Checksum = ComputeHMAC(envelope.Payload, sessionKey);

// Send
await router.SendAsync("agent:analyst-2", envelope);
```
