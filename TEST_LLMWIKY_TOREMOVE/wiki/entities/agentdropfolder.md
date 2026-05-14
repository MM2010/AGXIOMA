---
title: AgentDropFolder
category: entity
tags: [hotfolder, agents, filesystemwatcher, debounce]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AgentDropFolder

## Summary
The `AgentDropFolder` class monitors the `~/.agxioma/agents/{userId}/src/` directory for changes to `.cs` files using a `FileSystemWatcher`. It emits debounced events (`Discovered`, `Modified`, `Removed`) via an `OnFileEvent` callback, preventing duplicate or premature notifications during editor saves. This enables hot-reload of agent source code in the AGXIOMA framework.

## Key Details
- Watches a per-user hot‑folder path: `{basePath}/agents/{userId}/src/`.
- Uses `FileSystemWatcher` with `NotifyFilters.FileName | LastWrite | Size` for `.cs` files only.
- Debounce window of **300 ms** – suppresses rapid consecutive events for the same file.
- Thread‑safe: all event handling runs on the thread pool via `Task.Run`.
- Handles `Created`, `Changed`, `Deleted`, and `Renamed` watcher events; renames are decomposed into `Removed` + `Discovered`.
- For `Modified` events, inserts a 50 ms extra delay to ensure the file is fully written before firing.
- Maintains a `ConcurrentDictionary<string, DateTime>` for per-file debounce tracking.
- Exposes a `SrcPath` property that returns the monitored directory path.
- Implements `IDisposable` to clean up the `FileSystemWatcher`.

## Cross-References
- [[AgentFileEvent]] — the event record raised by `OnFileEvent`.
- [[HotFolder (concept)]] — broader pattern of file‑based agent deployment.

## Source Files
- `AGXIOME.DynamicRuntime/HotFolder/AgentDropFolder.cs` (ingested 2026-05-14)