---
title: AgentMemoryManager
category: entity
tags: [agent, memory, plugin]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AgentMemoryManager

## Summary
The `AgentMemoryManager` is the concrete implementation of `IAgentMemory` used by agents in the AGXIOMA framework. It provides a pluggable memory backend, defaulting to in-process lists and dictionaries, with support for Markdown file persistence via `MarkdownMemoryStore`. Switching backends is controlled by `StorageConfig`, and the manager transparently delegates all operations to the chosen backend (or handles them locally for InMemory).

## Key Details
- **Class** `sealed class AgentMemoryManager : IAgentMemory`
- **Backend selection** based on `StorageConfig.MemoryBackend` (currently `MemoryBackend.InMemory` or `MemoryBackend.MarkdownFiles`).
- For non‑InMemory backends, all calls are forwarded to `_inner` (the configured `IAgentMemory` instance).
- For InMemory, the manager stores `AgentInteraction` objects in an internal list and key‑value state in a dictionary.
- Exposes an `Inner` property to allow direct access to the underlying backend (e.g., for MarkdownMemoryStore‑specific features).
- Methods: `SaveInteractionAsync`, `GetHistoryAsync`, `ClearAsync`, `GetStateAsync`, `SetStateAsync` – all accept a cancellation token.
- `GetHistoryAsync` supports limiting results and filtering by `since` date.

## Cross-References
- [[IAgentMemory]] – interface this class implements.
- [[StorageConfig]] – configuration object that determines the memory backend.
- [[MarkdownMemoryStore]] – alternative backend for file‑based memory persistence.
- [[MemoryBackend]] – enum defining available backends.

## Source Files
- `AGXIOME.Core/Agent/AgentMemoryManager.cs` (ingested 2026-05-14)