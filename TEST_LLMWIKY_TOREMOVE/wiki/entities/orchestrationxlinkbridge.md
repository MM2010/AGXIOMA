---
title: XLinkOrchestrationBridge
category: entity
tags: [orchestration, xlink, bridge, completion, hitl]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# XLinkOrchestrationBridge

## Summary
XLinkOrchestrationBridge is an XLink-backed bridge that implements both `ICompletionNotifier` and `IHitlCompletionSource` for distributed multi-node orchestration scenarios. It publishes `TaskResultEnvelope` payloads over the XLink routing bus using HMAC-signed envelopes, enabling sub-agents running on remote processes to contribute results and HITL resolutions to a shared plan. In-process channels per plan ID provide local subscription streams, while global topic subscriptions feed results from the bus into those channels.

## Key Details
- Implements `IXLinkCompletionNotifier` (which extends the `ICompletionNotifier` concept) and `IHitlCompletionSource` (P15).
- Uses `IXLinkRouter` for publish/subscribe messaging; subscribes globally to `"orchestration:results"` and `"orchestration:hitl:resolved"`.
- `PublishAsync` sends a `TaskResultEnvelope` to the topic `"orchestration:results:{planId}"` with checksum signing enabled.
- `SubscribeAsync` returns an `IAsyncEnumerable<TaskResultEnvelope>` from a per-plan `Channel<TaskResultEnvelope>`, allowing callers to await results for a specific plan.
- `NotifyResolvedAsync` publishes a `HitlResolvedEnvelope` to `"orchestration:hitl:resolved"`; the bridge then synthesises a resumed `TaskResultEnvelope` and writes it into the channel for the relevant plan.
- Maintains a `ConcurrentDictionary<Guid, Channel<TaskResultEnvelope>>` to track active plan channels.
- Transport envelopes (`TaskResultEnvelope`, `HitlResolvedEnvelope`) are serialisable record types that decouple the bridge from the AGXIOME.Orchestration assembly.
- Registration pattern in host DI:
  - `services.AddSingleton<XLinkOrchestrationBridge>();`
  - `services.AddSingleton<ICompletionNotifier>(sp => sp.GetRequiredService<XLinkOrchestrationBridge>());`
  - `services.AddSingleton<IHitlCompletionSource>(sp => sp.GetRequiredService<XLinkOrchestrationBridge>());`

## Cross-References
- [[IXLinkRouter]] — core routing infrastructure used for publishing and subscribing.
- [[ICompletionNotifier]] — interface for notifying task completion (locally or over XLink).
- [[IHitlCompletionSource]] — interface for notifying HITL resolution (P15).
- [[TaskResultEnvelope]] — transport record for task results over XLink.
- [[HitlResolvedEnvelope]] — transport record for HITL resolution events.
- [[XLinkOptions]] — configuration for requiring checksums on published messages.

## Source Files
- `AGXIOME.XLink/Bridges/OrchestrationXLinkBridge.cs` (ingested 2026-05-14)