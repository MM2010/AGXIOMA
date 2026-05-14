---
title: ToolMetricsCollector
category: entity
tags: [metrics, tools, performance, threading]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# ToolMetricsCollector

## Summary
`ToolMetricsCollector` is a thread-safe, per-tool metrics collector designed for use within the AGXIOME agent framework. It records total calls, success/error counts, and latency for each tool, and provides approximate p95 latency using a fixed-size ring buffer (last 200 samples). The collector is suitable for single-node, high-frequency tool dispatch paths and exposes immutable snapshots via the `IToolMetricsCollector` interface.

## Key Details
- Implements `IToolMetricsCollector` with methods: `Record`, `GetSnapshot`, `GetAllSnapshots`, `Reset`, `ResetAll`.
- Uses `ConcurrentDictionary<string, ToolMetricState>` for fine-grained, mostly lock‑free state management.
- Per‑tool counters (TotalCalls, SuccessCount, ErrorCount) are updated via `Interlocked`; latency aggregation is lock‑protected.
- p95 latency is computed from a ring buffer of 200 samples; requires at least 20 calls before returning a value (`-1.0` otherwise).
- `ToolMetricSnapshot` is an immutable record providing: ToolName, TotalCalls, SuccessCount, ErrorCount, TotalLatencyMs, AvgLatencyMs, P95LatencyMs, ErrorRate, LastCalledAt, LastErrorAt.
- `PercentileApproximation` enum allows selecting between ring‑buffer (default) and summary‑only modes; currently only `RingBuffer` is used in production.
- All public methods throw `ArgumentException` on null/whitespace tool names.
- Thread‑safe and ready for high‑concurrency; for multi‑node aggregation, snapshots should be pushed to an external sink (e.g., Prometheus).

## Cross-References
- [[IToolMetricsCollector]] — interface contract for this collector.
- [[ToolMetricSnapshot]] — immutable snapshot record produced by the collector.
- [[Tool]] — tools are the units being measured (concept page).

## Source Files
- `AGXIOME.Core/Tools/ToolMetricsCollector.cs` (ingested 2026-05-14)