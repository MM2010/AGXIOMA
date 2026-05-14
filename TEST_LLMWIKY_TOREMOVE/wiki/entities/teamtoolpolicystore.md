---
title: Team Tool Policy Store
category: entity
tags: [storage, team, tool-policy, default-tools, in-memory]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Team Tool Policy Store

## Summary
The `TeamToolPolicyStore` is an in-memory storage implementation of `ITeamToolPolicyStore` that persists per-team default tool policies using an `ICommandStore` backend. It maintains a local dictionary index to provide fast reads, and supports upsert, retrieval, deletion, and listing of policies, as well as scope-filtered resolution of effective default tools for a given team and agent type.

## Key Details
- Implements `ITeamToolPolicyStore` with typed methods: `SetPolicyAsync`, `GetPolicyAsync`, `ClearPolicyAsync`, `ListPoliciesAsync`, and `GetEffectiveToolsAsync`.
- Backed by `ICommandStore` for actual persistence; uses a `Dictionary<string, TeamToolPolicy>` as a read-through cache.
- `GetEffectiveToolsAsync` filters entries by `ToolScope`: includes `AllAgents` tools for any scope, and scope-specific tools only when the requested scope matches.
- `TeamToolPolicy` includes a `LockAgentTools` flag to prevent agents from adding extra tools beyond defaults in governed teams.
- Follows an enum-first design: `ToolScope` is a dedicated enum (`AllAgents`, `SupervisorOnly`, `WorkersOnly`), never a string.
- Merging rules (team defaults + agent overrides) are resolved at agent build time, not stored here.
- Suitable for single-process deployments; no distributed locking is provided.

## Cross-References
- [[ITeamToolPolicyStore]] — typed storage contract this class implements.
- [[TeamToolPolicy]] — data record for per-team default tool policy.
- [[DefaultToolEntry]] — individual default tool entry with type name, scope, and optional config.
- [[ToolScope]] — enum that controls which agents receive a default tool.
- [[ICommandStore]] — generic persistence store used for durable writes.
- [[SS-08 Team Default Tools]] — design specification for this feature.

## Source Files
- `AGXIOME.Core/Storage/TeamToolPolicyStore.cs` (ingested 2026-05-14)