---
title: F10 Security & Audit Tests
category: entity
tags: [security, audit, tests, RBAC, key-rotation, api-key, rate-limit, sanitization, CSP, secrets]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# F10 Security & Audit Tests

## Summary
This file contains 48 security and audit tests organized into eight SA tasks (SA-01 through SA-08), covering Role-Based Access Control (RBAC), audit trail logging, key rotation, API key authentication, rate limiting, input sanitization, Content Security Policy (CSP), and secrets management. These tests validate the core security mechanisms of the AGXIOME agent framework, ensuring correct enforcement of permissions, deterministic audit pseudonymization, key lifecycle management, API key validation, rate limit thresholds, sanitization filters, CSP header generation, and secret store operations.

## Key Details
- **SA-01 (RBAC):** 6 tests verifying permission checks for Admin, Operator, Viewer, and Service roles, including role membership and custom policy building.
- **SA-02 (Audit Trail):** 6 tests for `InMemoryAuditTrail` — record storage, action/min-severity filtering, deterministic pseudonymization, and `AuditLogger` integration.
- **SA-03 (Key Rotation):** 6 tests for `InMemoryKeyRotationService` — current key retrieval, lookup by kid, unknown kid returns null, forced rotation produces new key ID, public key defensive copy, and active key set membership.
- **SA-04 (API Key Auth):** 6 tests for `InMemoryApiKeyStore` — create/validate round-trip, invalid key rejection, permission/scope checks, expiry, and revocation.
- **SA-05 (Rate Limit):** 6 tests for `SlidingWindowRateLimiter` — bursts, concurrency limits, quota depletion, and reset behavior.
- **SA-06 (Input Sanitization):** 6 tests for `HtmlSanitizer` and `SqlSanitizer` — removing dangerous tags, preventing SQL injection patterns, and preserving safe content.
- **SA-07 (CSP):** 6 tests for `CspBuilder` — default policy generation, script restriction using nonces, and custom directive overrides.
- **SA-08 (Secrets):** 6 tests for `InMemorySecretsStore` — setting, getting, deleting secrets, TTL expiration handling, and versioning.
- All tests are implemented as synchronous `[Fact]` methods, but the interface methods are `async Task` (test methods use `async` void but are marked `[Fact]`).
- Uses in-memory implementations for audit, key rotation, API key store, rate limiter, and secrets store to avoid external dependencies.

## Cross-References
- [[F06-rbac-entity]] — Defines the `IRbacPolicy` and roles tested in SA-01.
- [[F07-audit-trail]] — Describes `InMemoryAuditTrail` and `AuditLogger` tested in SA-02.
- [[F08-key-rotation]] — Describes the `IKeyRotationService` tested in SA-03.
- [[F09-apikey-auth]] — Describes the `IApiKeyStore` tested in SA-04.
- [[F11-ratelimit]] — Describes the `IRateLimiter` tested in SA-05.
- [[F12-sanitization]] — Describes sanitizers tested in SA-06.
- [[F13-csp]] — Describes the CSP builder tested in SA-07.
- [[F14-secrets]] — Describes secret store tested in SA-08.

## Source Files
- `AGXIOME.Tests/F10SecurityAuditTests.cs` (ingested 2026-05-14)