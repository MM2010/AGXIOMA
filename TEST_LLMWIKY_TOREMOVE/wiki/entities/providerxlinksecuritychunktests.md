---
title: ProviderXLinkSecurityChunkTests
category: entity
tags: [tests, provider, costtracking, xlink, security, crypto, auditing, chunking]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# ProviderXLinkSecurityChunkTests

## Summary
This test class validates five independent subsystems of the AGXIOME framework: provider cost estimation, X‑Link messaging, cryptographic operations, audit logging, and text chunking. Each group of tests ensures correctness, edge cases (e.g., unknown providers, no subscribers, tampered data), and integration readiness for the agent runtime.

## Key Details
- **CostTracker** tests verify that `ProviderCostTracker.Estimate` returns sensible costs for known providers (OpenAI), zero for wildcard providers (Ollama), and zero for unknown ones.
- **XLink** tests confirm a subscribe/send roundtrip, no‑op for unsubscribed topics, and delivery to all subscribers.
- **Crypto** tests validate ECDSA sign/verify with correct and mismatched keys, and HMAC consistency/change on tampered data.
- **Audit** test ensures `AuditLogger.LogAsync` and `AuditLogger.QueryAsync` persist and retrieve a single audit event.
- **Chunker** tests cover fixed‑size, heading‑based, and sentence‑based chunking, including fallback for plain text in `HeadingChunker`.

## Cross-References
- [[ProviderCostTracker]] — cost estimation logic under test
- [[XLinkRouter]] — messaging router tested via subscribe/send
- [[CryptoProvider]] — cryptographic operations validated
- [[AuditLogger]] — audit event logging and querying
- [[FixedSizeChunker]], [[HeadingChunker]], [[SentenceChunker]] — chunking strategies tested

## Source Files
- `AGXIOME.Tests/ProviderXLinkSecurityChunkTests.cs` (ingested 2026-05-14)