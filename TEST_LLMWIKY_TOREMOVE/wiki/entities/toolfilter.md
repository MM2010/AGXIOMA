---
title: ToolFilter
category: entity
tags: [tools, filtering, allowlist, blocklist, security]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# ToolFilter

## Summary
The ToolFilter provides a thread-safe, per-scope mechanism to restrict which tools an agent can invoke during a session or run. It supports two modes: **allowlist** (only listed tools are permitted) and **blocklist** (all tools are permitted except those listed). When no rule is registered for a scope, all tools are allowed by default. This enables fine-grained access control at the session and run level.

## Key Details
- Enum `ToolFilterMode` defines `Allowlist = 0` and `Blocklist = 1`.
- Record `ToolFilterRule` holds a `ScopeId` (session or run identifier), the mode, and a case-sensitive set of tool names.
- Interface `IToolFilter` exposes `SetFilter`, `IsAllowed`, `GetFilter`, `ClearFilter`, `ClearAll`, and `GetAllFilters`.
- Implementation `ToolFilter` uses a `ConcurrentDictionary` for thread safety.
- `IsAllowed` returns `true` if no rule exists for the scope; otherwise it checks membership according to the mode.
- Scope hierarchy: run-level rules override session-level rules when both are defined (application logic decides which scope to check).
- An empty tool name set combined with `Blocklist` effectively allows all tools.
- Designed for use in tool dispatch pipelines before tool execution.

## Cross-References
- [[Session]] — session-level filtering rules are set during bootstrap.
- [[Run]] — run-level rules can override session rules for a specific agent invocation.
- [[Tool]] — the tools that are subject to filtering.

## Source Files
- `AGXIOME.Core/Tools/ToolFilter.cs` (ingested 2026-05-14)