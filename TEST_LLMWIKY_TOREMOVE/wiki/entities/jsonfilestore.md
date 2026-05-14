---
title: JsonFileCommandStore
category: entity
tags: [json, file, store, persistence, command store]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# JsonFileCommandStore

## Summary
A file-based command store that persists entities as JSON files—one file per entity type. Provides O(1) lookups via an in-memory dictionary, thread-safe access using `ReaderWriterLockSlim`, and crash-safe writes through an atomic temp-file + rename strategy. Implements `ICommandStore` for use with the AGXIOMA event sourcing/command handling pipeline.

## Key Details
- Implements `ICommandStore` and `IDisposable`.
- Configured via `JsonFileStoreOptions` (requires `BaseDirectory`, optional `PrettyPrint`).
- Stores all entities of type `T` in a single file named `{lowercase-type-name}s.json`.
- Uses an in-memory `Dictionary<string, T>` indexed by the `Id` property of each entity.
- Writes are performed to a `.tmp` file then atomically renamed (POSIX; best-effort on Windows) to prevent corruption on crash.
- Thread-safety achieved with `ReaderWriterLockSlim`; write locks for save/update/delete, no explicit read lock (in-memory load per operation).
- `SaveAsync` and `UpdateAsync` are identical (upsert behavior).
- Entity ID is extracted from an `Id` property; falls back to `Guid.NewGuid()` if not found.
- JSON serialization uses `System.Text.Json` with case-insensitive property matching.

## Cross-References
- [[ICommandStore]] — interface this store implements for writing commands/events.
- [[StorageProviderType]] — enum value `JsonFile` returned by the store.

## Source Files
- `AGXIOME.Storage/Stores/JsonFileStore.cs` (ingested 2026-05-14)