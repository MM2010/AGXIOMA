---
title: Keyrotation
category: entity
tags: [key-rotation, ecdsa, p-256, security, cryptography, signing, lifecycle]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Keyrotation

## Summary
The Keyrotation entity manages the lifecycle of ECDSA P-256 signing keys used for token or document signing. It provides configurable rotation intervals, key identifier (kid) based selection for signature verification, and an overlap window during which previous keys remain valid. Private key bytes are held only in memory and zeroized upon retirement, never persisting to logs or configuration. The in‑memory implementation (`InMemoryKeyRotationService`) is suitable for single‑node development; production deployments should back it with distributed storage like Redis or Vault.

## Key Details
- Uses **ECDsa** with **nistP256** curve to generate signing key pairs.
- Each key has a unique `kid` (e.g., `k-<16 hex chars>`), creation timestamp, expiration, and active flag.
- Default rotation interval is **24 hours**; default overlap window is **1 hour**.
- `GetCurrentKey()` returns the current active key, rotating automatically when the interval is reached (minus overlap).
- `FindKey(kid)` allows retrieval of any active or recently expired key for signature verification.
- `ForceRotate()` triggers an immediate rotation and prunes expired keys.
- `AllActiveKeys` returns all non‑retired keys (current + previous overlapping).
- Private key bytes are zeroized via `CryptographicOperations.ZeroMemory` when a key is retired or disposed.
- The service implements `IDisposable` to clean up all keys on shutdown.
- Thread‑safe via `Lock` and a `ConcurrentDictionary` for key storage.

## Cross-References
- [[Entities/SigningKey]] — the value object representing a single signing key
- [[Concepts/TokenSigning]] — how these keys are used to sign and verify tokens

## Source Files
- `AGXIOME.Security/KeyRotation.cs` (ingested 2026-05-14)