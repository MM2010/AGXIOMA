---
title: AgentCacheStore
category: entity
tags: [caching, compilation, dynamic agents, SHA256, persistence]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AgentCacheStore

## Summary
`AgentCacheStore` is a cache loader that avoids redundant compilation of dynamic agents. At startup, it scans the `bin/` directory for compiled `.dll` assemblies, verifies their SHA‑256 hash against the corresponding `.cs` source file in `src/`, and loads the assembly directly if the hash matches. If the hash is missing or mismatched, it marks the agent entry as `NeedsRecompile` in the [[DynamicAgentRegistry]]. This ensures zero recompilation when source code has not changed, dramatically improving cold‑start performance.

## Key Details
- Scans `~/.agxioma/agents/{userId}/bin/` for `*.dll` files and their accompanying `.sha256` hash files.
- Compares the stored hash with a fresh hash of the source file in `src/` using `AgentFileScanner.ComputeHash`.
- On hash match, loads the assembly via `AgentAssemblyContext.LoadFromAssemblyPath` (an `AssemblyLoadContext`–based isolation context).
- Checks that the loaded assembly contains exactly one non‑abstract class implementing `ISimpleDynamicAgent`.
- Updates agent status to `Compiled` on success, or `NeedsRecompile` on any failure (missing files, hash mismatch, no valid agent type).
- The actual agent instance is created lazily by [[LazyAgentCompiler]]; the cache store only validates and pre‑loads the assembly.
- Operates asynchronously and respects a `CancellationToken`.
- Default base path is `~/.agxioma/` but can be overridden.
- The class is `sealed` and intended for use at boot time.

## Cross-References
- [[DynamicAgentRegistry]] — used to query, update, and persist agent status entries.
- [[AgentFileScanner]] — provides the `ComputeHash` method used for hash comparison.
- [[AgentAssemblyContext]] — the collectible `AssemblyLoadContext` used to load the cached assembly.
- [[ISimpleDynamicAgent]] — the interface that agent types must implement.
- [[LazyAgentCompiler]] — creates the agent instance from a cached assembly.

## Source Files
- `AGXIOME.DynamicRuntime/Persistence/AgentCacheStore.cs` (ingested 2026-05-14)