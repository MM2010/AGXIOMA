---
title: ResiliencePolicy
category: entity
tags: [resilience, error-handling, retry, circuit-breaker, infrastructure]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# ResiliencePolicy

## Summary
The `ResiliencePolicy` namespace provides a set of types for handling infrastructure-level failures in a consistent, configurable manner. It includes an error classification enum, a result wrapper tracking attempts and fallback usage, a retry policy with exponential backoff and jitter, a circuit breaker state machine, and a static classifier that maps HTTP status codes and exceptions to actionable error types. Together, these components form the backbone of the framework’s resilience strategy for external service calls.

## Key Details
- `InfrastructureErrorType` enum: `Transient`, `RateLimited`, `ServerError`, `ConnectionLost`, `DataCorruption`, `Unknown` — each indicating a recovery strategy.
- `InfrastructureResult<T>` record: encapsulates success/failure, error details, attempt count, duration, and whether a fallback was used. Provides static `Ok` and `Fail` factory methods.
- `RetryPolicy` record: configurable `MaxRetries` (default 3), `BaseDelay` (200 ms), `ExponentialBackoff` (enabled), `JitterFactor` (0.2), `MaxDelay` (30 s). `GetDelay(attempt)` computes delay with optional jitter.
- `CircuitBreaker` class: state machine with `Closed`, `Open`, `HalfOpen` states. Uses `_failureThreshold` (default 5) and `_resetTimeout` (default 30 s). `IsOpen` transitions to HalfOpen after timeout. `RecordSuccess`/`RecordFailure` manage state.
- `ErrorClassifier` static class: `Classify(int httpStatusCode)` maps 429→RateLimited, 502/503/504→ServerError, 500→Transient, others→Unknown. `Classify(Exception)` maps `TimeoutException`→Transient, `HttpRequestException` via status code, `SocketException`/`IOException`→ConnectionLost, `InvalidDataException`→DataCorruption.
- All types are designed for use in agent infrastructure calls, retry loops, and fallback chains.

## Cross-References
- [[ResiliencePatterns]] — overarching strategies for fault tolerance.
- [[CircuitBreaker]] — related pattern for preventing cascading failures.

## Source Files
- `AGXIOME.Shared/Resilience/ResiliencePolicy.cs` (ingested 2026-05-14)