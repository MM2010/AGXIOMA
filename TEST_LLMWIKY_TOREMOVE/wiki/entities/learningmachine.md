---
title: LearningMachine
category: entity
tags: [learning, memory, agent, core]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# LearningMachine

## Summary
The `LearningMachine` is the default implementation of `ILearningMachine` in the AGXIOME framework. It manages persistent agent learning by storing user memories, user profiles, entity facts, session contexts, and learned knowledge. Its behavior is driven by a `LearningConfig` that controls modes (e.g., always‑on auto‑save) and is designed for production use with a PostgreSQL learning store.

## Key Details
- Implements `ILearningMachine` for all learning‑related operations.
- Configured via `LearningConfig` (injected via constructor); `Configure()` allows runtime updates.
- `ProcessAsync` automatically persists user memory and profile when `UserMemoryMode` or `UserProfileMode` is `LearningMode.Always`.
- Supported operations: save/retrieve user memories, get/create/update user profiles, save/retrieve entity facts, manage session contexts, save/search learned knowledge, and log decisions.
- Default implementations are stubs (return `Task.CompletedTask` or empty collections) – actual persistence is wired in production (e.g., PostgreSQL).
- Used by the agent runtime to capture observations, update profiles, and maintain long‑term context across sessions.

## Cross-References
- [[ILearningMachine]] — interface contract that `LearningMachine` implements
- [[LearningConfig]] — configuration object that drives learning modes
- [[UserProfile]] — data model for user profiles
- [[MemoryEntry]] — data model for user memories
- [[EntityMemory]] — data model for entity facts
- [[SessionContext]] — data model for session learning state
- [[LearnedKnowledge]] — data model for stored insights

## Source Files
- `AGXIOME.Core/Learning/LearningMachine.cs` (ingested 2026-05-14)