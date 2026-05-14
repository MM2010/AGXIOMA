---
title: AgentAssemblyContext
category: entity
tags: [assemblyloadcontext, isolation, collectible, dynamic-agent]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AgentAssemblyContext

## Summary
A sealed, collectible `AssemblyLoadContext` that isolates assembly loading for individual dynamic agents. By setting `IsCollectible = true`, it enables the runtime to unload the entire context (and all assemblies loaded within it) when no longer referenced, allowing safe hot-reload of agent code. The custom `Load` override prevents automatic assembly resolution, ensuring only assemblies explicitly provided via `LoadFromStream` are used.

## Key Details
- Inherits from `System.Runtime.Loader.AssemblyLoadContext`.
- Constructor accepts an `agentId` and sets the context name to `"AgentCtx_{agentId}"`.
- `IsCollectible = true` – supports unloading via garbage collection after calling `Unload()`.
- Overrides `Load(AssemblyName)` to return `null` – no automatic fallback loading; all assemblies must be supplied explicitly.
- Use a `WeakReference<AgentAssemblyContext>` to verify that the GC has reclaimed memory after `Unload()`.

## Cross-References
- [[DynamicAgent]] — agents create and manage their own `AgentAssemblyContext` for isolation.

## Source Files
- `AGXIOME.DynamicRuntime/Isolation/AgentAssemblyContext.cs` (ingested 2026-05-14)