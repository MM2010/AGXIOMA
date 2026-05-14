---
title: Ratelimiting
category: entity
tags: [rate-limiting, sliding-window, token-bucket, security]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Ratelimiting

## Summary
The AGXIOMA framework implements a sliding-window token-bucket rate limiting system that supports three concurrent dimensions: per-endpoint, per-user, and per-IP. It is designed for integration with ASP.NET Core middleware via the `IRateLimiter` interface. The core implementation (`SlidingWindowRateLimiter`) is backed by an in-memory concurrent dictionary, provides thread-safe operation through per-bucket locking, and includes a `RateLimitPolicyRegistry` to associate route patterns and dimensions with their policies.

## Key Details
- Defines `RateLimitDimension` enum: `Endpoint`, `User`, `Ip`.
- `RateLimitPolicy` record: `MaxRequests`, `Window` (TimeSpan), optional `BurstAllowance` (default 0).
- `RateLimitCheckResult` record: `IsAllowed`, `Remaining`, `RetryAfter` (TimeSpan).
- Interface `IRateLimiter` exposes `Check(string key, RateLimitPolicy policy)` and `Reset(string key)`.
- `Check()` builds composite key `"{dimension}:{identifier}:{endpoint}"`; internally uses a `ConcurrentDictionary<string, RateLimitBucket>`.
- `RateLimitBucket` is an internal class storing timestamps in a sorted list; `Prune()` removes entries older than the sliding window (lazy eviction).
- On reachable limit, `Check()` rejects and computes `RetryAfter` as the time until the oldest request exits the window.
- `SlidingWindowRateLimiter.Reset(key)` removes the bucket from the dictionary.
- `RateLimitPolicyRegistry` registers policies `(Route, Dimension) -> Policy`; `Find()` retrieves by composite key; `All` exposes read-only dictionary.
- Thread safety: each bucket is locked individually — no global lock.
- Designed for integration with ASP.NET Core middleware (inject `IRateLimiter`).

## Cross-References
- [[middleware]] — Expected usage pattern (inject `IRateLimiter` into ASP.NET Core middleware)
- [[security]] — Part of the AGXIOME.Security namespace

## Source Files
- `AGXIOME.Security/RateLimiting.cs` (ingested 2026-05-14)