---
title: SessionType
category: entity
tags: [enum, session, types]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# SessionType

## Summary
`SessionType` is an enumeration that categorizes the kind of session being managed within the AGXIOMA framework. It distinguishes between sessions initiated by an **Agent**, a **Team**, or a **Workflow**, allowing the system to route and handle interactions appropriately.

## Key Details
- Defined as a `public enum` in the `AGXIOME.Shared.Models` namespace.
- Contains three members: `Agent`, `Team`, `Workflow`.
- `Agent` — a session tied to a single autonomous agent.
- `Team` — a session involving a group of agents collaborating.
- `Workflow` — a session driven by a predefined process or pipeline.
- Used to determine behavior, persistence, and lifecycle rules for different session contexts.

## Cross-References
- [[entities/agent]] — Agent sessions are created and managed for individual agents.
- [[entities/team]] — Team sessions coordinate multi-agent interactions.
- [[entities/workflow]] — Workflow sessions follow structured execution paths.
- [[entities/session]] — Core session entity that holds a `SessionType` property.

## Source Files
- `AGXIOME.Shared/Models/SessionType.cs` (ingested 2026-05-14)