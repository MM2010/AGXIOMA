---
title: ProviderMetricsCollector
category: entity
tags: [metrics, telemetry, provider, routing, real-time]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# ProviderMetricsCollector

## Summary
Real-time, per-provider metrics collector for latency (p50/p95/p99), cost, and error rate. Implemented as a thread-safe singleton with sliding‑window counters, it feeds data into [[AgxiomeModelRouter]] to enable cost‑, latency‑, capability‑, or hybrid‑based routing. This is a key differentiator from Swarms, which lacks per‑provider metrics.

## Key Details
- Singleton accessed via `ProviderMetricsCollector.Instance`.
- `BeginCall(providerName)` returns a `CallScope` (RAII: call `.Complete()` or `.Fail()`, or `Dispose()` auto‑records a failure).
- Sliding‑window latency histogram (latest 200 samples) computes p50, p95, p99.
- Error rate sliding window (last 100 calls) computes error percentage.
- Estimated cost per 1k tokens is derived from a hardcoded lookup or defaults to $0.003; can be overridden.
- `GetSnapshot(providerName)` returns a `ProviderMetricsSnapshot` with all metrics.
- `GetAllSnapshots()` returns snapshots for all tracked providers.
- Thread‑safe via `ConcurrentDictionary`, `Interlocked`, and per‑state locks.
- `Reset(providerName)` removes a provider’s state (e.g., after circuit breaker reset).

## Cross-References
- [[AgxiomeModelRouter]] — consumes provider snapshots for data-driven routing decisions.
- [[CircuitBreakerStatus]] (if exists) — integrated into snapshot for health assessment.

## Source Files
- `AGXIOME.LLMWIKY/Metrics/ProviderMetricsCollector.cs` (ingested 2026-05-14)