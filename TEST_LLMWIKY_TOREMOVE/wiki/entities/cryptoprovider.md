---
title: CryptoProvider
category: entity
tags: [cryptography, security, ECDSA, HMAC]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# CryptoProvider

## Summary
The `CryptoProvider` class implements the `IAgxiomeCrypto` interface, providing ECDSA-based key generation, signing, and verification using the NIST P-256 curve, as well as HMAC-SHA256 computation and constant-time verification. It serves as the default cryptographic engine for the AGXIOME framework, ensuring data integrity and authenticity.

## Key Details
- Implements `IAgxiomeCrypto` in the `AGXIOME.Security` namespace.
- Uses the `ECDsa` class with the `nistP256` elliptic curve for asymmetric operations.
- `GenerateKeyPair()` returns (private key, public key) in DER-encoded formats (ECPrivateKey, SubjectPublicKeyInfo).
- `Sign()` signs data with SHA-256 using the provided private key.
- `Verify()` verifies a signature against data and public key.
- `ComputeHMAC()` returns the HMAC-SHA256 digest as a hex string.
- `VerifyHMAC()` performs constant-time comparison via `CryptographicOperations.FixedTimeEquals` to prevent timing attacks.
- `AuditLogger` (in same file) provides an in-memory audit log implementing `IAuditLogger`, with filtering by event type, actor, time range, and limit.

## Cross-References
- [[IAgxiomeCrypto]] — interface that CryptoProvider implements
- [[IAuditLogger]] — interface implemented by AuditLogger (co-located)
- [[AuditEvent]] — data model for audit logging
- [[AuditQuery]] — query model for filtering audit events

## Source Files
- `AGXIOME.Security/CryptoProvider.cs` (ingested 2026-05-14)