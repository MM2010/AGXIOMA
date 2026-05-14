---
title: Property Based Tests
category: entity
tags: [property-based-tests, testing, XLink, HMAC, SessionState, cost-tracking, chunking, cryptography]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Property Based Tests

## Summary
This test class implements property-based tests using manual random value generation to verify essential invariants of core AGXIOME components. It exercises XLink envelope payload roundtrips (including empty and large payloads), HMAC determinism and collision resistance, SessionState type preservation and overwrite semantics, `ProviderCostTracker` estimation behavior, `FixedSizeChunker` output integrity, and `CryptoProvider` key pair validity across data sizes. These tests provide a lightweight regression suite without external property-based testing libraries.

## Key Details
- **Random seed (42)** used for reproducibility in `XLink_EnvelopePayloadRoundtrip_RandomPayload` and other tests.
- **Envelope tests** confirm that `XLinkEnvelope.Payload` matches the original bytes for random, empty, and 10 MB payloads.
- **HMAC tests** prove deterministic output for identical input and distinct output for different input.
- **SessionState tests** validate round‑trip of `int`, `string`, and `double` values, plus key overwrite.
- **Cost estimation** confirms non‑zero cost for known OpenAI model, zero cost for Ollama (local) and unknown providers.
- **Fixed-size chunker** generates at least one chunk from random 5000‑character text, and all chunks have non‑null `Content`.
- **Crypto key pair** signed data of sizes 16, 100, and 1024 bytes verifies consistently.
- All tests use `[Fact]` with manual random data; no FsCheck or similar framework.

## Cross-References
- [[XLink]] — the envelope/header types tested for payload roundtrip.
- [[SessionState]] — the typed state container validated for set/get and overwrite.
- [[ProviderCostTracker]] — the cost estimator tested for provider‑specific behavior.
- [[FixedSizeChunker]] — the chunking algorithm validated for non‑null chunks.
- [[CryptoProvider]] — the cryptographic provider tested for HMAC and signing/verification.

## Source Files
- `AGXIOME.Tests/PropertyBasedTests.cs` (ingested 2026-05-14)