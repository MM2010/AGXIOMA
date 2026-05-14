---
title: AgentCompiler
category: entity
tags: [compiler, Roslyn, CSharpCompilation, dynamic agent, isolation, caching, SHA256]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AgentCompiler

## Summary
`AgentCompiler` is the Roslyn-based compiler that transforms C# source code into an isolated, dynamically loadable agent assembly. It enforces a strict allowlist of referenced assemblies, computes a SHA256 hash of the source for disk caching, and returns a `CompiledAgent` containing a live `ISimpleDynamicAgent` instance loaded into an unloadable `AgentAssemblyContext`. This enables secure, hot‑reloadable agent execution within the AGXIOME runtime.

## Key Details
- **`CompileAsync(string source, string agentId, string? binPath, CancellationToken ct)`** — Asynchronously compiles source, emits a DLL, hashes the source, loads the assembly in an isolated context, and optionally persists both the DLL and its SHA256 hash to disk.
- **Allowlist** — Only assemblies containing `object`, `List<>`, `JsonDocument`, `HttpClient`, `Enumerable`, `ISimpleDynamicAgent`, and `ILLMProvider` are permitted; all other references are rejected.
- **Caching** — If `binPath` is provided, the compiled DLL is saved as `{agentId}.dll` and the hash as `{agentId}.sha256`. Subsequent runs can avoid recompilation by comparing hashes.
- **Isolation** — The resulting assembly is loaded via `AgentAssemblyContext`, which supports unloading to prevent memory leaks.
- **Source hash** — `SHA256` of the UTF‑8 source string, returned as lowercase hex via `Convert.ToHexStringLower`.
- **Error handling** — If compilation fails, all diagnostic errors are returned in a `CompileResult` with `Success = false`. If no class implements `ISimpleDynamicAgent`, an error is returned.
- **No unsafe code** — `allowUnsafe: false` is enforced.
- **Performance** — Compilation targets `OptimizationLevel.Release` and enables nullable reference types.

## Cross-References
- [[ISimpleDynamicAgent]] — The interface that every compiled agent must implement.
- [[AgentAssemblyContext]] — The load context used for isolated assembly loading and unloading.
- [[ILLMProvider]] — An allowed reference, indicating integration with LLM providers.
- [[CompiledAgent]] — The result record containing the agent ID, hash, context, and instance.

## Source Files
- `AGXIOME.DynamicRuntime/Compilation/AgentCompiler.cs` (ingested 2026-05-14)