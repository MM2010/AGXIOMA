---
title: SecretsScanner
category: entity
tags: [security, secrets, scanner, redaction, logging, pattern-matching]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# SecretsScanner

## Summary
The `SecretsScanner` is a pattern-based detection and masking utility designed to prevent accidental exposure of credentials, tokens, keys, and connection strings in log output, serialized payloads, and structured error responses. It is intended to be called at logging boundaries (not as a security gate) and replaces matched secrets with a fixed `***REDACTED***` token.

## Key Details
- Implements `ISecretsScanner` interface with `Scan(string)`, `Mask(string)`, and `ContainsSecrets(string)` methods.
- Scanning returns a `ScanResult` containing a boolean `HasSecrets`, the `SafeText` with all secrets redacted, and a list of `SecretMatch` details (pattern name, category, position, length).
- Uses a `SecretPattern` record that pairs a `Regex` pattern with a replacement string (default `***REDACTED***`).
- Built-in patterns cover: JWT tokens, OpenAI API keys, AWS access/secret keys, Azure connection strings/keys, GitHub tokens, PEM private keys, generic passwords in key=value pairs, and connection strings for PostgreSQL/Redis/SQL Server.
- Patterns are applied sequentially; each pass operates on the already‑masked string to avoid double‑matching the replacement token.
- Secrets are categorized via the `SecretCategory` enum: `ApiKey`, `JwtToken`, `ConnectionString`, `PrivateKey`, `Password`, `AwsCredential`, `AzureKey`, `GitHubToken`, `GenericSecret`.
- The default constructor loads all built-in patterns from `BuiltInPatterns.All`.
- Designed to be fast and non‑destructive (only masks, does not validate or block).

## Cross-References
- [[SecurityOverview]] — central security documentation and guidelines for the AGXIOMA framework.
- [[AgentLogger]] — logging component that integrates SecretsScanner for safe log output.

## Source Files
- `AGXIOME.Security/SecretsScanner.cs` (ingested 2026-05-14)