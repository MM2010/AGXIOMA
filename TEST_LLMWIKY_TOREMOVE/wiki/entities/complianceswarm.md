---
title: ComplianceSwarm
category: entity
tags: [compliance, security, agent-patterns, audit, pii-detection]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# ComplianceSwarm

## Summary
The `ComplianceSwarm` is an enterprise-grade agent pipeline designed for regulated industries (finance, healthcare, legal, government). It wraps every agent run with PII detection (input + output), cryptographic auditing, role‑based access control (RBAC), sensitive data masking, and compliance metadata. It includes a configurable breach policy (block, redact, or alert) and an immutable audit trail.

## Key Details
- **Class**: `public sealed class ComplianceSwarm` in `AGXIOME.Core.Patterns`
- **Constructor**: Takes `IReadOnlyList<IAgxiomeAgent>` agents and optional `ComplianceConfig` (defaults provided).
- **Core Method**: `RunAsync(string task, ComplianceContext context, CancellationToken ct)` returns `ComplianceResult`.
- **RBAC**: If `AllowedRoles` is non‑empty, the user must have at least one matching role; otherwise access is denied.
- **PII Detection**: Uses compiled regex patterns for email, phone, SSN, credit card, IP address, IBAN, plus custom patterns from config.
- **BreachPolicy**: `Block` stops execution on PII detection; `Redact` masks detected patterns; `Alert` continues but invokes a callback.
- **Audit Trail**: Each event (access denied, PII detected, agent invoked/completed/failed) is recorded as an `AuditRecord` and passed to an optional `OnAuditEntry` callback.
- **ComplianceResult**: Contains output, agent results, audit trail, duration, block status, data classification, and regulatory reference.
- **Agent Wrapping**: Each agent call is pre‑ and post‑audited, with output scanned for PII before returning.

## Cross-References
- [[IAgxiomeAgent]] — agent interface executed within the compliance pipeline.
- [[ComplianceConfig]] — configuration for allowed roles, breach policy, custom PII patterns, and callbacks.
- [[ComplianceContext]] — context carrying user identity, roles, and metadata.
- [[ComplianceResult]] — output type encapsulating results and audit trail.

## Source Files
- `AGXIOME.Core/Patterns/ComplianceSwarm.cs` (ingested 2026-05-14)