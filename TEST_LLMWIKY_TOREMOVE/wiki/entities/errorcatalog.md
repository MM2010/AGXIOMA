---
title: ErrorCatalog
category: entity
tags: [error-catalog, error-handling, localization, framework-core]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# ErrorCatalog

## Summary
`ErrorCatalog` is a static centralized registry of all error codes and descriptions used by the AGXIOME agent framework. It stores a **`FrozenDictionary`** mapping each `AgxiomeErrorCode` to an `ErrorDescriptor` that contains both a technical (English) and a user-facing (Italian) message, along with a severity and a category. This catalog ensures consistent error handling, prevents stack traces from being exposed to end users, and supports logging and internationalization across all framework modules.

## Key Details
- Uses `FrozenDictionary` for immutable, thread-safe, high-performance lookups.
- Every error is identified by an `AgxiomeErrorCode` enum value (e.g., `ERR-0000`).
- Each entry provides: technical message, user message (Italian), `ErrorSeverity` (Info, Warning, Error, Critical), and category (e.g., Generic, Agent, LLM, Tool, Storage, XLink, RAG, Security, Guardrail).
- Stack traces are never exposed to the user — they are only logged internally.
- Covers errors from all framework areas: Agent lifecycle, LLM interactions, Tool execution, Database, X-Link, RAG, Security, and Guardrails.
- Designed to be the single source of truth for error information; used by exception handling and logging infrastructure.
- Severity levels guide automatic retries and alerting: `Critical` typically halts operation, `Warning` may trigger backoff, `Info` provides user feedback.

## Cross-References
- [[AgxiomeErrorCode]] — enum defining all error code identifiers
- [[ErrorDescriptor]] — data class holding the message and metadata for each error
- [[ErrorSeverity]] — enum classifying error criticality (Info, Warning, Error, Critical)
- [[entities/error-handling]] — overview of the framework’s error handling pattern

## Source Files
- `AGXIOME.Shared/Errors/ErrorCatalog.cs` (ingested 2026-05-14)