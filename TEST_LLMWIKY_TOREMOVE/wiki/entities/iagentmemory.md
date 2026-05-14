---
title: IAgentMemory
category: entity
tags: [interface, memory, agent, persistence]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# IAgentMemory

## Summary
The `IAgentMemory` interface defines the contract for persisting and retrieving an agent's interaction history and key-value state. It enables agents to maintain long‑term context across conversations. A companion interface, `ISemanticMemory`, provides semantic search over stored text using embeddings.

## Key Details
- `Task SaveInteractionAsync(AgentInteraction, CancellationToken)` — Stores a single agent interaction (e.g., a conversation turn) for later retrieval.
- `Task<IReadOnlyList<AgentInteraction>> GetHistoryAsync(string agentId, int limit = 50, DateTime? since, CancellationToken)` — Retrieves the most recent interactions for a given agent, with optional limit and date filter.
- `Task ClearAsync(string agentId, CancellationToken)` — Removes all stored interactions and state for the specified agent.
- `Task<T?> GetStateAsync<T>(string key, CancellationToken)` — Reads a typed value from the agent’s persistent key‑value store. Returns `default(T)` if key not found.
- `Task SetStateAsync<T>(string key, T value, TimeSpan? ttl, CancellationToken)` — Writes a typed value with an optional time‑to‑live (TTL) expiration.
- `ISemanticMemory` (related) provides `SemanticSearchAsync` and `StoreEmbeddingAsync` for vector‑based retrieval of memory entries.
- Implementations can vary storage backend (e.g., in‑memory, database, distributed cache).

## Cross-References
- [[entities/iagentinteraction]] — The data model for interactions saved and retrieved by `IAgentMemory`.
- [[entities/imemoryentry]] — The data model used by `ISemanticMemory` for semantic search results.
- [[concepts/agent-persistence]] — Overview of how agents maintain state and memory across sessions.

## Source Files
- `AGXIOME.Shared/Interfaces/IAgentMemory.cs` (ingested 2026-05-14)