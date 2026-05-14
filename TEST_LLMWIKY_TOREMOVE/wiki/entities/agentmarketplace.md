---
title: AgentMarketplace
category: entity
tags: [marketplace, agent registry, prompt loader]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AgentMarketplace

## Summary
The `AgentMarketplace` class provides a local registry of agent definitions, tools, prompts, skills, and other marketplace entries. It loads entries from JSON, YAML, and Markdown files in a specified directory, and supports programmatic registration, lookup by name, and keyword-based search. Accompanying helpers (`MarketplaceRegistry`, `PromptLoader`) simplify loading the marketplace or individual prompts from files.

## Key Details
- **Constructor**: `AgentMarketplace(string registryPath)` – creates the directory if it does not exist.
- **`LoadRegistry()`**: Clears existing entries and scans the registry path for `.json`, `.yaml`, and `.md` files.
  - JSON files are deserialized into `MarketplaceEntry` (invalid files are silently skipped).
  - YAML support is currently not implemented (files are ignored).
  - Markdown files cause a new entry named after the file (without extension) with type `"markdown"` and the file content as `Content`.
- **`Register(MarketplaceEntry entry)`**: Adds `entry` to the dictionary keyed by `entry.Name`.
- **`Find(string name)`**: Returns the `MarketplaceEntry` with the given name, or `null`.
- **`Search(string keyword)`**: Returns a list of entries whose `Name`, `Description`, or any `Tag` contains the keyword (case-insensitive).
- **`MarketplaceEntry`** record: `Name`, `Type` (e.g., "agent", "tool", "prompt", "skill", "markdown"), `Content`, `Description`, `Tags`, `Version`, `Author`.
- **`MarketplaceRegistry.LoadFromDirectory(string path)`**: Convenience factory that creates an `AgentMarketplace`, calls `LoadRegistry()`, and returns it.
- **`PromptLoader`**: Static helpers to read prompt text from a file (`LoadFromFile`) or extract a JSON property by key (`LoadFromJson`, default key `"prompt"`).
- All comparisons are case-insensitive (`OrdinalIgnoreCase`).

## Cross-References
- [[Agent]] – Marketplace entries of type `"agent"` define agent configurations.
- [[Prompt]] – Entries of type `"prompt"` or `"markdown"` are loaded via `PromptLoader`.
- [[Tool]] – Tools can be registered as marketplace entries.

## Source Files
- `AGXIOME.Core/Marketplace/AgentMarketplace.cs` (ingested 2026-05-14)