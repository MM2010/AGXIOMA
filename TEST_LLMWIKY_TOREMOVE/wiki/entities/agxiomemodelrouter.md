---
title: AgxiomeModelRouter
category: entity
tags: [routing, provider, metrics, circuit-breaker, load-balancing]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AgxiomeModelRouter

## Summary
The `AgxiomeModelRouter` is the core routing engine of the AGXIOMA framework. It intelligently routes LLM tasks to the optimal provider by evaluating real-time performance metrics (latency, cost, error rate), enforcing circuit-breaker policies, and applying configurable strategies (cost‑minimisation, latency‑minimisation, capability‑maximisation, hybrid, or round‑robin). It supports capability filtering, cost budgets, automatic fallback chains, and concurrent execution for high‑throughput scenarios.

## Key Details
- **Data‑driven routing**: provider metrics are measured live, not assumed.
- **CircuitBreaker per provider**: providers are automatically excluded after a configurable failure threshold; a half‑open state allows recovery after 30 seconds.
- **RoutingStrategy**: `Cost`, `Latency`, `Capability`, `Hybrid`, `RoundRobin` – default configurable via `ModelRouterConfig.DefaultStrategy`.
- **Fallback chain**: when `TryFallback` is enabled, failures cascade through ordered eligible providers.
- **Capability filtering**: requests can require `SupportsVision`, `SupportsFunctionCalling`, or a set of `RequiredCapabilities`.
- **Cost budget**: per‑request `MaxCostPerRequest` cap is enforced during eligibility.
- **Metrics snapshot**: `GetMetricsSnapshot()` exposes a dictionary of `ProviderMetrics` for external monitoring.
- **Concurrent execution**: `RunConcurrentlyAsync` runs multiple tasks in parallel with a configurable degree of parallelism, each independently routed.
- **Hybrid scoring**: combines normalised cost, latency, capability score, and error penalty with configurable weights (`CostWeight`, `LatencyWeight`, `CapabilityWeight`).

## Cross-References
- [[ModelRouterConfig]] – configuration options for the router (strategies, weights, fallback, circuit‑breaker threshold)
- [[ProviderMetrics]] – live metrics collected per provider (latency, cost, error rate)
- [[RouterEntry]] – definition of a provider (capabilities, cost, execution delegate)
- [[RouterResult]] – result envelope containing the provider name, elapsed time, and metrics

## Source Files
- `AGXIOME.Core/Routing/AgxiomeModelRouter.cs` (ingested 2026-05-14)