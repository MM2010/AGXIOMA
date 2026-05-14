---
title: ResilientExecutor
category: entity
tags: [resilience, retry, circuit-breaker, fallback]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# ResilientExecutor

## Summary
`ResilientExecutor` is a sealed class that wraps asynchronous operations with configurable retry, exponential backoff, circuit breaker, and fallback logic. It integrates with [[CircuitBreaker]] and [[RetryPolicy]] to provide resilient execution while logging every attempt via `System.Diagnostics.Trace`. The method returns an [[InfrastructureResult]] containing full diagnostics (attempt count, duration, fallback usage, error type). This component is designed for zero-dependency tracing and is suitable for infrastructure-level resilience patterns.

## Key Details
- Sealed class in namespace `AGXIOME.Shared.Resilience`.
- Constructor accepts optional `RetryPolicy`, `CircuitBreaker`, and `componentName` (defaults to "Unknown").
- Primary method: `Task<InfrastructureResult<T>> ExecuteAsync<T>(string operationName, Func<CancellationToken, Task<T>> operation, Func<CancellationToken, Task<T>>? fallback = null, CancellationToken ct = default)`.
- If the circuit breaker is open, retries are skipped and fallback is attempted immediately (if provided).
- Uses [[ErrorClassifier]] to determine `InfrastructureErrorType` from exceptions.
- Logs attempt number, delay, and result via `Trace.WriteLine` with a structured prefix (`[LVL] [Component] [Timestamp]`).
- After exhausting retries, if a fallback is provided it is executed; if fallback also fails, the original error is returned.
- Records success or failure on the circuit breaker after each operation attempt.
- Returns `InfrastructureResult` with fields: `Value`, `Success`, `ErrorType`, `ErrorMessage`, `Attempts`, `Duration`, `UsedFallback`.

## Cross-References
- [[RetryPolicy]] — configures max retries and delay calculation.
- [[CircuitBreaker]] — tracks failure count and opens/closes the circuit.
- [[InfrastructureResult]] — the result wrapper returned by `ExecuteAsync`.
- [[ErrorClassifier]] — classifies exceptions into `InfrastructureErrorType` values.
- [[LogLevel]] — used internally for structured log prefixes.

## Source Files
- `AGXIOME.Shared/Resilience/ResilientExecutor.cs` (ingested 2026-05-14)