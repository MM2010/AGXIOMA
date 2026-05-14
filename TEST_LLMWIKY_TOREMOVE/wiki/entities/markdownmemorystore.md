---
title: MarkdownMemoryStore
category: entity
tags: [memory, storage, markdown, IAgentMemory, knowledgebase, grep]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# MarkdownMemoryStore

## Summary
MarkdownMemoryStore is the default implementation of [[IAgentMemory]] that persists agent interactions as individual Markdown files on disk. Each interaction is stored as a `.md` file with YAML frontmatter containing metadata (agent ID, role, timestamp, tool calls) and the body holds the interaction content. It supports full-text grep search across all files and optional semantic search via [[IKnowledgeBase]] when configured. This design provides parity with swarm-based memory patterns by combining file-native storage with retrieval-augmented generation (RAG).

## Key Details
- Implements `IAgentMemory` using a configurable base directory (`MarkdownMemoryConfig.BasePath`).
- Files are organized in subdirectories named after the sanitized agent ID.
- Each file is named `{timestamp}_{role}.md` (e.g., `20260514-120000_user.md`).
- YAML frontmatter includes `agent`, `role`, `created_at`, and `tool_calls` fields.
- `SaveInteractionAsync` writes a new markdown file for an `AgentInteraction`.
- `GetHistoryAsync` returns the most recent interactions for a given agent (sorted descending, limit optional).
- `ClearAsync` deletes all `.md` files for an agent.
- Provides state persistence via `SetStateAsync<T>` and `GetStateAsync<T>` stored as JSON files under `state/` subdirectory.
- `LoadMarkdownAsync` bulk-loads all `.md` files from a directory (or the base path).
- `SaveAsMarkdownAsync` writes an interaction to a specific file path, useful for direct export.
- Full-text search: `SearchByGrepAsync(string query)` performs case-insensitive content scan.
- Semantic search: `SearchSemanticAsync` delegates to `IKnowledgeBase` if `UseKnowledgeBase` is true in config.
- Uses `Regex` to parse frontmatter from file content.
- Sanitizes agent IDs and roles for safe file naming.

## Cross-References
- [[IAgentMemory]] — the interface that MarkdownMemoryStore implements.
- [[AgentInteraction]] — the data model used for memory entries.
- [[MarkdownMemoryConfig]] — configuration class driving store behavior.
- [[IKnowledgeBase]] — optional semantic search backend.

## Source Files
- `AGXIOME.Core/Storage/MarkdownMemoryStore.cs` (ingested 2026-05-14)