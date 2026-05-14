---
title: AgentDropFolderService
category: entity
tags: [hosted service, hot folder, file watcher, agent discovery]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AgentDropFolderService

## Summary
A `IHostedService` that watches a configurable drop-folder directory per user for new, modified, or deleted `.cs` agent source files. It delegates file scanning to `AgentFileScanner`, registers discovered agents in `DynamicAgentRegistry`, and marks modified agents for recompilation. This service is the primary runtime mechanism for hot‑loading user‑defined agents.

## Key Details
- Implements `IHostedService` and `IDisposable`; started via `StartAsync` and stopped via `Dispose`.
- Manages a list of user IDs (`_userIds`) that can be extended at runtime via `AddUser()`.
- For each user, creates an `AgentDropFolder` watcher and performs an initial scan of existing `.cs` files.
- Listens for `AgentFileEvent` from `AgentDropFolder` — handles `Discovered`, `Modified`, and `Removed` kinds.
- On `Discovered`: scans the file, registers agent in registry with status `Discovered`.
- On `Modified`: compares source hash; if different, marks agent as `NeedsRecompile` and updates registry.
- On `Removed`: removes the agent entry from registry using the file name as a fallback identifier.
- Uses `AgentFileScanner.ScanAsync` to extract metadata (`AgentFileMetadata`) from source files.
- Registers agents as `DiscoveredAgentEntry` objects with ID, description, file path, source hash, status, and timestamp.
- Logs all significant events via `ILogger<AgentDropFolderService>`.
- Default base path is `.agxioma` under the user's profile directory; can be overridden.

## Cross-References
- [[DynamicAgentRegistry]] — central registry updated by this service
- [[AgentDropFolder]] — per-user file watcher managed by this service
- [[AgentFileScanner]] — used to parse agent metadata from `.cs` files
- [[DiscoveredAgentEntry]] — data model for newly discovered agents
- [[AgentFileEvent]] — event type consumed by this service
- [[AgentFileEventKind]] — enumeration of event kinds (Discovered, Modified, Removed)

## Source Files
- `AGXIOME.DynamicRuntime/HotFolder/AgentDropFolderService.cs` (ingested 2026-05-14)