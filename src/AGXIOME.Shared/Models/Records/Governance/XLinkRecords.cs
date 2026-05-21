namespace AGXIOME.Shared.Models;

/// <summary>Signed and optionally compressed XLink inter-agent message envelope.</summary>
public record XLinkEnvelope(
    XLinkHeader Header,
    string Checksum,
    byte[] Payload,
    byte[]? Signature = null
);

/// <summary>Routing and metadata header for an XLink message.</summary>
public record XLinkHeader(
    string Version,
    string MessageId,
    string Source,
    long Timestamp,
    string? CorrelationId = null,
    string? Target = null,
    string ContentType = "application/msgpack",
    string? Compression = null,
    int TtlMs = 5000,
    string? Route = null
);

/// <summary>Options controlling XLink message security, compression, and delivery behaviour.</summary>
public record XLinkOptions(
    bool RequireChecksum = true,
    bool RequireSignature = false,
    CompressionLevel Compression = CompressionLevel.None,
    int? TtlMs = null,
    bool FireAndForget = false,
    int MaxRetries = 3
);
