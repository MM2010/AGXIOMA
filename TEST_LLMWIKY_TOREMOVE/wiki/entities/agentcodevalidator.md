---
title: AgentCodeValidator
category: entity
tags: [validation, security, roslyn, static-analysis, agent-framework]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AgentCodeValidator

## Summary
`AgentCodeValidator` is a static security analysis tool for user-provided C# code in the AGXIOMA dynamic runtime. Using Roslyn syntax tree walking (no compilation), it scans for blocked namespace references, member accesses, keywords, and unsafe contexts to prevent dangerous operations from being executed by agents.

## Key Details
- Defined in `AGXIOME.DynamicRuntime.Compilation` namespace as a static class.
- Returns a `ValidationResult` record with `Ok` (bool) and `Violations` (list of strings).
- Blocked namespace prefixes include `System.IO`, `System.Reflection`, `System.Diagnostics.Process`, `System.Runtime.InteropServices`, `System.Security`, `System.Net.Sockets`, and `Microsoft.Win32`.
- Blocked keywords: `unsafe`, `DllImport`, `Marshal`, `__makeref`.
- Checks four aspects via AST walk:
  1. `using` directives that match blocked namespaces.
  2. Member access expressions (e.g., `System.IO.File.ReadAllText`).
  3. Tokens containing blocked keywords.
  4. Explicit `unsafe` statement contexts.
- Does not compile the code — operates purely on the parsed syntax tree.
- Validation is case‑sensitive when matching namespace prefixes and keywords.

## Cross-References
- [[DynamicCompiler]] — likely companion for code execution after validation.
- [[SecurityModule]] — conceptual block for agent security policies.

## Source Files
- `AGXIOME.DynamicRuntime/Compilation/AgentCodeValidator.cs` (ingested 2026-05-14)