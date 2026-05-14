---
title: AgxiomeErrorCode
category: entity
tags: [error-codes, enum, shared, agxiome, infrastructure]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AgxiomeErrorCode

## Summary
The `AgxiomeErrorCode` enumeration provides a structured, module‑scoped classification of all error codes used across the AGXIOME .NET agent framework. It follows a consistent `{Modulo}{Categoria}{Numero}` pattern, where each major subsystem (Agent Engine, LLM, Tools, Storage, etc.) is assigned a unique numeric range. This design enables unambiguous error identification, granular logging, and systematic recovery strategies throughout the framework.

## Key Details
- Defined in namespace `AGXIOME.Shared.Errors`.
- Range categories: 0‑99 (generic), 100‑199 (Agent Engine), 200‑299 (LLM Provider), 300‑399 (Tools), 400‑499 (Storage/Database), 500‑599 (Redis/Cache), 600‑699 (X‑Link Transport), 700‑799 (RAG/Knowledge), 800‑899 (Security), 900‑999 (Guardrails), 1000‑1099 (HITL), 1100‑1199 (Sessions), 1200‑1299 (Network/Infrastructure), 1300‑1399 (Configuration).
- Each numeric value is unique across the entire enum; no overlapping ranges.
- Flag values are not used; each code is a plain integer.
- Common HTTP‑related errors (e.g., 429 Rate Limited, 502 Bad Gateway) are mirrored with dedicated codes.
- Enum is extensible – new subsystems can be added in future ranges beyond 1399.
- Used internally by exception classes and result handling patterns throughout the AGXIOME framework.

## Cross-References
- None yet.

## Source Files
- `AGXIOME.Shared/Errors/AgxiomeErrorCode.cs` (ingested 2026-05-14)