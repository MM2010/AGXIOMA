---
title: TeamTopology
category: entity
tags: [enum, topology, multi-agent, coordination]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# TeamTopology

## Summary
`TeamTopology` is an enumeration that defines the possible coordination patterns for multi-agent teams within the AGXIOME framework. It specifies how agents interact and communicate, enabling different topologies like routing, swarming, pipelining, or supervisor-led orchestration.

## Key Details
- Defined in namespace `AGXIOME.Shared.Models`.
- Values: `Router`, `Swarm`, `Pipeline`, `Supervisor`.
- `Router` – agents forward messages along a defined path.
- `Swarm` – agents operate in a decentralized, emergent manner.
- `Pipeline` – agents process work in sequential stages.
- `Supervisor` – a single agent coordinates and delegates tasks.
- Used to configure multi-agent coordination strategies at runtime.

## Cross-References
- [[Agent]] – agents are organized according to these topologies
- [[Team]] – the team object likely accepts a topology setting

## Source Files
- `AGXIOME.Shared/Models/TeamTopology.cs` (ingested 2026-05-14)