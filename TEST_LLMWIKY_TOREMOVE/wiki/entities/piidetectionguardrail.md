---
title: PiiDetectionGuardrail
category: entity
tags: [guardrail, PII, input, validation, regex]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# PiiDetectionGuardrail

## Summary
`PiiDetectionGuardrail` is an input guardrail that blocks user-provided content containing common personally identifiable information (PII). It uses four regular expressions to detect Social Security Numbers, credit card numbers, email addresses, and phone numbers. If any pattern matches, the guardrail returns a blocking result; otherwise, content passes validation.

## Key Details
- **Type:** `GuardrailType.Input` — applied to user input before processing
- **Severity:** `GuardrailSeverity.Block` — content is rejected upon detection
- **Name:** `"pii_detection"`
- **Patterns:**
  - SSN: `\b\d{3}-\d{2}-\d{4}\b`
  - Credit card: `\b(?:\d[ -]*?){13,16}\b`
  - Email: `\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b`
  - Phone: `\b\d{3}[-.]?\d{3}[-.]?\d{4}\b`
- **Namespace:** `AGXIOME.Core.Guardrails`
- **Implements:** `IGuardrail`
- **Validation method:** `ValidateAsync(string content, GuardrailContext ctx, CancellationToken ct)` — synchronous return via `Task.FromResult`
- **No dependencies on external services** – purely regex-based

## Cross-References
- [[PromptInjectionGuardrail]] — another input guardrail in the same namespace, blocks prompt injection attempts
- [[IGuardrail]] — the interface all guardrails implement
- [[GuardrailResult]] — the result type returned by validation

## Source Files
- `AGXIOME.Core/Guardrails/PIIDetectionGuardrail.cs` (ingested 2026-05-14)