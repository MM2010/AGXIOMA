---
title: CallableMemberRegistry
category: entity
tags: [registry, callable, runtime, descriptor, in-memory]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# CallableMemberRegistry

## Summary
The `CallableMemberRegistry` provides a runtime, in-memory registry for resolvable callable members (agents, tools, skills, and team proxies) within a named scope (e.g., a session or team). It implements the `ICallableMemberRegistry` interface, offering operations to register, resolve, list, and unregister callable descriptors by name. Designed for single-process use, it can be replaced with a distributed store (e.g., Redis) in multi-node deployments.

## Key Details
- **CallableMemberType** enum: `Agent`, `Tool`, `Skill`, `TeamProxy` — avoids string comparisons.
- **CallableMemberDescriptor** record holds metadata: `Name`, `Type`, `EntityId`, `Description`, `Role`, `Metadata`, `RegisteredAt`.
- **ICallableMemberRegistry** interface defines: `RegisterAsync`, `ResolveAsync`, `ListAsync`, `UnregisterAsync`, `ClearScopeAsync`.
- All methods accept a `scopeId` (string) to isolate registrations (e.g., per session or team).
- `RegisterAsync` replaces any existing entry with the same name within the same scope.
- `ResolveAsync` returns `null` if the name is not found; `ListAsync` supports optional type filtering.
- `UnregisterAsync` and `ClearScopeAsync` allow cleanup without errors on missing entries.
- Implementation uses a nested `Dictionary<string, Dictionary<string, CallableMemberDescriptor>>` (scope → name → descriptor).
- **Not thread-safe**: No internal locking; intended for single-threaded or externally synchronized usage.
- For distributed or multi-process environments, the interface can be backed by a Redis hash per scope or a database table.
- Parameter validation via `ArgumentException.ThrowIfNullOrWhiteSpace` on scopeId and name.

## Cross-References
- [[entities/callabledescriptor]] — the metadata record used by the registry (same source file)
- [[entities/icallablememberregistry]] — the interface the registry implements
- [[concepts/runtime-resolution]] — how callable members are resolved at runtime within the AGXIOME framework

## Source Files
- `AGXIOME.Core/Storage/CallableMemberRegistry.cs` (ingested 2026-05-14)