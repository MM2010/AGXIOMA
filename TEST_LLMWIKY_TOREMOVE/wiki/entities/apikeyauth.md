---
title: API Key Authentication (Apikeyauth)
category: entity
tags: [authentication, security, api-key]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# API Key Authentication (Apikeyauth)

## Summary
Provides headless / machine-to-machine authentication using hashed API keys. Keys are never stored in plaintext; instead they are hashed with PBKDF2‑SHA256 (100 000 iterations, 32‑byte salt). Each key carries a set of roles and scopes, an optional expiry date, and a revocation flag. The `IApiKeyStore` interface abstracts storage (in-memory implementation provided), and the `SeedBootstrapKeyAsync` method allows safe bootstrapping from an environment variable for development/testing.

## Key Details
- **Value Objects**:
  - `ApiKeyRecord` — stored representation (KeyId, KeyHash, Salt, Roles, Scopes, OwnerSubjectHash, timestamps, IsRevoked)
  - `ApiKeyValidationResult` — result of validation (IsValid, Record, FailureReason)
- **Interface `IApiKeyStore`**:
  - `CreateAsync` — generates a new key (raw key returned once), stores PBKDF2 hash
  - `ValidateAsync` — validates raw key against all stored records (checks revocation, expiry, then hash comparison with `FixedTimeEquals`)
  - `RevokeAsync` — sets `IsRevoked = true` on an existing key
  - `ListByOwnerAsync` — returns all keys for a given owner subject hash
  - `SeedBootstrapKeyAsync` — seeds a full‑admin key from a caller‑supplied raw key (idempotent, for dev only)
- **Implementation `InMemoryApiKeyStore`**:
  - Uses `ConcurrentDictionary<string, ApiKeyRecord>` for storage
  - Key prefix `"agx_"`; key ID generated from 8 random bytes (hex)
  - Raw key: prefix + 32 random bytes (base64url, trimmed `=`)
  - Salt: 32 random bytes (base64)
  - Hash derivation via `Rfc2898DeriveBytes.Pbkdf2`
  - `AuditPseudonymize` — SHA‑256 of owner subject ID, truncated to 16 hex characters
- **Security notes**:
  - Raw key never persisted — only PBKDF2 hash
  - Time‑constant comparison (`CryptographicOperations.FixedTimeEquals`)
  - Production should replace in‑memory store with Postgres + Redis cache

## Cross-References
- [[entities/agxiomerole]] — API keys carry a list of `AgxiomeRole` for authorization
- [[concepts/authentication]] — broader authentication context

## Source Files
- `AGXIOME.Security/ApiKeyAuth.cs` (ingested 2026-05-14)