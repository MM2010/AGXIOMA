---
title: Audit Trail
category: entity
tags: [audit, security, logging]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Audit Trail

## Summary
The Audit Trail provides a tamper-evident, append-only record of security-relevant actions performed within the AGXIOME framework. It records who (pseudonymized subject hash) did what (action) to which resource and when. The design emphasizes immutability, thread safety, and easy integration via the `AuditLogger` helper. An in-memory implementation is provided for development/testing; production systems should replace it with a durable sink (e.g., Postgres or Seq).

## Key Details
- `AuditAction` enum groups actions by domain (Agent, Session, Team, Workflow, Knowledge, Auth, ApiKey, Key, Config, Security) with numeric codes.
- `AuditEntry` is an immutable record containing `EntryId`, `Timestamp`, `Action`, `Severity`, `SubjectHash` (first 16 hex chars of SHA-256 of subject ID), `ResourceType`, `ResourceId`, `OutcomeDetail`, and optional `Metadata`.
- `IAuditTrail` interface defines two operations: `RecordAsync` (write-only) and `QueryAsync` (filtered stream).
- `InMemoryAuditTrail` uses a `ConcurrentQueue` bounded by `maxCapacity` (default 10,000); oldest entries are evicted when capacity is exceeded.
- `AuditLogger` wraps the interface and automatically pseudonymizes subject IDs via `Pseudonymize()` before recording.
- Query filtering supports time range, action, minimum severity, subject hash, and resource type, with a maximum result limit.
- No mutation or deletion of past entries is allowed by design — supports compliance and forensic analysis.

## Cross-References
- [[SecurityOverview]] — Audit trails are a core security requirement for accountability.
- [[ThreadSafety]] — The in-memory implementation uses thread-safe patterns (`ConcurrentQueue`, `Interlocked`).

## Source Files
- `AGXIOME.Security/AuditTrail.cs` (ingested 2026-05-14)