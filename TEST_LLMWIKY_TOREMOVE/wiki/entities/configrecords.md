---
title: ConfigRecords
category: entity
tags: [config, records, agent, LLM, storage, team, learning, governance]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# ConfigRecords

## Summary
Configuration records in AGXIOME are immutable data structures used to initialize and control agents, LLM providers, storage backends, teams, learning modes, and governance features. Each record provides sensible defaults and typed properties to avoid runtime errors and simplify setup. They are designed for serialization (e.g., JSON or YAML) and can be composed to form a complete agent or team configuration.

## Key Details
- `AgentConfig` defines a single agent's identity, system prompt, LLM engine, persistence, tool registrations, skills, streaming, retry, and iteration limits.
- `LLMWIKYConfig` specifies provider type, model, API key, endpoint, temperature, tokens, retries, rate limits, cost tracking, and fallback providers.
- `StorageConfig` selects the default storage provider (e.g., PostgreSQL) and per-provider connection details, plus an optional `MarkdownMemoryConfig` for file-based memory.
- `TeamConfig` configures multi-agent orchestration: mode (coordinate/vote/etc.), timeouts, partial failure handling, retries, and caching.
- `LearningConfig` controls when and how the framework learns from user interactions, entity memory, session context, and decision logs.
- `GovernanceConfig` enables human-in-the-loop (HITL), audit hooks, and background evaluations for compliance and safety.
- All records use `init`-only properties, making them immutable after object initialization.
- Default values are chosen for common use cases (e.g., GPT-4o, 20 iterations, 30-second timeouts).

## Cross-References
- [[Agents]] — `AgentConfig` is the primary input to create agents.
- [[Teams]] — `TeamConfig` drives team orchestration behavior.
- [[LLMProviders]] — `LLMWIKYConfig` references provider types like OpenAI, Azure, etc.
- [[StorageBackends]] — `StorageConfig` and `MarkdownMemoryConfig` define persistence.
- [[Learning]] — `LearningConfig` controls autonomous learning strategies.
- [[Governance]] — `GovernanceConfig` sets safety and audit policies.

## Source Files
- `AGXIOME.Shared/Config/ConfigRecords.cs` (ingested 2026-05-14)