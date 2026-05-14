---
title: InputSanitization
category: entity
tags: [security, sanitization, validation]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# InputSanitization

## Summary
InputSanitization provides a centralized, deny-by-default validation and sanitization layer for all inputs crossing .NET↔JavaScript and API trust boundaries within the AGXIOME framework. It guards against common injection attacks (SQL, command, HTML/XSS), path traversal, and length-based attacks by requiring explicit allow rules. The system offers a configurable set of sanitization rules per field, predefined rules for common inputs (e.g., agent IDs, model names, free text), and a clear violation reporting mechanism.

## Key Details
- Core interface: `IInputSanitizer` with methods `Sanitize(string, SanitizationRule)` and `IsValid(string, SanitizationRule)`.
- Implementation: `InputSanitizer` class applies checks in fixed order: null/empty → length → path traversal → HTML strip/detect → SQL keywords → shell metacharacters → custom allow pattern.
- Checks are performed using compiled regular expressions for efficiency (`SqlKeywords`, `ShellMeta`, `PathTraversal`, `HtmlTags`).
- `SanitizationResult` is a value object returning `IsValid`, `SanitizedValue`, `Violation` enum, and optional detail string.
- `SanitizationViolation` enum: `None`, `TooLong`, `IllegalChars`, `NullOrEmpty`, `PathTraversal`, `HtmlInjection`, `SqlKeyword`, `ShellMeta`.
- `SanitizationRule` record allows per-field configuration: `MaxLength`, `AllowEmpty`, `StripHtml`, `CheckSqlKeywords`, `CheckShellMeta`, and a custom `AllowedPattern` regex.
- Predefined rules in `SanitizationRules` static class: `AgentId`, `AgentNameR`, `SessionId`, `ModelId`, `FreeText`, `ShellArg` with sensible defaults.
- Deny-by-default principle: any input that fails a check is rejected; `AllowEmpty` is the only opt-in exception.
- Designed for defense-in-depth; not a substitute for parameterized queries or output encoding.

## Cross-References
- [[Security]] — overall security guidelines for the AGXIOME framework

## Source Files
- `AGXIOME.Security/InputSanitization.cs` (ingested 2026-05-14)