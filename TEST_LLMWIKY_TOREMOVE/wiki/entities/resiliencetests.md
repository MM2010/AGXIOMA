---
title: ResilienceTests
category: entity
tags: [resilience, testing, retry, circuit-breaker]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# ResilienceTests

## Summary
The `ResilienceTests` class verifies the correctness of the AGXIOMA resilience primitives — `RetryPolicy`, `CircuitBreaker`, `ErrorClassifier`, and `ResilientExecutor`. These tests ensure that retry delays follow exponential backoff with optional jitter and clamping, circuit breakers open after a configurable failure threshold and reset on success, HTTP status codes are correctly classified into infrastructure error types, and the executor integrates retry, circuit breaking, and fallback logic robustly.

## Key Details
- `RetryPolicy.GetDelay` outputs increase with attempt count when exponential backoff is enabled.
- Jitter (via `JitterFactor`) introduces non-deterministic delay variation across many invocations.
- `MaxDelay` clamps the computed delay to the configured upper bound.
- `CircuitBreaker.IsOpen` becomes `true` after `failureThreshold` consecutive failures.
- A successful call resets the failure count, preventing the circuit from opening prematurely.
- `ErrorClassifier.Classify` maps HTTP status codes (429, 503, 500, 400) to `InfrastructureErrorType` values.
- `ResilientExecutor.ExecuteAsync` retries on transient exceptions and returns a success result if the operation eventually succeeds.
- When retries are exhausted, the executor returns a failure result with `Success = false`.
- Fallback delegates are invoked when the primary operation fails after all retries.
- If the circuit breaker is open, the executor skips the operation and returns a failure (or fallback if provided).
- Executor correctly categorizes exceptions into `InfrastructureErrorType` (e.g., `TimeoutException` → Transient, HTTP 429 → RateLimited).

## Cross-References
- [[RetryPolicy]] — defines the retry configuration tested here
- [[CircuitBreaker]] — state machine validated by open/reset tests
- [[ResilientExecutor]] — core resilience executor tested in multiple scenarios
- [[ErrorClassifier]] — maps HTTP codes to error types

## Source Files
- `AGXIOME.Tests/ResilienceTests.cs` (ingested 2026-05-14)