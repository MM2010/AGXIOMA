---
title: TeamMode
category: entity
tags: [teammode, enum, coordination]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# TeamMode

## Summary
`TeamMode` is an enumeration that defines the different modes of coordination or communication for a team of agents within the AGXIOMA framework. Each value represents a distinct pattern for how agents interact, share information, or collaborate on tasks, enabling flexible team orchestration.

## Key Details
- Defined in the `AGXIOME.Shared.Models` namespace.
- Enum values: `Coordinate`, `Route`, `Broadcast`, `Tasks`, `SocialAlgorithm`.
- `Coordinate` likely indicates direct agent-to-agent synchronization.
- `Route` may refer to message routing or path-based communication.
- `Broadcast` suggests one-to-many message dissemination.
- `Tasks` could represent a task-oriented assignment model.
- `SocialAlgorithm` implies behavior influenced by social or swarm-like algorithms.

## Cross-References
- [[agent]] — agents are the entities that adopt a TeamMode for collaboration.
- [[coordination]] — broader concept of agent coordination patterns.

## Source Files
- `AGXIOME.Shared/Models/TeamMode.cs` (ingested 2026-05-14)