---
title: XLinkRouter
category: entity
tags: [router, messaging, xlink, subscriber, envelope, compression, checksum]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# XLinkRouter

## Summary
The `XLinkRouter` is the central message routing engine in the AGXIOME X-Link subsystem. It implements the [[IXLinkRouter]] interface and provides asynchronous send, publish, and subscribe capabilities over a topic‑based channel model. It automatically creates [[XLinkEnvelope]] instances with HMAC checksums and optional Gzip compression, and maintains a thread‑safe subscriber dictionary using `ConcurrentDictionary`.

## Key Details
- Sealed class `XLinkRouter : IXLinkRouter`.
- Uses a `ConcurrentDictionary<string, List<Func<XLinkEnvelope, Task>>>` for managing subscribers per topic.
- `SendAsync<T>` creates an envelope via `CreateEnvelope<T>`, then routes to all handlers subscribed to the given target.
- `PublishAsync<T>` delegates to `SendAsync`, making publish and send functionally identical.
- `RequestAsync<T>` is **not yet implemented** (throws `NotImplementedException`).
- `Subscribe<T>` registers a handler that deserializes the envelope payload to type `T` using `System.Text.Json`.
- Envelope creation (`CreateEnvelope<T>`) applies Gzip compression if `options.Compression == CompressionLevel.Gzip`, otherwise uses raw bytes.
- Checksum is computed via `HMACSHA256` with the static key `"xlink-key"` when `options.RequireChecksum` is `true`.
- Envelope header (`XLinkHeader`) includes protocol version (`"1.0"`), a GUID message ID, origin `"agxiome"`, UTC timestamp, target, and compression info.

## Cross-References
- [[IXLinkRouter]] — the interface this class implements.
- [[XLinkEnvelope]] — the message envelope created and routed by this router.
- [[XLinkHeader]] — header metadata embedded in each envelope.
- [[XLinkOptions]] — configuration for compression and checksum requirements.

## Source Files
- `AGXIOME.XLink/XLinkRouter.cs` (ingested 2026-05-14)