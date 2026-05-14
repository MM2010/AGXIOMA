---
title: ErrorSystemTests
category: entity
tags: [error handling, testing, AgxiomeException, ErrorCatalog, security, user messages]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# ErrorSystemTests

## Summary
The `ErrorSystemTests` class validates the security and correctness of the AGXIOME error system. It ensures that `AgxiomeException` hides stack traces and internal details from end‑user messages, while retaining technical details for logging. It also verifies factory methods (`RateLimited`, `DatabaseError`, `NetworkError`, `AuthFailed`) produce the correct error codes, retryability, and severity, and confirms that every entry in `ErrorCatalog` provides a non‑empty user message.

## Key Details
- **`AgxiomeException.ToString()`** never contains file paths, line numbers, or `at` stack‑trace markers — only the error code and user message.
- **Inner exception details** (IP addresses, type names) are excluded from `UserMessage`, but available in `TechnicalMessage`.
- **Factory methods** map common failure scenarios to the correct `AgxiomeErrorCode` and set `IsRetryable` and `Severity` appropriately.
- **`ErrorCatalog`** must provide a valid descriptor for every defined `AgxiomeErrorCode`, including a non‑empty `UserMessage`.
- **Unknown error codes** fall back to a default descriptor (`ERR-0000`).
- **Agent integration test** confirms that error responses never expose stack traces or .NET exception types in the content.

## Cross-References
- [[AgxiomeException]] — main exception class tested here
- [[ErrorCatalog]] — descriptor registry validated for completeness
- [[AgxiomeErrorCode]] — enum of all error codes used by factory methods
- [[ErrorSeverity]] — severity levels assigned by factory methods

## Source Files
- `AGXIOME.Tests/ErrorSystemTests.cs` (ingested 2026-05-14)