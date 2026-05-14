---
title: LazyAgentCompiler
category: entity
tags: [lazy-loading, compilation, concurrency, agents, caching]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# LazyAgentCompiler

## Summary
Thread‑safe, lazy per‑agent compiler that ensures the first caller for a given `userId/agentId` triggers compilation and all subsequent callers await the same cached task. Eliminates race conditions by combining `ConcurrentDictionary` with `Lazy<Task<CompiledAgent?>>`. Supports cache invalidation so agents requiring recompilation can be rebuilt on the next request.

## Key Details
- Key format: `"{userId}::{agentId}"`
- Uses `ConcurrentDictionary<string, Lazy<Task<CompiledAgent?>>>` to store and deduplicate compilation tasks.
- `GetOrCompileAsync(userId, agentId, ct)` returns the compiled agent or `null` on failure.
- `Invalidate(userId, agentId)` removes the entry; next call triggers a fresh compilation.
- Internal compilation steps:
  1. Load source from `DynamicAgentRegistry` file path via `File.ReadAllTextAsync`.
  2. Run static validation via `AgentCodeValidator.Validate`.
  3. Compile via `AgentCompiler.CompileAsync` to a bin directory under `_basePath/agents/{userId}/bin`.
  4. Update registry status (`Compiling`, `Failed`, `Compiled`) throughout the process.
- Base path defaults to `~/.agxioma`.
- Handles `IOException` and validation/compilation errors gracefully, marking the agent as `Failed`.

## Cross-References
- [[entities/agentcompiler]] — core compilation logic used internally.
- [[entities/dynamicagentregistry]] — registry that stores agent metadata and file paths.
- [[entities/compiledagent]] — result type returned by the compiler.
- [[entities/agentcodevalidator]] — performs static validation before compilation.
- [[entities/agententrystatus]] — status enum updated during compilation lifecycle.

## Source Files
- `AGXIOME.DynamicRuntime/Compilation/LazyAgentCompiler.cs` (ingested 2026-05-14)