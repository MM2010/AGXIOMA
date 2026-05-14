---
title: AgxiomeException
category: entity
tags: [exception, error, structured-exception]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AgxiomeException

## Summary
`AgxiomeException` is the structured exception class used throughout the AGXIOME framework. It provides separate `UserMessage` (safe for end‑user display) and `TechnicalMessage` (logged internally with stack trace), an `AgxiomeErrorCode`, severity level, category, retryability flag, and optional structured details. Stack trace is never exposed to the user. Several static factory methods simplify creation of common error types (rate limit, database, network, auth).

## Key Details
- Inherits from `System.Exception`.
- Constructor accepts `AgxiomeErrorCode`, optional `technicalMessage`, `userMessage`, `innerException`, `isRetryable`, and a `Dictionary<string, object>?` of `details`.
- Uses `ErrorCatalog.Get(errorCode)` to obtain default messages, severity, and category.
- `UserMessage` — localized, safe string shown to the user.
- `TechnicalMessage` — detailed technical message logged internally.
- `Severity` — marked `[JsonIgnore]`; defined by the error code descriptor.
- `Category` — string category (e.g., Agent, LLM, Tool, Storage).
- `IsRetryable` — indicates whether the operation can be retried.
- `Details` — arbitrary structured key‑value pairs.
- Override `ToString()` returns `"[ErrorCode] UserMessage"` — never includes stack trace.
- Static factory methods:
  - `FromCode(AgxiomeErrorCode, Exception?, bool retryable)` — quick creation with code only.
  - `RateLimited(string provider, TimeSpan retryAfter, Exception?)` — for HTTP 429.
  - `DatabaseError(string operation, string message, Exception?)` — for DB failures.
  - `NetworkError(string target, int httpStatus, Exception?)` — maps status codes to specific `AgxiomeErrorCode` values.
  - `AuthFailed(string provider, Exception?)` — for authentication failures.

## Cross-References
- [[AgxiomeErrorCode]] — machine‑readable error code used by the exception.
- [[ErrorCatalog]] — provides default descriptors (message, severity, category) per error code.
- [[ErrorSeverity]] — severity enum used by the exception.

## Source Files
- `AGXIOME.Shared/Errors/AgxiomeException.cs` (ingested 2026-05-14)