---
title: Hotkeys
category: entity
tags: [key patterns, hot store, redis, orchestration infrastructure]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Hotkeys

## Summary
The `HotKeys` class is a centralized key‑name factory for the hot store (typically Redis) used by the AGXIOMA orchestration engine. All storage and pub/sub key patterns are defined here; a single prefix (`agxiome:orch`) ensures consistency across plan, task, session, and notification keys.

## Key Details
- Internal static class in `AGXIOME.Orchestration.Infrastructure`
- Constant prefix: `"agxiome:orch"`
- Generates keys with format: `{prefix}:{entity}:{id}:{field}`
- Covers **plan** keys: meta, step, status, tasks
- Covers **task** keys: spec, result, status, jobid
- Covers **session** keys: list of plans for a session
- Provides a **pub/sub channel** key for plan notifications
- All methods accept GUIDs (planId, taskId) or string (sessionId)
- Changing the prefix once propagates to every key, reducing maintenance risk

## Cross-References
- [[Plans]] — plan‑level keys are used when storing and retrieving orchestration plans
- [[Tasks]] — task‑level keys support task specifications and results
- [[Sessions]] — session keys group plans by session identifier
- [[Notifications]] — the notify channel enables real‑time plan event distribution

## Source Files
- `AGXIOME.Orchestration/Infrastructure/HotKeys.cs` (ingested 2026-05-14)