---
title: CSP Hardening
category: entity
tags: [csp, security, hardening, dotnet]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# CSP Hardening

## Summary
The CSP Hardening module provides a typed, allowlist-driven system for building and validating Content-Security-Policy headers in AGXIOME. It enforces strict defaults (no wildcards, no `unsafe-inline`/`unsafe-eval`) and offers a fluent builder to specify trusted origins for each directive. A production validation step rejects common misconfigurations before deployment, and the module integrates as an ASP.NET Core middleware extension.

## Key Details
- `CspEnvironment` enum: `Development`, `Staging`, `Production`.
- `CspPolicy` record: holds all CSP directives and serializes to a header value via `ToHeaderValue()`.
- `CspBuilder` fluent API: defaults to `'self'` for most directives, `'none'` for `frame-src` and `object-src`, and `data:` for `img-src`. `UpgradeInsecureRequests` defaults to `true`.
- Methods like `AllowScriptSrc()`, `AllowStyleSrc()`, `AllowScriptNonce()` allow adding trusted origins or nonces.
- `CspBuilder.DefaultStrict()` returns a production-ready policy with `upgrade-insecure-requests` enabled.
- `CspPolicyValidator.ValidateForProduction()` checks all directives for forbidden wildcards, `'unsafe-inline'`, and `'unsafe-eval'`.
- Designed to deny wildcards and unsafe patterns in production; requires explicit allowlisting.
- Integrates as ASP.NET Core middleware (middleware code not shown in source, but extension method is implied).

## Cross-References
- [[AGXIOME.Web.Security]] — overarching security middleware context

## Source Files
- `AGXIOME.Security/CspHardening.cs` (ingested 2026-05-14)